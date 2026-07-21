namespace NSchema.Configuration.Plugins;

/// <summary>
/// A declared plugin dependency.
/// </summary>
/// <param name="Label">The local name the plugin is referenced by.</param>
/// <param name="Package">The package source and version to declare.</param>
public sealed record PluginDeclaration(PluginLabel Label, PackageReference Package);
