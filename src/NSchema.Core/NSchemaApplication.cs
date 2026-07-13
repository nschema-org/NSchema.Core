using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSchema.Current;
using NSchema.Current.Locks;
using NSchema.Current.Storage;
using NSchema.Operations;
using NSchema.Plan.PlanFile;
using NSchema.Project;

namespace NSchema;

/// <summary>
/// The entry point for an NSchema application.
/// </summary>
public sealed class NSchemaApplication : IDisposable
{
    private readonly IHost _host;
    private readonly Lazy<INSchemaOperations> _operations;
    private readonly Lazy<IStateLockManager> _locks;
    private readonly Lazy<ICurrentSchemaProvider> _currentSchema;
    private readonly Lazy<IProjectProvider> _project;
    private readonly Lazy<IPlanFileManager> _planFile;
    private readonly Lazy<ISchemaStateManager> _state;

    internal NSchemaApplication(IHost host)
    {
        _host = host;
        _operations = new Lazy<INSchemaOperations>(() => _host.Services.GetRequiredService<INSchemaOperations>());
        _locks = new Lazy<IStateLockManager>(() => _host.Services.GetRequiredService<IStateLockManager>());
        _currentSchema = new Lazy<ICurrentSchemaProvider>(() => _host.Services.GetRequiredService<ICurrentSchemaProvider>());
        _project = new Lazy<IProjectProvider>(() => _host.Services.GetRequiredService<IProjectProvider>());
        _planFile = new Lazy<IPlanFileManager>(() => _host.Services.GetRequiredService<IPlanFileManager>());
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
    public IStateLockManager Locks => _locks.Value;

    /// <summary>
    /// Reads the current schema, the recorded (offline) state or the live (online) database.
    /// </summary>
    public ICurrentSchemaProvider CurrentSchema => _currentSchema.Value;

    /// <summary>
    /// Reads the desired project declared by the DDL.
    /// </summary>
    public IProjectProvider ProjectDefinition => _project.Value;

    /// <summary>
    /// Reads and writes saved plan files.
    /// </summary>
    public IPlanFileManager PlanFile => _planFile.Value;

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
