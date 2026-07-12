using NSchema.Diff.Domain.Models;

namespace NSchema.Plan.Domain.Models;

/// <summary>
/// The complete executable plan.
/// </summary>
/// <param name="Diff">The complete diff: the schema changes and the scripts that need to be run.</param>
/// <param name="Statements">The ordered SQL statements that will actually be executed.</param>
public sealed record MigrationPlan(DatabaseDiff Diff, IReadOnlyList<SqlStatement> Statements)
{
    /// <summary>
    /// Gets a value indicating whether the plan contains no statements to execute.
    /// </summary>
    public bool IsEmpty => Statements.Count == 0;
}
