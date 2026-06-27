using NSchema.Configuration;

namespace NSchema.Plugins;

/// <summary>
/// A plugin that supplies a database provider for one engine.
/// </summary>
public interface INSchemaProviderPlugin : INSchemaPlugin
{
    /// <summary>
    /// Registers this provider onto the application being built, interpreting the project's <c>PROVIDER</c> block.
    /// </summary>
    /// <param name="builder">The application builder to register the provider's services on.</param>
    /// <param name="block">The project's <c>PROVIDER</c> configuration block.</param>
    PluginConfigureResult Configure(NSchemaApplicationBuilder builder, ConfigBlock block);

    /// <summary>
    /// Builds the starter desired-schema DDL this provider contributes when a new project is scaffolded.
    /// </summary>
    string GetSampleSchema();
}
