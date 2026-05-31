using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSchema.Migration;

namespace NSchema.Hosting;

/// <summary>
/// The hosted service that runs the configured migration operation once on startup and then stops the application.
/// </summary>
internal sealed class NSchemaHost(IOptions<MigrationRunOptions> options, IHostApplicationLifetime lifetime, IServiceProvider services) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var operation = services.GetRequiredKeyedService<IMigrationOperation>(options.Value.Operation);
            await operation.Execute(cancellationToken);
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
