using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Operations;
using NSchema.Resolution;
using HostOptions = NSchema.Hosting.HostOptions;

namespace NSchema;

/// <summary>
/// The main entry point for an NSchema application.
/// </summary>
public sealed class NSchemaApplication : IDisposable
{
    private bool _hasRun;
    private readonly IHost _host;

    internal NSchemaApplication(IHost host)
    {
        _host = host;
    }

    /// <summary>
    /// The application's service provider.
    /// </summary>
    public IServiceProvider Services => _host.Services;

    /// <summary>
    /// Runs the operation configured via <see cref="NSchemaApplicationBuilder.RunOperation"/> (defaults to <see cref="HostOperation.Plan"/>).
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task RunAsync(CancellationToken cancellationToken = default) =>
        RunOperation(_host.Services.GetRequiredService<IOptions<HostOptions>>().Value.Operation, cancellationToken);

    /// <summary>
    /// Computes and renders the plan without applying it to the target.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Plan(CancellationToken cancellationToken = default) => RunOperation(HostOperation.Plan, cancellationToken);

    /// <summary>
    /// Computes the plan and applies it to the target.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Apply(CancellationToken cancellationToken = default) => RunOperation(HostOperation.Apply, cancellationToken);

    /// <summary>
    /// Reads the live current schema and writes it to the state store, without planning or applying anything.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Refresh(CancellationToken cancellationToken = default) => RunOperation(HostOperation.Refresh, cancellationToken);

    /// <summary>
    /// Reads the live current schema and writes it to the configured import target as desired-schema source files.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Import(CancellationToken cancellationToken = default) => RunOperation(HostOperation.Import, cancellationToken);

    /// <summary>
    /// Loads the desired schema and validates it against the configured schema policies.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Validate(CancellationToken cancellationToken = default) => RunOperation(HostOperation.Validate, cancellationToken);

    /// <summary>
    /// Drops the managed schema objects from the target.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Destroy(CancellationToken cancellationToken = default) => RunOperation(HostOperation.Destroy, cancellationToken);

    private async Task RunOperation(HostOperation operation, CancellationToken cancellationToken)
    {
        if (_hasRun)
        {
            throw new InvalidOperationException("The application can only be run once.");
        }
        _hasRun = true;

        var op = _host.Services.GetRequiredKeyedService<IOperation>(operation);
        try
        {
            await op.Execute(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Surface the exception via the reporter (when configured to) before letting it propagate to the caller.
            if (_host.Services.GetRequiredService<IOptions<HostOptions>>().Value.ExceptionBehavior == ExceptionBehavior.ReportAndThrow)
            {
                _host.Services.GetRequiredService<IKeyedResolver<IOperationReporter>>().Current.ReportException(ex);
            }
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _host.Dispose();
    }

    /// <summary>
    /// Creates a new <see cref="NSchemaApplicationBuilder"/> with the specified command-line arguments.
    /// </summary>
    /// <param name="args">The command-line arguments to add to the builder's configuration.</param>
    /// <returns>A new application builder.</returns>
    public static NSchemaApplicationBuilder CreateBuilder(string[]? args = null) => new(new NSchemaApplicationOptions { Args = args });

    /// <summary>
    /// Creates a new <see cref="NSchemaApplicationBuilder"/> with the specified options.
    /// </summary>
    /// <param name="options">The options to configure the builder.</param>
    /// <returns>A new application builder.</returns>
    public static NSchemaApplicationBuilder CreateBuilder(NSchemaApplicationOptions options) => new(options);
}
