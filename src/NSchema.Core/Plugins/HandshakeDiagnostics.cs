namespace NSchema.Plugins;

/// <summary>
/// The diagnostics minted by the plugin handshake.
/// </summary>
internal static class HandshakeDiagnostics
{
    private const string Source = "plugins";

    /// <summary>
    /// An assembly loaded as a plugin that references no NSchema.Core at all.
    /// </summary>
    public static Diagnostic DoesNotReferenceCore(string plugin) => Diagnostic.Error(Source,
        $"Assembly '{plugin}' does not reference NSchema.Core, so it cannot be an NSchema plugin.");

    /// <summary>
    /// A plugin built against a different NSchema.Core major version.
    /// </summary>
    public static Diagnostic MajorMismatch(string plugin, Version referenced, Version host) => Diagnostic.Error(Source,
        $"Plugin '{plugin}' targets NSchema.Core v{referenced.Major}, but this engine hosts NSchema.Core v{host.Major}. A plugin must share the engine's major version; update the plugin pin or the engine.");

    /// <summary>
    /// A plugin built against a newer NSchema.Core minor version than the engine hosts.
    /// </summary>
    public static Diagnostic EngineOlderThanPlugin(string plugin, Version referenced, Version host) => Diagnostic.Error(Source,
        $"Plugin '{plugin}' targets NSchema.Core {referenced.ToString(2)}, but this engine hosts NSchema.Core {host.ToString(2)}. Update the engine, or pin an older plugin version.");
}
