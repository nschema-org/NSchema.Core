using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Schema;
using NSchema.Schema.Serialization;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Adds a provider to the application that will be used to retrieve the desired schema.
    /// </summary>
    public NSchemaApplicationBuilder AddSchema<T>() where T : class, ISchemaProvider
    {
        Services.AddSingleton<ISchemaProvider, T>();
        return this;
    }

    /// <summary>
    /// Adds a provider to the application that will be used to retrieve the desired schema.
    /// </summary>
    public NSchemaApplicationBuilder AddSchema<T>(Func<IServiceProvider, T> factory) where T : class, ISchemaProvider
    {
        Services.AddSingleton<ISchemaProvider, T>(factory);
        return this;
    }

    /// <summary>
    /// Registers the <see cref="ISchemaProvider"/> that reads the live database schema (the online source).
    /// </summary>
    public NSchemaApplicationBuilder UseCurrentSchema<T>() where T : class, ISchemaProvider
    {
        Services.Replace(ServiceDescriptor.KeyedSingleton<ISchemaProvider, T>(NSchemaKeys.OnlineSchemaProvider));
        return this;
    }

    /// <summary>
    /// Registers an <see cref="ISchemaSerializer"/> for a new format.
    /// Throws if <paramref name="format"/> is already registered; use <see cref="UseSchemaSerializer{T}"/> to replace an existing one.
    /// </summary>
    public NSchemaApplicationBuilder AddSchemaSerializer<T>(string format) where T : class, ISchemaSerializer
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        Services.TryAddKeyedSingleton<ISchemaSerializer, T>(format);
        return this;
    }

    /// <summary>
    /// Replaces the <see cref="ISchemaSerializer"/> registered for <paramref name="format"/>, or adds it if not yet registered.
    /// </summary>
    public NSchemaApplicationBuilder UseSchemaSerializer<T>(string format) where T : class, ISchemaSerializer
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        Services.Replace(ServiceDescriptor.KeyedSingleton<ISchemaSerializer, T>(format));
        return this;
    }
}
