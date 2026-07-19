namespace NSchema.Config;

/// <summary>
/// A declared plugin dependency: the package that supplies it and the version range to resolve within.
/// </summary>
/// <param name="Label">The local name the plugin is referenced by.</param>
/// <param name="Source">The package id.</param>
/// <param name="Version">The version range to resolve within; its rendered text feeds package resolution.</param>
public sealed record PluginDeclaration(string Label, string Source, VersionRange Version);
