using NSchema.Plan.Backends;
using NSchema.Plan.Domain.Models;
using NSchema.Plan.Domain.Models.Scripts;
using NSchema.Project.Domain.Models;

namespace NSchema.Tests.Helpers;

/// <summary>
/// Deterministic stand-in for a dialect: one comment statement per schema action. (Script actions never reach a
/// dialect — the planner passes their raw SQL through itself.)
/// </summary>
internal sealed class StubSqlDialect : ISqlDialect
{
    public IReadOnlyList<SqlStatement> Generate(MigrationAction action) => action switch
    {
        ExecuteScript script => [script.Statement],
        _ => [new SqlStatement(new SqlText($"-- {action.GetType().Name}"))],
    };
}
