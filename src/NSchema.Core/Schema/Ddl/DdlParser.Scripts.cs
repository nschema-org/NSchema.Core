using NSchema.Schema.Ddl.Model;
using NSchema.Schema.Model.Scripts;

namespace NSchema.Schema.Ddl;

internal sealed partial class DdlParser
{
    /// <summary>
    /// Parses a deployment-script statement: <c>PRE|POST DEPLOYMENT '&lt;name&gt;' [( option = value, … )] AS $$ … $$;</c>.
    /// The body is opaque SQL (dollar-quoted, so it may contain its own <c>;</c>) run as-is around the migration —
    /// captured verbatim like a <c>CREATE VIEW … AS</c> body.
    /// </summary>
    private Script ParseDeploymentScript(ScriptType type)
    {
        Advance(); // PRE | POST
        ExpectKeyword("DEPLOYMENT");
        var name = Expect(TokenKind.String, "a quoted script name").Text;
        var runOutsideTransaction = ParseDeploymentScriptOptions();

        // The body is opaque (dollar-quoted) SQL, so — like a view body — it is captured by a raw lexer read rather
        // than tokenised. The 'AS' keyword is the anchor: we verify it without advancing onto the un-tokenisable '$',
        // leaving the lexer positioned right after it to read the body.
        if (!_current.IsKeyword("AS"))
        {
            throw Error("Expected 'AS' before the deployment script body.");
        }
        var body = _lexer.ReadDollarQuotedBody("a deployment script body").Trim();
        _current = _lexer.Next();
        Expect(TokenKind.Semicolon, "';' to end the deployment script");

        return new Script(name, body, type) { RunOutsideTransaction = runOutsideTransaction };
    }

    /// <summary>
    /// Parses the optional <c>( run_outside_transaction = true )</c> clause, returning the flag (default false).
    /// </summary>
    private bool ParseDeploymentScriptOptions()
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
                var key = ExpectIdentifier("a deployment script option name");
                Expect(TokenKind.Equals, "'=' after an option name");
                var value = ParseConfigValue();
                switch (key.ToLowerInvariant())
                {
                    case "run_outside_transaction":
                        runOutsideTransaction = value.AsBoolean();
                        break;
                    default:
                        throw new DdlSyntaxException($"Unknown deployment script option '{key}'. Expected 'run_outside_transaction'.", keyPosition);
                }
            }
            while (Match(TokenKind.Comma));
        }
        Expect(TokenKind.RightParen, "')' or ',' after a deployment script option");
        return runOutsideTransaction;
    }
}
