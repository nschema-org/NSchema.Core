using NSchema.Plan.Model;
using NSchema.Plan.Model.Scripts;
using NSchema.Sql;
using NSchema.Sql.Model;

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
        _ => [new SqlStatement($"-- {action.GetType().Name}")],
    };
}
