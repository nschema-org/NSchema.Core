using NSchema.Model;
using NSchema.Plan.Backends;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Scripts;

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
