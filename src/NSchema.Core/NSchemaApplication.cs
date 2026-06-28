using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSchema.State;

namespace NSchema;

/// <summary>
/// The entry point for an NSchema application.
/// </summary>
public sealed class NSchemaApplication : IDisposable
{
    private readonly IHost _host;
    private readonly Lazy<INSchemaOperations> _operations;
    private readonly Lazy<IStateLockCoordinator> _locks;

    internal NSchemaApplication(IHost host)
    {
        _host = host;
        _operations = new Lazy<INSchemaOperations>(() => _host.Services.GetRequiredService<INSchemaOperations>());
        _locks = new Lazy<IStateLockCoordinator>(() => _host.Services.GetRequiredService<IStateLockCoordinator>());
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
