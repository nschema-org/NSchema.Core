using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.State;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Tracks whether the user has explicitly chosen a state lock (via <see cref="UseStateLock{T}()"/> and friends),
    /// so that configuring a state store never overrides an explicit choice, regardless of call order.
    /// </summary>
    private bool _stateLockConfigured;

    /// <summary>
    /// Registers the <see cref="ISchemaStateStore"/> used to persist and read schema snapshots.
    /// When the store type also implements <see cref="IStateLock"/>, it will be registered as the lock too.
    /// </summary>
    /// <typeparam name="T">The state store implementation to register.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseStateStore<T>() where T : class, ISchemaStateStore
    {
        Services.Replace(ServiceDescriptor.Singleton<ISchemaStateStore, T>());

        if (!_stateLockConfigured)
        {
            // Co-locate the lock with the store. If the backend also locks, resolve the one instance for both seams;
            // otherwise fall back to the no-op so this call fully (re)defines the co-located lock.
            Services.Replace(typeof(IStateLock).IsAssignableFrom(typeof(T))
                ? ServiceDescriptor.Singleton<IStateLock>(sp => (IStateLock)sp.GetRequiredService<ISchemaStateStore>())
                : ServiceDescriptor.Singleton<IStateLock, NoOpStateLock>());
        }

        return this;
    }

    /// <summary>
    /// Registers an <see cref="ISchemaStateStore"/> instance used to persist and read schema snapshots.
    /// When the instance also implements <see cref="IStateLock"/>, it is registered as the lock too.
    /// </summary>
    /// <param name="store">The state store instance.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseStateStore(ISchemaStateStore store)
    {
        Services.Replace(ServiceDescriptor.Singleton(store));

        if (!_stateLockConfigured)
        {
            Services.Replace(store is IStateLock stateLock
                ? ServiceDescriptor.Singleton(stateLock)
                : ServiceDescriptor.Singleton<IStateLock, NoOpStateLock>());
        }

        return this;
    }

    /// <summary>
    /// Registers a <see cref="FileSchemaStateStore"/> that persists schema snapshots to a local file, and a matching <see cref="FileStateLock"/> at <c>&lt;path&gt;.lock</c>.
    /// </summary>
    /// <param name="path">The absolute or relative path of the state file.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseFileStateStore(string path)
    {
        Services.Configure<FileSchemaStateStoreOptions>(o => o.Path = path);
        Services.Replace(ServiceDescriptor.Singleton<ISchemaStateStore, FileSchemaStateStore>());

        if (!_stateLockConfigured)
        {
            Services.Configure<FileStateLockOptions>(o => o.Path = DeriveLockPath(path));
            Services.Replace(ServiceDescriptor.Singleton<IStateLock, FileStateLock>());
        }

        return this;
    }

    /// <summary>
    /// Registers the <see cref="IStateLock"/> used to coordinate exclusive access to the state during the
    /// state-mutating operations (apply, destroy, refresh).
    /// </summary>
    /// <typeparam name="T">The state lock implementation to register.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseStateLock<T>() where T : class, IStateLock
    {
        _stateLockConfigured = true;
        Services.Replace(ServiceDescriptor.Singleton<IStateLock, T>());
        return this;
    }

    /// <summary>
    /// Registers an <see cref="IStateLock"/> instance used to coordinate exclusive access to the state.
    /// </summary>
    /// <param name="stateLock">The state lock instance.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseStateLock(IStateLock stateLock)
    {
        _stateLockConfigured = true;
        Services.Replace(ServiceDescriptor.Singleton(stateLock));
        return this;
    }

    /// <summary>
    /// Registers a <see cref="FileStateLock"/> that holds the lock as a file on the local filesystem.
    /// Intended for local development.
    /// </summary>
    /// <param name="path">The absolute or relative path of the lock file.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseFileStateLock(string path)
    {
        _stateLockConfigured = true;
        Services.Configure<FileStateLockOptions>(o => o.Path = path);
        Services.Replace(ServiceDescriptor.Singleton<IStateLock, FileStateLock>());
        return this;
    }

    /// <summary>
    /// Derives the lock-file path that pairs with a state file path (e.g. <c>schema.json</c> → <c>schema.json.lock</c>).
    /// </summary>
    private static string DeriveLockPath(string statePath) => statePath + ".lock";
}
