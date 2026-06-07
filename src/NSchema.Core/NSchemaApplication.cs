using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration;

namespace NSchema;

/// <summary>
/// The main entry point for an NSchema application.
/// </summary>
public sealed class NSchemaApplication : IHost
{
    private bool _hasRun;
    private readonly IHost _host;

    internal NSchemaApplication(IHost host)
    {
        _host = host;
    }

    /// <inheritdoc />
    public IServiceProvider Services => _host.Services;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_hasRun)
        {
            throw new InvalidOperationException("The application can only be started once.");
        }
        _hasRun = true;
        return _host.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _host.StopAsync(cancellationToken);

        // The migration runs as a background service, so unhandled exceptions terminate the host but aren't surfaced.
        // We rethrow it here with its original stacktrace, so it isn't lost.
        if (_host.Services.GetRequiredService<OperationResult>().Exception is { } failure)
        {
            ExceptionDispatchInfo.Throw(failure);
        }
    }

    /// <summary>
    /// Computes and renders the plan without applying it to the target.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Plan(CancellationToken cancellationToken = default) => RunOperation(MigrationOperation.Plan, cancellationToken);

    /// <summary>
    /// Computes the plan and applies it to the target.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Apply(CancellationToken cancellationToken = default) => RunOperation(MigrationOperation.Apply, cancellationToken);

    /// <summary>
    /// Reads the live current schema and writes it to the state store, without planning or applying anything.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Refresh(CancellationToken cancellationToken = default) => RunOperation(MigrationOperation.Refresh, cancellationToken);

    /// <summary>
    /// Reads the live current schema and writes it to the configured import target as desired-schema source files.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Import(CancellationToken cancellationToken = default) => RunOperation(MigrationOperation.Import, cancellationToken);

    /// <summary>
    /// Loads the desired schema and validates it against the configured schema policies.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Validate(CancellationToken cancellationToken = default) => RunOperation(MigrationOperation.Validate, cancellationToken);

    /// <summary>
    /// Drops the managed schema objects from the target.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Destroy(CancellationToken cancellationToken = default) => RunOperation(MigrationOperation.Destroy, cancellationToken);

    private Task RunOperation(MigrationOperation operation, CancellationToken cancellationToken)
    {
        _host.Services.GetRequiredService<IOptions<OperationOptions>>().Value.Operation = operation;
        return this.RunAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
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
