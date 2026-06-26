using NSchema.Configuration;

namespace NSchema.Plugins;

/// <summary>
/// A plugin that supplies a state-store backend.
/// </summary>
public interface INSchemaBackendPlugin : INSchemaPlugin
{
    /// <summary>
    /// Registers this backend onto the application being built, interpreting the project's <c>BACKEND</c> block.
    /// </summary>
    /// <param name="builder">The application builder to register the backend's services on.</param>
    /// <param name="block">The project's <c>BACKEND</c> configuration block.</param>
    void Configure(NSchemaApplicationBuilder builder, ConfigBlock block);
}
