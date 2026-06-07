using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Operations;
using NSchema.Resolution;

namespace NSchema.Hosting;

/// <summary>
/// The hosted service that runs the configured migration operation once on startup and then stops the application.
/// </summary>
internal sealed class NSchemaHost(
    IOptions<OperationOptions> options,
    IHostApplicationLifetime lifetime,
    IServiceProvider services,
    IKeyedResolver<IMigrationReporter> reporter,
    OperationResult result
) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var operation = services.GetRequiredKeyedService<INSchemaOperation>(options.Value.Operation);
            await operation.Execute(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (options.Value.ExceptionBehavior == ExceptionBehavior.ReportAndThrow)
            {
                reporter.Current.ReportException(ex);
            }

            result.Exception = ex;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
