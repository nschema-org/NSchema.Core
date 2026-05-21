using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NSchema.Hosting;

/// <summary>
/// The hosted service that runs the pipeline.
/// </summary>
/// <param name="logger">The logger for the pipeline host.</param>
/// <param name="lifetime">The application lifetime.</param>
/// <param name="runner">The service that will be used to run the pipeline.</param>
internal class NSchemaHost(
    ILogger<NSchemaHost> logger,
    IHostApplicationLifetime lifetime,
    INSchemaRunner runner
) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await runner.Run(cancellationToken);
        }
        finally
        {
            // Exit the application gracefully now that the pipeline has run.
            logger.LogDebug("Requesting application stop...");
            lifetime.StopApplication();
        }
    }
}
