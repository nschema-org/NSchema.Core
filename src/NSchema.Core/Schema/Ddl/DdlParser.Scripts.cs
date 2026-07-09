using NSchema.Schema.Ddl.Model;
using NSchema.Schema.Model.Migrations;
using NSchema.Schema.Model.Scripts;

namespace NSchema.Schema.Ddl;

internal sealed partial class DdlParser
{
    /// <summary>
    /// Parses a SCRIPT statement.
    /// </summary>
    private void ParseScript(List<Script> scripts, List<DataMigration> migrations)
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

        if (_current.IsKeyword("PRE") || _current.IsKeyword("POST"))
        {
            var type = _current.IsKeyword("PRE") ? ScriptType.PreDeployment : ScriptType.PostDeployment;
            Advance(); // PRE | POST
            ExpectKeyword("DEPLOYMENT");
            var (runOutsideTransaction, body) = ParseScriptTail("script");
            scripts.Add(new Script(name, body, type) { RunOutsideTransaction = runOutsideTransaction, RunCondition = condition });
            return;
        }

        if (!_current.IsKeyword("ADD") && !_current.IsKeyword("ALTER"))
        {
            throw Error("Expected a script event: 'PRE DEPLOYMENT', 'POST DEPLOYMENT', 'ADD COLUMN', 'ALTER COLUMN TYPE' or 'ADD CONSTRAINT'.");
        }

        var trigger = ParseMigrationTrigger();
        var (schema, table, member) = ParseMemberPath();
        var (outside, sql) = ParseScriptTail("script");
        migrations.Add(new DataMigration(name, trigger, schema, table, member, sql)
        {
            RunOutsideTransaction = outside,
            RunCondition = condition,
        });
    }

    /// <summary>
    /// Parses a deployment-script statement in the deprecated pre-SCRIPT form:
    /// <c>PRE|POST DEPLOYMENT '&lt;name&gt;' [( option = value, … )] AS $$ … $$;</c>.
    /// </summary>
    private Script ParseDeploymentScript(ScriptType type)
    {
        var start = _current.Position;
        var keyword = type == ScriptType.PreDeployment ? "PRE" : "POST";
        Advance(); // PRE | POST
        ExpectKeyword("DEPLOYMENT");
        var name = Expect(TokenKind.String, "a quoted script name").Text;
        var (runOutsideTransaction, body) = ParseScriptTail("deployment script");

        Warn($"'{keyword} DEPLOYMENT' is deprecated and will be removed in NSchema 5.0; " +
             $"declare the script as SCRIPT '{name}' RUN ON {keyword} DEPLOYMENT … instead", start);

        return new Script(name, body, type) { RunOutsideTransaction = runOutsideTransaction };
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
                var key = ExpectIdentifier($"a {what} option name");
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
