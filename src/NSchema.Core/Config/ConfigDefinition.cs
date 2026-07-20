using NSchema.Plugins;

namespace NSchema.Config;

/// <summary>
/// A resolved project configuration.
/// </summary>
/// <param name="Plugins">The declared plugin dependencies.</param>
/// <param name="Engine">The engine-level configuration, or <see langword="null"/> when the configuration carries none.</param>
/// <param name="Database">The database configuration, or <see langword="null"/> when none is configured.</param>
/// <param name="State">The state-store configuration, or <see langword="null"/> when none is configured.</param>
public sealed record ConfigDefinition(
    IReadOnlyList<PluginDeclaration> Plugins,
    EngineConfig? Engine,
    PluginConfig? Database,
    PluginConfig? State
);
