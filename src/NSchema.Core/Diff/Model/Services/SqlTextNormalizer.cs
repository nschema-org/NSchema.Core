using System.Text;
using NSchema.Model;

namespace NSchema.Diff.Model.Services;

/// <summary>
/// Decides whether two pieces of opaque SQL text — view bodies, routine argument lists and routine
/// definitions are <em>equivalent</em> for diffing, so the cosmetic differences a database introduces when
/// it stores and re-emits a definition do not surface as phantom drift.
/// </summary>
/// <remarks>
/// This handles only <strong>cosmetic</strong> differences — insignificant whitespace and a trailing
/// statement terminator — and does so <em>literal-aware</em>: text inside single-quoted strings and
/// double-quoted identifiers is preserved verbatim, so <c>'a  b'</c> is never conflated with <c>'a b'</c>.
/// Under-normalizing leaves a harmless phantom diff; over-normalizing would silently swallow a real change,
/// so the bias is deliberately conservative.
/// <para>
/// It intentionally does <strong>not</strong> normalize keyword casing or the semantic rewrites a database
/// performs (name qualification, <c>*</c> expansion, injected casts). Those require the database itself to
/// canonicalize the definition and are reconciled provider-side (by storing/importing the DB-reported form).
/// </para>
/// </remarks>
internal static class SqlTextNormalizer
{
    /// <summary>Returns <c>true</c> when two pieces of SQL text differ only cosmetically.</summary>
    public static bool AreEquivalent(SqlText current, SqlText desired) =>
        string.Equals(Normalize(current.Value), Normalize(desired.Value), StringComparison.Ordinal);

    private static string Normalize(string body)
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
}
