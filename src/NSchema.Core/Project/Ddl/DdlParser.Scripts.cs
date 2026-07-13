using NSchema.Project.Ddl.Models;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Project.Ddl;

internal sealed partial class DdlParser
{
    /// <summary>
    /// Parses a SCRIPT statement.
    /// </summary>
    private void ParseScript(List<Script> scripts)
    {
        Advance(); // SCRIPT
        var name = Expect(TokenKind.String, "a quoted script name").Text;
        ExpectKeyword("RUN");

        var condition = RunCondition.Always;
        if (_current.IsKeyword("ALWAYS"))
        {
            Advance();
        }
        else if (_current.IsKeyword("ONCE"))
        {
            condition = RunCondition.Once;
            Advance();
        }
        else if (_current.IsKeyword("UNLESS"))
        {
            throw Error("'UNLESS EXISTS' is reserved for a future release; a script runs ALWAYS (the default) or ONCE.");
        }

        ExpectKeyword("ON");

        ScriptEvent scriptEvent;
        if (_current.IsKeyword("PRE") || _current.IsKeyword("POST"))
        {
            var phase = _current.IsKeyword("PRE") ? DeploymentPhase.Pre : DeploymentPhase.Post;
            Advance(); // PRE | POST
            ExpectKeyword("DEPLOYMENT");
            scriptEvent = new DeploymentEvent(phase);
        }
        else if (_current.IsKeyword("ADD") || _current.IsKeyword("ALTER"))
        {
            var trigger = ParseChangeTrigger();
            var (schema, table, member) = ParseMemberPath();
            scriptEvent = new ChangeEvent(trigger, table, member) { ScopeSchema = schema };
        }
        else
        {
            throw Error("Expected a script event: 'PRE DEPLOYMENT', 'POST DEPLOYMENT', 'ADD COLUMN', 'ALTER COLUMN TYPE' or 'ADD CONSTRAINT'.");
        }

        var (runOutsideTransaction, body) = ParseScriptTail("script");
        scripts.Add(new Script(new SqlIdentifier(name), body, scriptEvent) { RunOutsideTransaction = runOutsideTransaction, RunCondition = condition });
    }

    private ChangeTrigger ParseChangeTrigger()
    {
        if (_current.IsKeyword("ADD"))
        {
            Advance(); // ADD
            if (_current.IsKeyword("COLUMN"))
            {
                Advance();
                return ChangeTrigger.AddColumn;
            }
            if (_current.IsKeyword("CONSTRAINT"))
            {
                Advance();
                return ChangeTrigger.AddConstraint;
            }
        }
        else if (_current.IsKeyword("ALTER"))
        {
            Advance(); // ALTER
            ExpectKeyword("COLUMN");
            ExpectKeyword("TYPE");
            return ChangeTrigger.AlterColumnType;
        }

        throw Error("Expected 'ADD COLUMN', 'ALTER COLUMN TYPE' or 'ADD CONSTRAINT'.");
    }

    /// <summary>
    /// Parses the migration target path: <c>schema.table.member</c> at the top level (the member is a column or
    /// constraint name depending on the trigger), or the unqualified <c>table.member</c> inside a template body,
    /// where the schema binds to each schema the template is applied to.
    /// </summary>
    private (SqlIdentifier Schema, SqlIdentifier Table, SqlIdentifier Member) ParseMemberPath()
    {
        var first = ExpectIdentifier(_templateSchemaContext is null ? "a schema name" : "a table name");
        Expect(TokenKind.Dot, "'.' in the migration target path");
        var second = ExpectIdentifier(_templateSchemaContext is null ? "a table name" : "a column or constraint name");

        if (!Match(TokenKind.Dot))
        {
            if (_templateSchemaContext is { } templateSchema)
            {
                return (templateSchema, first, second);
            }
            throw Error("Expected '.' in the migration target path (schema.table.member).");
        }

        if (_templateSchemaContext is not null)
        {
            throw Error("A migration inside a template must use an unqualified 'table.member' path; " +
                        "the schema binds to each schema the template is applied to.");
        }
        var member = ExpectIdentifier("a column or constraint name");
        return (first, second, member);
    }

    /// <summary>
    /// Parses the tail shared by every script form — the optional options clause, then <c>AS $$ … $$;</c>.
    /// The body is opaque SQL lexed as a single DollarString token (so its own <c>;</c> is not a terminator);
    /// the <c>AS</c> keyword is the anchor, and the body's delimiters are then stripped.
    /// <paramref name="what"/> names the statement in errors.
    /// </summary>
    private (bool RunOutsideTransaction, string Sql) ParseScriptTail(string what)
    {
        var runOutsideTransaction = ParseRunOutsideTransactionOptions(what);

        if (!_current.IsKeyword("AS"))
        {
            throw Error($"Expected 'AS' before the {what} body.");
        }
        Advance(); // AS
        var dollar = Expect(TokenKind.DollarString, $"a {what} body as a dollar-quoted block ($$ … $$)");
        var body = StripDollarQuote(dollar.Text).Trim();
        Expect(TokenKind.Semicolon, $"';' to end the {what}");

        return (runOutsideTransaction, body);
    }

    /// <summary>
    /// Strips the opening and closing delimiters from a <see cref="TokenKind.DollarString"/>'s verbatim text
    /// (<c>$tag$ … $tag$</c>), returning its inner content. The lexer guarantees the tags are balanced.
    /// </summary>
    private static string StripDollarQuote(string text)
    {
        var tagLength = text.IndexOf('$', 1) + 1; // '$' … '$' opening tag, e.g. "$$" or "$body$"
        return text[tagLength..^tagLength];
    }

    /// <summary>
    /// Parses the optional <c>( run_outside_transaction = true )</c> clause shared by every script form,
    /// returning the flag (default false). <paramref name="what"/> names the statement in errors.
    /// </summary>
    private bool ParseRunOutsideTransactionOptions(string what)
    {
        if (_current.Kind != TokenKind.LeftParen)
        {
            return false;
        }

        Advance(); // (
        var runOutsideTransaction = false;
        if (_current.Kind != TokenKind.RightParen)
        {
            do
            {
                var keyPosition = _current.Position;
                var key = ExpectIdentifier($"a {what} option name").Value;
                Expect(TokenKind.Equals, "'=' after an option name");
                var value = ParseConfigValue();
                switch (key.ToLowerInvariant())
                {
                    case "run_outside_transaction":
                        runOutsideTransaction = value.AsBoolean();
                        break;
                    default:
                        throw new DdlSyntaxException($"Unknown {what} option '{key}'. Expected 'run_outside_transaction'.", keyPosition);
                }
            }
            while (Match(TokenKind.Comma));
        }
        Expect(TokenKind.RightParen, $"')' or ',' after a {what} option");
        return runOutsideTransaction;
    }
}
