using NSchema.Model;
using NSchema.Model.Views;

namespace NSchema.Project.Nsql;

/// <summary>
/// Extracts the objects a view reads from its (opaque) body, by scanning for the targets of <c>FROM</c> and
/// <c>JOIN</c> clauses. The scan is deliberately shallow — NSchema does not parse SQL — but it walks the whole
/// body, so references inside sub-queries are found too, and it subtracts names introduced by a <c>WITH</c>
/// common-table expression (those are local, not real objects).
/// </summary>
/// <remarks>
/// The extracted set only matters where a reference names an object that is <em>also</em> part of the same plan:
/// the planner forms an ordering edge only between two objects it is creating (or dropping) together. A spurious
/// reference (an alias, a function, a CTE the scan missed) therefore costs nothing — it matches no planned object
/// and produces no edge. The failure mode that matters is missing a genuine dependency, so the scan errs towards
/// over-collecting.
/// </remarks>
internal static class ViewDependencyExtractor
{
    // Keywords that end a table reference in a FROM list, so the scan stops treating following words as table
    // names (e.g. an alias is fine to over-collect, but these are never tables).
    private static readonly HashSet<string> _stops = new(StringComparer.OrdinalIgnoreCase)
    {
        "WHERE", "GROUP", "ORDER", "HAVING", "LIMIT", "OFFSET", "UNION", "INTERSECT", "EXCEPT",
        "ON", "USING", "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "OUTER", "CROSS", "NATURAL",
        "WINDOW", "FETCH", "FOR", "AS",
    };

    /// <summary>
    /// Extracts the dependencies of a view from its body.
    /// </summary>
    /// <param name="body">The view's defining query (the text after <c>AS</c>).</param>
    /// <param name="defaultSchema">The schema an unqualified reference is resolved against (the view's own schema).</param>
    public static List<ViewDependency> Extract(string body, SqlIdentifier defaultSchema)
    {
        var tokens = Tokenize(body);
        var ctes = CollectCteNames(tokens);

        var result = new List<ViewDependency>();
        var seen = new HashSet<(SqlIdentifier, SqlIdentifier)>();

        // The outer scan visits every token, so FROM/JOIN clauses are found at any nesting depth (in sub-queries,
        // WHERE/SELECT-list scalar sub-queries, CTE bodies). The per-clause readers below use a *local* cursor and
        // never advance this loop, so descending into a sub-query to handle a comma list can't hide the inner
        // clauses from this scan.
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind != TokenType.Word)
            {
                continue;
            }

            if (Is(token, "FROM"))
            {
                ReadFromList(tokens, i + 1, defaultSchema, ctes, seen, result);
            }
            else if (Is(token, "JOIN"))
            {
                var cursor = i + 1;
                TryReadReference(tokens, ref cursor, defaultSchema, ctes, seen, result);
            }
        }

        return result;
    }

    /// <summary>Reads the comma-separated table references of a FROM clause, starting at <paramref name="j"/>.</summary>
    private static void ReadFromList(
        IReadOnlyList<Token> tokens,
        int j,
        SqlIdentifier defaultSchema,
        HashSet<string> ctes,
        HashSet<(SqlIdentifier, SqlIdentifier)> seen,
        List<ViewDependency> result)
    {
        while (j < tokens.Count)
        {
            if (!TryReadReference(tokens, ref j, defaultSchema, ctes, seen, result))
            {
                break;
            }

            SkipAlias(tokens, ref j);
            if (j < tokens.Count && tokens[j].Kind == TokenType.Comma)
            {
                j++;
                continue;
            }
            break;
        }
    }

    /// <summary>
    /// Reads a single table reference at <paramref name="j"/>: a (optionally schema-qualified) name, or a
    /// parenthesised sub-query which is stepped over (its own FROM/JOIN clauses are found by the outer scan).
    /// Advances <paramref name="j"/> past what it read. Returns <see langword="false"/> when there is nothing to
    /// read (end of clause).
    /// </summary>
    private static bool TryReadReference(
        IReadOnlyList<Token> tokens,
        ref int j,
        SqlIdentifier defaultSchema,
        HashSet<string> ctes,
        HashSet<(SqlIdentifier, SqlIdentifier)> seen,
        List<ViewDependency> result
    )
    {
        if (j >= tokens.Count)
        {
            return false;
        }

        if (tokens[j].Kind == TokenType.LeftParen)
        {
            SkipBalancedParens(tokens, ref j);
            return true;
        }

        if (tokens[j].Kind != TokenType.Word || _stops.Contains(tokens[j].Text))
        {
            return false;
        }

        var first = tokens[j].Text;
        j++;

        if (j + 1 < tokens.Count && tokens[j].Kind == TokenType.Dot && tokens[j + 1].Kind == TokenType.Word)
        {
            var schema = first;
            var name = tokens[j + 1].Text;
            j += 2;
            Add(seen, result, new ViewDependency(new SqlIdentifier(schema), new SqlIdentifier(name)));
            return true;
        }

        // Unqualified: a CTE name is local and must not be treated as a real object.
        if (!ctes.Contains(first))
        {
            Add(seen, result, new ViewDependency(defaultSchema, new SqlIdentifier(first)));
        }
        return true;
    }

    /// <summary>
    /// Skips an optional table alias (<c>AS x</c> or a bare identifier) following a reference.
    /// </summary>
    private static void SkipAlias(IReadOnlyList<Token> tokens, ref int j)
    {
        if (j >= tokens.Count || tokens[j].Kind != TokenType.Word)
        {
            return;
        }

        if (Is(tokens[j], "AS"))
        {
            j++;
            if (j < tokens.Count && tokens[j].Kind == TokenType.Word)
            {
                j++;
            }
            return;
        }

        if (!_stops.Contains(tokens[j].Text))
        {
            j++; // a bare alias, e.g. "users u"
        }
    }

    /// <summary>
    /// Collects the names introduced by common-table expressions: any <c>name AS (</c> binds a local name that
    /// must not be mistaken for a real object.
    /// </summary>
    private static HashSet<string> CollectCteNames(IReadOnlyList<Token> tokens)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i + 2 < tokens.Count; i++)
        {
            if (tokens[i].Kind == TokenType.Word
                && Is(tokens[i + 1], "AS")
                && tokens[i + 2].Kind == TokenType.LeftParen)
            {
                names.Add(tokens[i].Text);
            }
        }
        return names;
    }

    private static void SkipBalancedParens(IReadOnlyList<Token> tokens, ref int i)
    {
        var depth = 0;
        for (; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == TokenType.LeftParen)
            {
                depth++;
            }
            else if (tokens[i].Kind == TokenType.RightParen)
            {
                depth--;
                if (depth == 0)
                {
                    i++;
                    return;
                }
            }
        }
    }

    private static void Add(HashSet<(SqlIdentifier, SqlIdentifier)> seen, List<ViewDependency> result, ViewDependency dependency)
    {
        if (seen.Add((dependency.Schema, dependency.Name)))
        {
            result.Add(dependency);
        }
    }

    private static bool Is(Token token, string keyword) =>
        token.Kind == TokenType.Word && string.Equals(token.Text, keyword, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A forgiving scanner over an opaque SQL body. Unlike <see cref="NsqlLexer"/> (which only accepts the DDL
    /// character set) this tolerates any SQL: it recognises identifiers, dots, parentheses and commas, swallows
    /// string literals and comments, and ignores every other character (operators, <c>*</c>, etc.).
    /// </summary>
    private static List<Token> Tokenize(string body)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < body.Length)
        {
            var c = body[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '-' && i + 1 < body.Length && body[i + 1] == '-')
            {
                while (i < body.Length && body[i] != '\n')
                {
                    i++;
                }
                continue;
            }

            if (c == '/' && i + 1 < body.Length && body[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < body.Length && !(body[i] == '*' && body[i + 1] == '/'))
                {
                    i++;
                }
                i += 2;
                continue;
            }

            if (c == '\'')
            {
                i++;
                while (i < body.Length)
                {
                    if (body[i] == '\'')
                    {
                        if (i + 1 < body.Length && body[i + 1] == '\'')
                        {
                            i += 2;
                            continue;
                        }
                        i++;
                        break;
                    }
                    i++;
                }
                continue;
            }

            if (c == '"')
            {
                // A double-quoted identifier.
                var start = ++i;
                while (i < body.Length && body[i] != '"')
                {
                    i++;
                }
                tokens.Add(new Token(TokenType.Word, body[start..i]));
                i++; // closing quote
                continue;
            }

            switch (c)
            {
                case '.': tokens.Add(new Token(TokenType.Dot, ".")); i++; continue;
                case '(': tokens.Add(new Token(TokenType.LeftParen, "(")); i++; continue;
                case ')': tokens.Add(new Token(TokenType.RightParen, ")")); i++; continue;
                case ',': tokens.Add(new Token(TokenType.Comma, ",")); i++; continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < body.Length && (char.IsLetterOrDigit(body[i]) || body[i] == '_'))
                {
                    i++;
                }
                tokens.Add(new Token(TokenType.Word, body[start..i]));
                continue;
            }

            i++; // any other character (operators, digits, *, etc.) is irrelevant to dependency extraction.
        }

        return tokens;
    }

    private enum TokenType
    {
        Word,
        Dot,
        LeftParen,
        RightParen,
        Comma,
    }

    private readonly record struct Token(TokenType Kind, string Text);
}
