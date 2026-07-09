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
        var runOutsideTransaction = ParseRunOutsideTransactionOptions("deployment script");

        // The body is opaque (dollar-quoted) SQL lexed as a single DollarString token (so its own ';' is not a
        // terminator). The 'AS' keyword is the anchor; the body's delimiters are then stripped.
        if (!_current.IsKeyword("AS"))
        {
            throw Error("Expected 'AS' before the deployment script body.");
        }
        Advance(); // AS
        var dollar = Expect(TokenKind.DollarString, "a deployment script body as a dollar-quoted block ($$ … $$)");
        var body = StripDollarQuote(dollar.Text).Trim();
        Expect(TokenKind.Semicolon, "';' to end the deployment script");

        return new Script(name, body, type) { RunOutsideTransaction = runOutsideTransaction };
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
    /// Parses the optional <c>( run_outside_transaction = true )</c> clause shared by deployment scripts and
    /// migrations, returning the flag (default false). <paramref name="what"/> names the statement in errors.
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
