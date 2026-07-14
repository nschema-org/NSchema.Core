namespace NSchema.Plugins;

/// <summary>
/// A plugin that supplies a database provider for one engine.
/// </summary>
public interface INSchemaProviderPlugin : INSchemaPlugin
{
    /// <summary>
    /// Registers this provider onto the application being built, interpreting the project's <c>PROVIDER</c> statement.
    /// </summary>
    /// <param name="builder">The application builder to register the provider's services on.</param>
    /// <param name="settings">The statement's translated settings.</param>
    Result Configure(NSchemaApplicationBuilder builder, PluginSettings settings);

    /// <summary>
    /// Builds the starter desired-schema DDL this provider contributes when a new project is scaffolded.
    /// </summary>
    string GetSampleSchema();
}
