namespace NSchema.Plugins;

/// <summary>
/// A plugin that supplies a state-store backend, configured by a <c>STATE</c> statement.
/// </summary>
public interface INSchemaStatePlugin : INSchemaPlugin
{
    /// <summary>
    /// Registers this backend onto the application being built.
    /// </summary>
    /// <param name="builder">The application builder to register the backend's services on.</param>
    /// <param name="config">The statement's translated settings.</param>
    Result Configure(NSchemaApplicationBuilder builder, PluginConfig config);
}
