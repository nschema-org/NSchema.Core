using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Migration;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Adds a provider to the application that will be used to retrieve the desired schema.
    /// </summary>
    /// <typeparam name="T">The type of the provider to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSchema<T>() where T : ISchemaProvider
    {
        Services.TryAddEnumerable(new ServiceDescriptor(typeof(ISchemaProvider), typeof(T), ServiceLifetime.Singleton));
        return this;
    }

    /// <summary>
    /// Registers the <see cref="ISchemaProvider"/> that reads the live database schema (the online source).
    /// </summary>
    /// <typeparam name="T">The type of the provider to register as the online current-state source.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseCurrentSchema<T>() where T : class, ISchemaProvider
    {
        Services.AddKeyedSingleton<ISchemaProvider, T>(NSchemaKeys.OnlineSchemaProvider);
        return this;
    }
}
