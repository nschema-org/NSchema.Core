namespace NSchema.Project.Nsql.Tokens;

/// <summary>
/// The statement-opening keywords of the NSchema language — the one spelling the parser's dispatch and the
/// formatter's statement recognition must agree on. Grammar-internal keywords stay at their match sites,
/// where a typo fails the grammar tests immediately; this set is load-bearing across components.
/// </summary>
internal static class NsqlKeywords
{
    /// <summary>
    /// The keywords that open a project-grammar statement. The formatter treats anything else at statement
    /// position as a configuration statement (config files format through the same token stream).
    /// </summary>
    public static readonly IReadOnlySet<string> StatementOpeners = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CREATE", "DROP", "GRANT", "TEMPLATE", "APPLY", "SCRIPT", "RENAME", "PARTIAL",
    };

    /// <summary>
    /// The keywords that open a configuration-grammar statement.
    /// </summary>
    public static readonly IReadOnlySet<string> ConfigStatementOpeners = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "DATABASE", "STATE",
    };
}
