using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSchema.Plan.PlanFile;
using NSchema.Schema;
using NSchema.State;
using NSchema.State.Storage;

namespace NSchema;

/// <summary>
/// The entry point for an NSchema application.
/// </summary>
public sealed class NSchemaApplication : IDisposable
{
    private readonly IHost _host;
    private readonly Lazy<INSchemaOperations> _operations;
    private readonly Lazy<IStateLockCoordinator> _locks;
    private readonly Lazy<ICurrentSchemaProvider> _currentSchema;
    private readonly Lazy<IProjectProvider> _project;
    private readonly Lazy<IPlanFileWriter> _planFile;
    private readonly Lazy<ISchemaStateManager> _state;

    internal NSchemaApplication(IHost host)
    {
        _host = host;
        _operations = new Lazy<INSchemaOperations>(() => _host.Services.GetRequiredService<INSchemaOperations>());
        _locks = new Lazy<IStateLockCoordinator>(() => _host.Services.GetRequiredService<IStateLockCoordinator>());
        _currentSchema = new Lazy<ICurrentSchemaProvider>(() => _host.Services.GetRequiredService<ICurrentSchemaProvider>());
        _project = new Lazy<IProjectProvider>(() => _host.Services.GetRequiredService<IProjectProvider>());
        _planFile = new Lazy<IPlanFileWriter>(() => _host.Services.GetRequiredService<IPlanFileWriter>());
        _state = new Lazy<ISchemaStateManager>(() => _host.Services.GetRequiredService<ISchemaStateManager>());
    }

    /// <summary>
    /// The application's service provider.
    /// </summary>
    public IServiceProvider Services => _host.Services;

    /// <summary>
    /// The workflow operations.
    /// </summary>
    public INSchemaOperations Operations => _operations.Value;

    /// <summary>
    /// Takes the state lock around an operation.
    /// </summary>
    public IStateLockCoordinator Locks => _locks.Value;

    /// <summary>
    /// Reads the current schema, the recorded (offline) state or the live (online) database.
    /// </summary>
    public ICurrentSchemaProvider CurrentSchema => _currentSchema.Value;

    /// <summary>
    /// Reads the desired project declared by the DDL.
    /// </summary>
    public IProjectProvider Project => _project.Value;

    /// <summary>
    /// Reads and writes saved plan files.
    /// </summary>
    public IPlanFileWriter PlanFile => _planFile.Value;

    /// <summary>
    /// Reads and writes the recorded state.
    /// </summary>
    public ISchemaStateManager State => _state.Value;

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
