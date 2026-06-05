using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.State;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Registers the <see cref="ISchemaStateStore"/> used to persist and read schema snapshots.
    /// </summary>
    /// <typeparam name="T">The state store implementation to register.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseStateStore<T>() where T : class, ISchemaStateStore
    {
        Services.RemoveAll<ISchemaStateStore>();
        Services.AddSingleton<ISchemaStateStore, T>();
        return this;
    }

    /// <summary>
    /// Registers an <see cref="ISchemaStateStore"/> instance used to persist and read schema snapshots.
    /// </summary>
    /// <param name="store">The state store instance.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseStateStore(ISchemaStateStore store)
    {
        Services.RemoveAll<ISchemaStateStore>();
        Services.AddSingleton(store);
        return this;
    }

    /// <summary>
    /// Registers a <see cref="FileSchemaStateStore"/> that persists schema snapshots to a local file.
    /// </summary>
    /// <param name="path">The absolute or relative path of the state file.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseFileStateStore(string path)
    {
        Services.RemoveAll<ISchemaStateStore>();
        Services.Configure<FileSchemaStateStoreOptions>(o => o.Path = path);
        Services.AddSingleton<ISchemaStateStore, FileSchemaStateStore>();
        return this;
    }
}
