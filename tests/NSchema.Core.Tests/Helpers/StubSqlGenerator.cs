using NSchema.Plan.Model;
using NSchema.Plan.Model.Migrations;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Tests.Helpers;

/// <summary>
/// Deterministic stand-in for a generator: one SQL statement per migration action. A data migration is raw SQL
/// needing no dialect, so it passes through verbatim (with its transaction placement).
/// </summary>
internal sealed class StubSqlGenerator : ISqlGenerator
{
    public SqlPlan Generate(MigrationPlan plan)
        => new([.. plan.Actions.Select(a => a switch
        {
            ExecuteDataMigration migration => new SqlStatement(migration.Sql, migration.RunOutsideTransaction),
            _ => new SqlStatement($"-- {a.GetType().Name}"),
        })]);
}
