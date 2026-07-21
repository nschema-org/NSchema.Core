namespace NSchema.Plugins.Model;

/// <summary>
/// A single recorded plugin pin from the lockfile: the package (<see cref="Source"/>) and the exact version the
/// project resolved and locked it to.
/// </summary>
/// <param name="Source">The plugin's NuGet package id.</param>
/// <param name="Version">The exact version the range resolved to.</param>
public sealed record LockedPlugin(PackageId Source, string Version);
