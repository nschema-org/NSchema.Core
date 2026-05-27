using Microsoft.Extensions.Hosting;

namespace NSchema.Hosting;

/// <summary>
/// The hosted service that runs the migration pipeline once on startup and then stops the application.
/// </summary>
/// <param name="lifetime">The application lifetime.</param>
/// <param name="pipeline">The migration pipeline to run.</param>
internal sealed class NSchemaHost(IHostApplicationLifetime lifetime, IMigrationPipeline pipeline) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await pipeline.Run(cancellationToken);
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
