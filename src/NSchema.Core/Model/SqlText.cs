using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NSchema.Model;

/// <summary>
/// A verbatim fragment of SQL that NSchema carries but does not interpret.
/// </summary>
public sealed record SqlText : ValueObject<string>
{
    /// <summary>
    /// Wraps the verbatim SQL text.
    /// </summary>
    public SqlText(string value) : base(value)
    {
    }

    /// <summary>
    /// Whether this text and <paramref name="other"/> differ only cosmetically, so the differences a database
    /// introduces when it stores and re-emits a definition do not read as a change.
    /// </summary>
    /// <remarks>
    /// This handles only <strong>cosmetic</strong> differences — insignificant whitespace and a trailing
    /// statement terminator — and does so <em>literal-aware</em>: text inside single-quoted strings and
    /// double-quoted identifiers is preserved verbatim, so <c>'a  b'</c> is never conflated with <c>'a b'</c>.
    /// Under-normalizing leaves a harmless phantom difference; over-normalizing would silently swallow a real
    /// change, so the bias is deliberately conservative.
    /// <para>
    /// It intentionally does <strong>not</strong> normalize keyword casing or the semantic rewrites a database
    /// performs (name qualification, <c>*</c> expansion, injected casts). Those require the database itself to
    /// canonicalize the definition and are reconciled provider-side (by storing/importing the DB-reported form).
    /// </para>
    /// </remarks>
    public bool EquivalentTo(SqlText other) =>
        string.Equals(NormalizeCosmetics(Value), NormalizeCosmetics(other.Value), StringComparison.Ordinal);

    private static string NormalizeCosmetics(string body)
    {
        var sb = new StringBuilder(body.Length);
        var pendingSpace = false;

        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];

            if (c is '\'' or '"')
            {
                // A quoted run is significant verbatim. Emit any pending separator, then copy the whole
                // literal (including doubled-quote escapes like '' / "") without touching its contents.
                if (pendingSpace && sb.Length > 0)
                {
                    sb.Append(' ');
                }
                pendingSpace = false;
                i = CopyQuotedRun(body, i, sb);
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                pendingSpace = true; // collapse any run of whitespace to a single separator, emitted lazily
                continue;
            }

            if (pendingSpace && sb.Length > 0)
            {
                sb.Append(' ');
            }
            pendingSpace = false;
            sb.Append(c);
        }

        // A trailing terminator is cosmetic, as is any separator space we emitted just before it (e.g.
        // "... users ;"). Any interior ';' is left intact, and a body ending in a literal ends with a quote
        // (never ';'), so this can't reach inside a string.
        while (sb.Length > 0 && (sb[^1] == ';' || sb[^1] == ' '))
        {
            sb.Length--;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Copies the quoted run starting at <paramref name="start"/> (the opening quote) verbatim into
    /// <paramref name="sb"/>, honouring doubled-quote escapes, and returns the index of the closing quote.
    /// </summary>
    private static int CopyQuotedRun(string body, int start, StringBuilder sb)
    {
        var quote = body[start];
        sb.Append(quote);

        for (var i = start + 1; i < body.Length; i++)
        {
            var c = body[i];
            sb.Append(c);
            if (c != quote)
            {
                continue;
            }

            // A doubled quote is an escaped quote, not the close — consume the pair and stay inside.
            if (i + 1 < body.Length && body[i + 1] == quote)
            {
                sb.Append(quote);
                i++;
                continue;
            }

            return i; // closing quote
        }

        return body.Length - 1; // unterminated literal: copied to the end
    }

    /// <summary>
    /// Wraps the verbatim SQL text. One-way: text never converts silently back to a bare string.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator SqlText?(string? value) => value is null ? null : new SqlText(value);
}
