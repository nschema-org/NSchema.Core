using System.Text;
using NSchema.Schema.Ddl.Model;

namespace NSchema.Schema.Ddl;

/// <summary>
/// Reformats NSchema DDL <em>gently</em>, normalizing layout.
/// </summary>
public sealed class DdlFormatter
{
    /// <summary>
    /// The singleton instance of <see cref="DdlFormatter"/> for convenience.
    /// </summary>
    public static readonly DdlFormatter Instance = new();

    private const string Indent = "  ";

    /// <summary>
    /// Reformats <paramref name="source"/> as canonical-layout NSchema DDL, preserving its content and comments.
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
        var leading = new List<string>();
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
                    leading.Add(FormatComment(token));
                }
                i++;
                continue;
            }

            // A statement runs to the first depth-zero ';' (parens balanced; strings/dollar-quotes are single tokens).
            var start = i;
            var depth = 0;
            var end = i;
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
                else if (kind == TokenKind.Semicolon && depth == 0)
                {
                    break;
                }
                end++;
            }

            var hasSemicolon = end < tokens.Count && tokens[end].Kind == TokenKind.Semicolon;
            items.Add(new Item { Leading = leading, Body = FormatStatement(tokens, source, start, end, hasSemicolon) });
            leading = [];
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
            foreach (var comment in item.Leading)
            {
                AppendIndentedLines(sb, comment, indent: "");
                sb.Append('\n');
            }
            if (item.Body is { } body)
            {
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

    // --- statements -----------------------------------------------------------

    private static string FormatStatement(List<Token> tokens, string source, int start, int end, bool hasSemicolon)
    {
        var first = FirstSignificant(tokens, start, end);
        if (first >= 0)
        {
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

        sb.Append('\n').Append(')');
        if (hasSemicolon)
        {
            sb.Append(';');
        }
        return sb.ToString();
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

    private static bool IsStatementKeyword(string text) =>
        text.Equals("CREATE", StringComparison.OrdinalIgnoreCase)
        || text.Equals("DROP", StringComparison.OrdinalIgnoreCase)
        || text.Equals("GRANT", StringComparison.OrdinalIgnoreCase)
        || text.Equals("PRE", StringComparison.OrdinalIgnoreCase)
        || text.Equals("POST", StringComparison.OrdinalIgnoreCase);

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
        public required List<string> Leading { get; init; }
        public required string? Body { get; init; }
        public string? Trailing { get; set; }
    }

    private sealed class Member
    {
        public required List<string> Leading { get; init; }
        public string? Content { get; init; }
        public string? Trailing { get; set; }
    }
}
