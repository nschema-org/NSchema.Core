namespace NSchema.Plugins;

/// <summary>
/// A plugin that supplies a state-store backend.
/// </summary>
public interface INSchemaBackendPlugin : INSchemaPlugin
{
    /// <summary>
    /// Registers this backend onto the application being built.
    /// </summary>
    /// <param name="builder">The application builder to register the backend's services on.</param>
    /// <param name="settings">The statement's translated settings.</param>
    Result Configure(NSchemaApplicationBuilder builder, PluginSettings settings);
}
