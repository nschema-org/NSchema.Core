using NSchema.Sql.Model;

namespace NSchema.Operations.Apply;

/// <summary>
/// The result of applying a plan: the SQL that was executed against the database.
/// </summary>
/// <param name="AppliedSql">The SQL plan that was executed, or <see langword="null"/> when the target already matched the desired schema and nothing was applied.</param>
public sealed record ApplyResult(SqlPlan? AppliedSql)
{
    /// <summary>
    /// Whether any SQL was executed (<see langword="false"/> when the target already matched and nothing changed).
    /// </summary>
    public bool ChangesApplied => AppliedSql is { IsEmpty: false };

    /// <summary>
    /// The number of SQL statements that were executed.
    /// </summary>
    public int StatementsExecuted => AppliedSql?.Statements.Count ?? 0;
}
