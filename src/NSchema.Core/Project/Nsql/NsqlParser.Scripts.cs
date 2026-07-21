using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Scripts;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    /// <summary>
    /// Parses a SCRIPT statement.
    /// </summary>
    private ScriptStatement ParseScript(string? doc)
    {
        var position = _current.Position;
        Advance(); // SCRIPT
        var name = ExpectIdentifierNode("a script name");
        ExpectKeyword(NsqlKeywords.Run);

        RunCondition? condition = null;
        if (_current.IsKeyword(NsqlKeywords.Always))
        {
            condition = RunCondition.Always;
            Advance();
        }
        else if (_current.IsKeyword(NsqlKeywords.Once))
        {
            condition = RunCondition.Once;
            Advance();
        }
        else if (_current.IsKeyword(NsqlKeywords.Unless))
        {
            throw Error("'UNLESS EXISTS' is reserved for a future release; a script runs ALWAYS (the default) or ONCE.");
        }

        ExpectKeyword(NsqlKeywords.On);

        ScriptEventClause scriptEvent;
        var eventPosition = _current.Position;
        if (_current.IsAnyKeyword(NsqlKeywords.Pre, NsqlKeywords.Post))
        {
            var phase = _current.IsKeyword(NsqlKeywords.Pre) ? DeploymentPhase.Pre : DeploymentPhase.Post;
            Advance(); // PRE | POST
            ExpectKeyword(NsqlKeywords.Deployment);
            scriptEvent = new DeploymentEventClause(phase) { Position = eventPosition };
        }
        else if (_current.IsAnyKeyword(NsqlKeywords.Add, NsqlKeywords.Alter))
        {
            var trigger = ParseChangeTrigger();
            var path = ParseMemberPathNode();
            scriptEvent = new ChangeEventClause(trigger, path) { Position = eventPosition };
        }
        else
        {
            throw Error("Expected a script event: 'PRE DEPLOYMENT', 'POST DEPLOYMENT', 'ADD COLUMN', 'ALTER COLUMN TYPE' or 'ADD CONSTRAINT'.");
        }

        var (runOutsideTransaction, body) = ParseScriptTail("script");
        return new ScriptStatement(name, condition, scriptEvent, body, runOutsideTransaction) { Position = position, Doc = doc };
    }

    private ChangeTrigger ParseChangeTrigger()
    {
        if (_current.IsKeyword(NsqlKeywords.Add))
        {
            Advance(); // ADD
            if (_current.IsKeyword(NsqlKeywords.Column))
            {
                Advance();
                return ChangeTrigger.AddColumn;
            }
            if (_current.IsKeyword(NsqlKeywords.Constraint))
            {
                Advance();
                return ChangeTrigger.AddConstraint;
            }
        }
        else if (_current.IsKeyword(NsqlKeywords.Alter))
        {
            Advance(); // ALTER
            ExpectKeyword(NsqlKeywords.Column);
            ExpectKeyword(NsqlKeywords.Type);
            return ChangeTrigger.AlterColumnType;
        }

        throw Error("Expected 'ADD COLUMN', 'ALTER COLUMN TYPE' or 'ADD CONSTRAINT'.");
    }

    /// <summary>
    /// Parses the migration target path: <c>schema.table.member</c> at the top level (the member is a column or
    /// constraint name depending on the trigger), or the unqualified <c>table.member</c> inside a template body,
    /// where the schema binds to each schema the template is applied to.
    /// </summary>
    private MemberPath ParseMemberPathNode()
    {
        var first = ExpectIdentifierNode(_inTemplateBody ? "a table name" : "a schema name");
        Expect(TokenKind.Dot, "'.' in the migration target path");
        var second = ExpectIdentifierNode(_inTemplateBody ? "a column or constraint name" : "a table name");

        if (!Match(TokenKind.Dot))
        {
            if (_inTemplateBody)
            {
                return new MemberPath(null, first, second) { Position = first.Position };
            }
            throw Error("Expected '.' in the migration target path (schema.table.member).");
        }

        if (_inTemplateBody)
        {
            throw Error("A migration inside a template must use an unqualified 'table.member' path; " +
                        "the schema binds to each schema the template is applied to.");
        }
        var member = ExpectIdentifierNode("a column or constraint name");
        return new MemberPath(first, second, member) { Position = first.Position };
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

        if (!_current.IsKeyword(NsqlKeywords.As))
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
                var key = ExpectIdentifierNode($"a {what} option name").Value;
                Expect(TokenKind.Equals, "'=' after an option name");
                var value = ParseConfigValueNode();
                switch (key.ToLowerInvariant())
                {
                    case "run_outside_transaction":
                        runOutsideTransaction = AsBoolean(value);
                        break;
                    default:
                        throw new NsqlSyntaxException($"Unknown {what} option '{key}'. Expected 'run_outside_transaction'.", keyPosition);
                }
            }
            while (Match(TokenKind.Comma));
        }
        Expect(TokenKind.RightParen, $"')' or ',' after a {what} option");
        return runOutsideTransaction;
    }
}
