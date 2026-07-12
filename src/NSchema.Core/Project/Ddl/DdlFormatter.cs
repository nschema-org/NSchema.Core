using System.Text;
using NSchema.Project.Ddl.Models;

namespace NSchema.Project.Ddl;

/// <summary>
/// Reformats NSchema DDL <em>gently</em>, normalizing layout.
/// </summary>
public sealed class DdlFormatter
{
    /// <summary>
    /// The singleton instance of <see cref="DdlFormatter"/> for convenience.
    /// </summary>
    public static readonly DdlFormatter Instance = new();

    private const int MaxBlankLines = 1;
    private const string Indent = "  ";

    /// <summary>
    /// Reformats <paramref name="source"/> as canonical-layout NSchema DDL.
    /// </summary>
    /// <param name="source">The DDL source text to format.</param>
    /// <returns>The formatted DDL, ending in a single newline (empty input yields an empty string).</returns>
    public string Format(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var tokens = Lex(source);
        var items = SplitTopLevel(tokens, source);
        return Render(items);
    }

    private static List<Token> Lex(string source)
    {
        var lexer = new DdlLexer(source, emitComments: true);
        var tokens = new List<Token>();
        while (true)
        {
            var token = lexer.Next();
            tokens.Add(token);
            if (token.Kind == TokenKind.EndOfFile)
            {
                return tokens;
            }
        }
    }

    // --- top level: statements separated by a blank line ----------------------

    private static List<Item> SplitTopLevel(List<Token> tokens, string source)
    {
        var items = new List<Item>();
        var leading = new List<Lead>();
        var lastLeadingEndLine = -1;
        var previousSemicolonLine = -1;
        var i = 0;
        while (tokens[i].Kind != TokenKind.EndOfFile)
        {
            var token = tokens[i];
            if (IsComment(token.Kind))
            {
                // A comment on the same line as the previous statement's ';' trails it; otherwise it leads the next.
                if (items.Count > 0 && items[^1] is { Body: not null, Trailing: null } previous
                    && token.Position.Line == previousSemicolonLine)
                {
                    previous.Trailing = FormatComment(token);
                }
                else
                {
                    // Record how many blank lines the author put before this comment; Render caps it per the options.
                    var blanksBefore = leading.Count > 0 ? BlankLinesBetween(lastLeadingEndLine, token.Position.Line) : 0;
                    leading.Add(new Lead(FormatComment(token), blanksBefore));
                    lastLeadingEndLine = EndLine(token);
                }
                i++;
                continue;
            }

            // A statement runs to the first depth-zero ';' (parens balanced; strings/dollar-quotes are single tokens).
            // A TEMPLATE statement contains whole inner statements, so it runs past their ';'s to the ';' after the
            // depth-zero END that closes its block.
            var start = i;
            var depth = 0;
            var end = i;
            var awaitingEnd = token.IsKeyword("TEMPLATE");
            while (end < tokens.Count)
            {
                var kind = tokens[end].Kind;
                if (kind == TokenKind.EndOfFile)
                {
                    break;
                }
                if (kind == TokenKind.LeftParen)
                {
                    depth++;
                }
                else if (kind == TokenKind.RightParen)
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                }
                else if (depth == 0 && awaitingEnd && tokens[end].IsKeyword("END"))
                {
                    awaitingEnd = false;
                }
                else if (kind == TokenKind.Semicolon && depth == 0 && !awaitingEnd)
                {
                    break;
                }
                end++;
            }

            var hasSemicolon = end < tokens.Count && tokens[end].Kind == TokenKind.Semicolon;
            var blanksBeforeBody = leading.Count > 0 ? BlankLinesBetween(lastLeadingEndLine, tokens[start].Position.Line) : 0;
            items.Add(new Item
            {
                Leading = leading,
                Body = FormatStatement(tokens, source, start, end, hasSemicolon),
                BlanksBeforeBody = blanksBeforeBody,
            });
            leading = [];
            lastLeadingEndLine = -1;
            if (hasSemicolon)
            {
                previousSemicolonLine = tokens[end].Position.Line;
                i = end + 1;
            }
            else
            {
                i = end;
            }
        }

        if (leading.Count > 0)
        {
            items.Add(new Item { Leading = leading, Body = null });
        }
        return items;
    }

    private static string Render(List<Item> items)
    {
        var sb = new StringBuilder();
        for (var k = 0; k < items.Count; k++)
        {
            var item = items[k];
            if (k > 0)
            {
                sb.Append("\n\n");
            }
            foreach (var lead in item.Leading)
            {
                AppendBlankLines(sb, lead.BlanksBefore);
                AppendIndentedLines(sb, lead.Text, indent: "");
                sb.Append('\n');
            }
            if (item.Body is { } body)
            {
                AppendBlankLines(sb, item.BlanksBeforeBody);
                sb.Append(body);
                if (item.Trailing is { } trailing)
                {
                    sb.Append(Indent).Append(trailing);
                }
            }
        }

        var text = sb.ToString().TrimEnd('\n');
        return text.Length == 0 ? string.Empty : text + "\n";
    }

    /// <summary>
    /// Emits up to <see cref="MaxBlankLines"/> blank lines, given the source had <paramref name="sourceBlankLines"/>.
    /// </summary>
    private static void AppendBlankLines(StringBuilder sb, int sourceBlankLines)
    {
        var count = Math.Min(sourceBlankLines, MaxBlankLines);
        for (var b = 0; b < count; b++)
        {
            sb.Append('\n');
        }
    }

    // --- statements -----------------------------------------------------------

    private static string FormatStatement(List<Token> tokens, string source, int start, int end, bool hasSemicolon)
    {
        var first = FirstSignificant(tokens, start, end);
        if (first >= 0)
        {
            if (tokens[first].IsKeyword("TEMPLATE") && RenderTemplate(tokens, source, start, end, hasSemicolon) is { } template)
            {
                return template;
            }

            var second = FirstSignificant(tokens, first + 1, end);
            var isCreateTable = tokens[first].IsKeyword("CREATE") && second >= 0 && tokens[second].IsKeyword("TABLE");
            var isConfigBlock = tokens[first].Kind == TokenKind.Identifier && !IsStatementKeyword(tokens[first].Text);
            if (isCreateTable || isConfigBlock)
            {
                var open = FindTopLevelOpenParen(tokens, start, end);
                if (open >= 0)
                {
                    var close = MatchParen(tokens, open, end);
                    if (close >= 0)
                    {
                        var members = SplitMembers(tokens, source, open, close);
                        if (members.Count > 0)
                        {
                            return RenderBroken(tokens, source, start, open, members, hasSemicolon);
                        }
                    }
                }
            }
        }

        return Verbatim(tokens, source, start, end, hasSemicolon);
    }

    /// <summary>
    /// Emits a <c>TEMPLATE … BEGIN … END;</c> block: the header verbatim, <c>BEGIN</c>/<c>END</c> on their own
    /// lines, and the inner statements formatted recursively (so a table body still breaks per member) and
    /// indented one level. Returns <see langword="null"/> when the statement has no well-formed BEGIN/END frame,
    /// or a comment sits between END and the ';' — the caller falls back to verbatim so no content is lost.
    /// </summary>
    private static string? RenderTemplate(List<Token> tokens, string source, int start, int end, bool hasSemicolon)
    {
        var begin = -1;
        for (var i = start; i < end; i++)
        {
            if (tokens[i].IsKeyword("BEGIN"))
            {
                begin = i;
                break;
            }
        }

        var last = LastSignificant(tokens, start, end);
        if (begin < 0 || last <= begin || !tokens[last].IsKeyword("END"))
        {
            return null;
        }
        for (var i = last + 1; i < end; i++)
        {
            if (IsComment(tokens[i].Kind))
            {
                return null;
            }
        }

        var header = source[tokens[start].Position.Offset..tokens[begin].Position.Offset].Trim();

        // A FOR TABLE template's body is a member list (like a table body), not statements; each renders on its
        // own line. Kind is read from the header tokens: a TABLE keyword before BEGIN.
        var isTableTemplate = false;
        for (var i = start; i < begin; i++)
        {
            if (tokens[i].IsKeyword("TABLE"))
            {
                isTableTemplate = true;
                break;
            }
        }

        var sb = new StringBuilder();
        sb.Append(header).Append("\nBEGIN");
        if (isTableTemplate)
        {
            AppendMembers(sb, SplitMembers(tokens, source, begin, last));
            sb.Append('\n');
        }
        else
        {
            var inner = tokens.GetRange(begin + 1, last - begin - 1);
            inner.Add(new Token(TokenKind.EndOfFile, string.Empty, tokens[last].Position));
            var body = Render(SplitTopLevel(inner, source)).TrimEnd('\n');

            sb.Append('\n');
            if (body.Length > 0)
            {
                string? openDollarTag = null;
                foreach (var line in body.Split('\n'))
                {
                    if (line.Length > 0)
                    {
                        if (openDollarTag is null)
                        {
                            sb.Append(Indent);
                        }
                        sb.Append(line);
                    }
                    openDollarTag = AdvanceDollarQuoteState(line, openDollarTag);
                    sb.Append('\n');
                }
            }
        }
        sb.Append("END");
        if (hasSemicolon)
        {
            sb.Append(';');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Advances the dollar-quote state across one line of rendered text: <paramref name="openTag"/> is the
    /// delimiter of the body the line starts inside (or <see langword="null"/> outside one), and the return value
    /// is the state the line ends in. A body only closes on its own tag, so <c>$$</c> inside a <c>$body$</c>
    /// block stays interior text.
    /// </summary>
    private static string? AdvanceDollarQuoteState(string line, string? openTag)
    {
        var i = 0;
        while (i < line.Length)
        {
            if (openTag is null)
            {
                var open = FindDollarTag(line, i);
                if (open is null)
                {
                    break;
                }
                openTag = open.Value.Tag;
                i = open.Value.End;
            }
            else
            {
                var close = line.IndexOf(openTag, i, StringComparison.Ordinal);
                if (close < 0)
                {
                    break;
                }
                i = close + openTag.Length;
                openTag = null;
            }
        }
        return openTag;
    }

    /// <summary>Finds the next <c>$tag$</c> delimiter at or after <paramref name="from"/>, returning it and the index just past it.</summary>
    private static (string Tag, int End)? FindDollarTag(string line, int from)
    {
        for (var i = line.IndexOf('$', from); i >= 0; i = line.IndexOf('$', i + 1))
        {
            var close = i + 1;
            while (close < line.Length && (char.IsLetterOrDigit(line[close]) || line[close] == '_'))
            {
                close++;
            }
            if (close < line.Length && line[close] == '$')
            {
                return (line[i..(close + 1)], close + 1);
            }
        }
        return null;
    }

    /// <summary>Emits a statement as its original text (leading whitespace trimmed), keeping the ';' where it sat.</summary>
    private static string Verbatim(List<Token> tokens, string source, int start, int end, bool hasSemicolon)
    {
        var from = tokens[start].Position.Offset;
        var to = hasSemicolon ? tokens[end].Position.Offset + 1 : tokens[end].Position.Offset;
        return source[from..to].Trim();
    }

    /// <summary>Emits a <c>CREATE TABLE</c> / config block with one member per line, two-space indented.</summary>
    private static string RenderBroken(List<Token> tokens, string source, int start, int open, List<Member> members, bool hasSemicolon)
    {
        var header = source[tokens[start].Position.Offset..tokens[open].Position.Offset].Trim();
        var sb = new StringBuilder();
        sb.Append(header).Append(" (");
        AppendMembers(sb, members);
        sb.Append('\n').Append(')');
        if (hasSemicolon)
        {
            sb.Append(';');
        }
        return sb.ToString();
    }

    /// <summary>Emits a member list one per line, two-space indented, comma-separating the content members.</summary>
    private static void AppendMembers(StringBuilder sb, List<Member> members)
    {
        var lastContent = -1;
        for (var k = 0; k < members.Count; k++)
        {
            if (members[k].Content is not null)
            {
                lastContent = k;
            }
        }

        for (var k = 0; k < members.Count; k++)
        {
            var member = members[k];
            foreach (var comment in member.Leading)
            {
                sb.Append('\n');
                AppendIndentedLines(sb, comment, Indent);
            }
            if (member.Content is { } content)
            {
                sb.Append('\n').Append(Indent).Append(content);
                if (k < lastContent)
                {
                    sb.Append(',');
                }
                if (member.Trailing is { } trailing)
                {
                    sb.Append(Indent).Append(trailing);
                }
            }
        }
    }

    // --- members of a broken list ---------------------------------------------

    private static List<Member> SplitMembers(List<Token> tokens, string source, int open, int close)
    {
        var members = new List<Member>();
        var previousSeparatorLine = tokens[open].Position.Line;
        var i = open + 1;
        while (i < close)
        {
            var leading = new List<string>();
            while (i < close && IsComment(tokens[i].Kind))
            {
                // A comment on the line of the previous member's ',' trails that member; otherwise it leads this one.
                if (members.Count > 0 && members[^1].Trailing is null && tokens[i].Position.Line == previousSeparatorLine)
                {
                    members[^1].Trailing = FormatComment(tokens[i]);
                }
                else
                {
                    leading.Add(FormatComment(tokens[i]));
                }
                i++;
            }
            if (i >= close)
            {
                if (leading.Count > 0)
                {
                    members.Add(new Member { Leading = leading });
                }
                break;
            }

            var contentStart = i;
            var contentEnd = contentStart; // exclusive index past the last token belonging to this member's value
            var depth = 0;
            var separator = -1;
            string? trailing = null;
            while (i < close)
            {
                var kind = tokens[i].Kind;

                if (depth == 0 && kind == TokenKind.Comma)
                {
                    separator = i;
                    break;
                }

                if (depth == 0 && IsComment(kind))
                {
                    // A line comment on the same line as the value so far trails this member and stays inline (a ','
                    // still lands before it). Any other comment is on its own line: stop here and leave it for the next
                    // iteration to collect as a leading comment, so a run of dangling comments each keep their own line
                    // instead of being flattened onto one.
                    if (trailing is null
                        && kind == TokenKind.LineComment
                        && contentEnd > contentStart
                        && tokens[i].Position.Line == tokens[contentEnd - 1].Position.Line)
                    {
                        trailing = FormatComment(tokens[i]);
                        i++;
                        continue;
                    }
                    break;
                }

                if (kind == TokenKind.LeftParen)
                {
                    depth++;
                }
                else if (kind == TokenKind.RightParen)
                {
                    depth--;
                }

                contentEnd = i + 1;
                i++;
            }

            members.Add(new Member
            {
                Leading = leading,
                Content = source[tokens[contentStart].Position.Offset..tokens[contentEnd].Position.Offset].Trim(),
                Trailing = trailing,
            });

            if (separator >= 0)
            {
                previousSeparatorLine = tokens[separator].Position.Line;
                i = separator + 1;
            }

            // With no separator, i already sits on the own-line comment or the close paren; leaving it there lets the
            // outer loop pick those trailing comments up (as a comment-only member) rather than discarding them.
        }

        return members;
    }

    // --- helpers --------------------------------------------------------------

    private static bool IsComment(TokenKind kind) =>
        kind is TokenKind.LineComment or TokenKind.BlockComment or TokenKind.DocComment;

    /// <summary>
    /// The 1-based line a comment token ends on: its start line plus the newlines its text spans. A line comment spans
    /// none; a merged doc-comment or block comment keeps its internal newlines, so counting them is exact.
    /// </summary>
    private static int EndLine(Token token) => token.Position.Line + token.Text.Count(c => c == '\n');

    /// <summary>The number of wholly-blank lines between a line that ends at <paramref name="endLine"/> and one that starts at <paramref name="startLine"/>.</summary>
    private static int BlankLinesBetween(int endLine, int startLine) => Math.Max(0, startLine - endLine - 1);

    private static bool IsStatementKeyword(string text) =>
        text.Equals("CREATE", StringComparison.OrdinalIgnoreCase)
        || text.Equals("DROP", StringComparison.OrdinalIgnoreCase)
        || text.Equals("GRANT", StringComparison.OrdinalIgnoreCase)
        || text.Equals("TEMPLATE", StringComparison.OrdinalIgnoreCase)
        || text.Equals("APPLY", StringComparison.OrdinalIgnoreCase)
        || text.Equals("SCRIPT", StringComparison.OrdinalIgnoreCase);

    private static string FormatComment(Token token) => token.Kind switch
    {
        // A doc-comment is re-emitted in the canonical '--- line' form (matching DdlWriter), one line per merged line.
        TokenKind.DocComment => string.Join('\n', token.Text.Split('\n').Select(line => "--- " + line)),
        // Source comments are kept verbatim (their '--' / delimiters intact).
        _ => token.Text,
    };

    private static int FirstSignificant(List<Token> tokens, int from, int end)
    {
        for (var i = from; i < end; i++)
        {
            if (!IsComment(tokens[i].Kind))
            {
                return i;
            }
        }
        return -1;
    }

    private static int LastSignificant(List<Token> tokens, int from, int end)
    {
        for (var i = end - 1; i >= from; i--)
        {
            if (!IsComment(tokens[i].Kind))
            {
                return i;
            }
        }
        return -1;
    }

    private static int FindTopLevelOpenParen(List<Token> tokens, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (tokens[i].Kind == TokenKind.LeftParen)
            {
                return i;
            }
        }
        return -1;
    }

    private static int MatchParen(List<Token> tokens, int open, int end)
    {
        var depth = 0;
        for (var i = open; i < end; i++)
        {
            if (tokens[i].Kind == TokenKind.LeftParen)
            {
                depth++;
            }
            else if (tokens[i].Kind == TokenKind.RightParen && --depth == 0)
            {
                return i;
            }
        }
        return -1;
    }

    private static void AppendIndentedLines(StringBuilder sb, string text, string indent)
    {
        var lines = text.Split('\n');
        for (var j = 0; j < lines.Length; j++)
        {
            if (j > 0)
            {
                sb.Append('\n');
            }
            sb.Append(indent).Append(lines[j]);
        }
    }

    private sealed class Item
    {
        public required List<Lead> Leading { get; init; }
        public required string? Body { get; init; }
        public int BlanksBeforeBody { get; init; }
        public string? Trailing { get; set; }
    }

    /// <summary>A leading comment line, with the number of blank lines the source had before it.</summary>
    private readonly record struct Lead(string Text, int BlanksBefore);

    private sealed class Member
    {
        public required List<string> Leading { get; init; }
        public string? Content { get; init; }
        public string? Trailing { get; set; }
    }
}
