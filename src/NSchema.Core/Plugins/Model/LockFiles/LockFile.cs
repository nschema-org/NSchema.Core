namespace NSchema.Plugins.Model.LockFiles;

/// <summary>
/// The plugin lockfile (<c>nschema.lock</c>): the exact versions the project has resolved and locked its
/// plugins to. Named for the file, and distinct from the state lock.
/// </summary>
/// <param name="Plugins">The locked plugins, in source order.</param>
public sealed record LockFile(IReadOnlyList<LockedPlugin> Plugins)
{
    /// <summary>
    /// An empty lockfile — nothing locked yet.
    /// </summary>
    public static LockFile Empty { get; } = new([]);

    /// <summary>
    /// The entry locking <paramref name="source"/>, or <see langword="null"/> when it is unlocked.
    /// </summary>
    public LockedPlugin? Find(PackageId source) => Plugins.FirstOrDefault(plugin => plugin.Source == source);
}
