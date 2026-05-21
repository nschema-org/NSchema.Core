using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSchema.Migration;

namespace NSchema.Hosting;

/// <summary>
/// The hosted service that runs the pipeline.
/// </summary>
/// <param name="logger">The logger for the pipeline host.</param>
/// <param name="lifetime">The application lifetime.</param>
/// <param name="runner">The service that will be used to run the pipeline.</param>
/// <param name="migrator">The migrator, used to generate SQL in dry-run mode.</param>
/// <param name="configuration">The host configuration, used to detect --dry-run.</param>
internal class NSchemaHost(
    ILogger<NSchemaHost> logger,
    IHostApplicationLifetime lifetime,
    INSchemaRunner runner,
    ISchemaMigrator migrator,
    IConfiguration configuration
) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            bool isDryRun = configuration["dry-run"] is not null;

            if (isDryRun)
            {
                var schemaPlan = await runner.Plan(cancellationToken);
                var statementPlan = migrator.Plan(schemaPlan);

                if (statementPlan.IsEmpty)
                {
                    logger.LogInformation("Dry run: no changes detected.");
                }
                else
                {
                    logger.LogInformation("Dry run: {Count} statement(s) would be executed:\n\n{Script}",
                        statementPlan.Statements.Count,
                        string.Join(";\n\n", statementPlan) + ";");
                }
            }
            else
            {
                await runner.Apply(cancellationToken);
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
