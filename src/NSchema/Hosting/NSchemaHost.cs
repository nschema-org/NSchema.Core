using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSchema.Migration;

namespace NSchema.Hosting;

/// <summary>
/// The hosted service that runs the database migration.
/// </summary>
/// <param name="logger">The logger for the host.</param>
/// <param name="lifetime">The application lifetime.</param>
/// <param name="migrator">The schema migrator.</param>
/// <param name="sqlPlanner">The SQL planner, used to generate the SQL that will be executed.</param>
/// <param name="sqlExecutor">The SQL executor, used to run the generated SQL against the database.</param>
/// <param name="options">The migration options.</param>
internal class NSchemaHost(
    ILogger<NSchemaHost> logger,
    IOptions<MigrationOptions> options,
    IHostApplicationLifetime lifetime,
    IMigrationPlanProvider migrator,
    ISqlPlanner sqlPlanner,
    ISqlExecutor sqlExecutor
) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var schemaPlan = await migrator.ComputeMigrationPlan(cancellationToken);
            var sqlPlan = sqlPlanner.Plan(schemaPlan);

            if (options.Value.DryRun)
            {
                logger.LogInformation("Dry run enabled. No changes will be applied to the database.");

                if (sqlPlan.IsEmpty)
                {
                    logger.LogInformation("No changes detected.");
                }
                else
                {
                    logger.LogInformation("Changes detected. The following SQL would be executed to apply the migration:");
                    foreach(var statement in sqlPlan.Statements)
                    {
                        logger.LogInformation("Statement: {Sql}", statement.Sql);
                    }
                }
            }
            else
            {
                await sqlExecutor.Execute(sqlPlan, cancellationToken);
            }
        }
        finally
        {
            // Exit the application gracefully now that the pipeline has run.
            logger.LogDebug("Requesting application stop...");
            lifetime.StopApplication();
        }
    }
}
