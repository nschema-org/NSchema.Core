using Microsoft.Extensions.DependencyInjection;
using NSchema.Scripts;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Adds a provider to the application that will be used to retrieve deployment scripts to run during migration.
    /// </summary>
    /// <param name="provider">The provider to add.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddScripts(IScriptProvider provider)
    {
        Services.AddSingleton(provider);
        return this;
    }

    /// <summary>
    /// Adds a provider to the application that will be used to retrieve deployment scripts to run during migration.
    /// </summary>
    /// <typeparam name="TProvider">The type of the provider to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddScripts<TProvider>() where TProvider : class, IScriptProvider
    {
        Services.AddSingleton<IScriptProvider, TProvider>();
        return this;
    }
}
