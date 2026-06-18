using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSchema.Operations;
using NSchema.Operations.Apply;
using NSchema.Operations.Destroy;
using NSchema.Operations.Drift;
using NSchema.Operations.ForceUnlock;
using NSchema.Operations.Import;
using NSchema.Operations.Plan;
using NSchema.Operations.PlanDestroy;
using NSchema.Operations.Refresh;
using NSchema.Operations.Show;
using NSchema.Operations.Validate;
using NSchema.Resolution;

namespace NSchema;

/// <summary>
/// The main entry point for an NSchema application.
/// </summary>
public sealed class NSchemaApplication : IDisposable
{
    private bool _hasRun;
    private readonly IHost _host;
    private readonly ExceptionBehavior _behavior;

    internal NSchemaApplication(IHost host, ExceptionBehavior behavior)
    {
        _host = host;
        _behavior = behavior;
    }

    /// <summary>
    /// The application's service provider.
    /// </summary>
    public IServiceProvider Services => _host.Services;

    /// <summary>
    /// Computes and renders the plan without applying it to the target.
    /// </summary>
    /// <param name="arguments">The arguments controlling the plan.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Plan(PlanArguments arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Run(() => Resolve<IPlanOperation>().Execute(arguments, cancellationToken));
    }

    /// <summary>
    /// Computes and renders the teardown plan (the plan to drop the managed schema) without applying it.
    /// </summary>
    /// <param name="arguments">The arguments controlling the teardown plan.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task PlanDestroy(PlanDestroyArguments arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Run(() => Resolve<IPlanDestroyOperation>().Execute(arguments, cancellationToken));
    }

    /// <summary>
    /// Computes the plan and applies it to the target.
    /// </summary>
    /// <param name="arguments">The arguments controlling the apply.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Apply(ApplyArguments arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Run(() => Resolve<IApplyOperation>().Execute(arguments, cancellationToken));
    }

    /// <summary>
    /// Reads the live current schema and writes it to the state store, without planning or applying anything.
    /// </summary>
    /// <param name="arguments">The arguments controlling the refresh.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Refresh(RefreshArguments arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Run(() => Resolve<IRefreshOperation>().Execute(arguments, cancellationToken));
    }

    /// <summary>
    /// Reads the live current schema and writes it to the output files as desired-schema source files.
    /// </summary>
    /// <param name="arguments">The arguments controlling which schema to import.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Import(ImportArguments arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Run(() => Resolve<IImportOperation>().Execute(arguments, cancellationToken));
    }

    /// <summary>
    /// Loads the desired schema and validates it against the configured schema policies.
    /// </summary>
    /// <param name="arguments">The arguments controlling the validation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Validate(ValidateArguments arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Run(() => Resolve<IValidateOperation>().Execute(arguments, cancellationToken));
    }

    /// <summary>
    /// Reads the recorded state from the state store and renders it.
    /// </summary>
    /// <param name="arguments">The arguments controlling what is shown.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Show(ShowArguments arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Run(() => Resolve<IShowOperation>().Execute(arguments, cancellationToken));
    }

    /// <summary>
    /// Compares the recorded state against the live database and reports how the live database has drifted from it.
    /// </summary>
    /// <param name="arguments">The arguments controlling the drift check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Drift(DriftArguments arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Run(() => Resolve<IDriftOperation>().Execute(arguments, cancellationToken));
    }

    /// <summary>
    /// Drops the managed schema objects from the target.
    /// </summary>
    /// <param name="arguments">The arguments controlling the teardown.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task Destroy(DestroyArguments arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Run(() => Resolve<IDestroyOperation>().Execute(arguments, cancellationToken));
    }

    /// <summary>
    /// Forcibly removes the state lock, for recovering from a stale lock.
    /// </summary>
    /// <param name="arguments">The arguments controlling the force-unlock.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task ForceUnlock(ForceUnlockArguments arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Run(() => Resolve<IForceUnlockOperation>().Execute(arguments, cancellationToken));
    }

    private T Resolve<T>() where T : notnull => _host.Services.GetRequiredService<T>();

    private async Task Run(Func<Task> execute)
    {
        if (_hasRun)
        {
            throw new InvalidOperationException("The application can only be run once.");
        }
        _hasRun = true;

        try
        {
            await execute();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Surface the exception via the reporter (when configured to) before letting it propagate to the caller.
            if (_behavior == ExceptionBehavior.ReportAndThrow)
            {
                _host.Services.GetRequiredService<IOperationReporter>().ReportException(ex);
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
