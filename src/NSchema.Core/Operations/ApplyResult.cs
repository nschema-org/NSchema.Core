using NSchema.Plan.Domain.Models;

namespace NSchema.Operations;

/// <summary>
/// The result of applying a plan.
/// </summary>
/// <param name="AppliedPlan">The plan that was applied. An empty plan means the target already matched the desired schema.</param>
public sealed record ApplyResult(MigrationPlan AppliedPlan)
{
    /// <summary>
    /// Whether any SQL was executed (<see langword="false"/> when the plan was empty and nothing changed).
    /// </summary>
    public bool ChangesApplied => !AppliedPlan.IsEmpty;

    /// <summary>
    /// The number of SQL statements that were executed.
    /// </summary>
    public int StatementsExecuted => AppliedPlan.Statements.Count;
}
