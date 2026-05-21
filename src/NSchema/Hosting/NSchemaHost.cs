using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSchema.Migration;

namespace NSchema.Hosting;

/// <summary>
/// The hosted service that runs the pipeline.
/// </summary>
/// <param name="logger">The logger for the pipeline host.</param>
/// <param name="lifetime">The application lifetime.</param>
/// <param name="migrator">The schema migrator.</param>
/// <param name="sqlMigrator">The SQL migrator, used to generate SQL in dry-run mode.</param>
/// <param name="options">The migration options.</param>
internal class NSchemaHost(
    ILogger<NSchemaHost> logger,
    IOptions<MigrationOptions> options,
    IHostApplicationLifetime lifetime,
    ISchemaMigrator migrator,
    ISqlMigrator sqlMigrator
) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var schemaPlan = await migrator.Plan(cancellationToken);
            var sqlPlan = sqlMigrator.Plan(schemaPlan);

            if (options.Value.DryRun)
            {

                if (sqlPlan.IsEmpty)
                {
                    logger.LogInformation("Dry run: no changes detected.");
                }
                else
                {
                    logger.LogInformation("Dry run: {Count} statement(s) would be executed:\n\n{Script}",
                        sqlPlan.Statements.Count,
                        string.Join(";\n\n", sqlPlan) + ";");
                }
            }
            else
            {
                await sqlMigrator.Apply(sqlPlan, cancellationToken);
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
