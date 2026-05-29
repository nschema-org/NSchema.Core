using NSchema.Migration.Plan;

namespace NSchema.Migration.Sql;

/// <summary>
/// Default <see cref="IMigrationExecutor"/> for SQL targets. Compiles the migration plan into a
/// <see cref="SqlPlan"/> wrapped as an executable unit; reporting is left to the pipeline.
/// </summary>
internal sealed class SqlMigrationExecutor(
    ISqlPlanner sqlPlanner,
    ISqlExecutor sqlExecutor
) : IMigrationExecutor
{
    public Task<IMigrationExecution> Compile(MigrationPlan plan, CancellationToken cancellationToken = default)
    {
        var sqlPlan = sqlPlanner.Plan(plan);
        return Task.FromResult<IMigrationExecution>(new SqlMigrationExecution(sqlPlan, sqlExecutor));
    }
}
