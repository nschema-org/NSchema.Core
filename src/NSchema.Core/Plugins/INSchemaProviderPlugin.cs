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
    void Configure(NSchemaApplicationBuilder builder, ConfigBlock block);
}
