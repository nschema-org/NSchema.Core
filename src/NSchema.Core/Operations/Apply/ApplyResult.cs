using NSchema.Sql.Model;

namespace NSchema.Operations.Apply;

/// <summary>
/// The result of applying a plan.
/// </summary>
/// <param name="AppliedSql">The SQL plan that was applied. An empty plan means the target already matched the desired schema.</param>
public sealed record ApplyResult(SqlPlan AppliedSql)
{
    /// <summary>
    /// Whether any SQL was executed (<see langword="false"/> when the plan was empty and nothing changed).
    /// </summary>
    public bool ChangesApplied => !AppliedSql.IsEmpty;

    /// <summary>
    /// The number of SQL statements that were executed.
    /// </summary>
    public int StatementsExecuted => AppliedSql.Statements.Count;
}
