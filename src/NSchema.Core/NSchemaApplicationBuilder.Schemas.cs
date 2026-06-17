using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Schema;
using NSchema.Schema.Ddl;

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
    /// Adds schemas from the given SQL files.
    /// </summary>
    /// <param name="globPattern">A glob pattern, e.g. <c>schemas/**/*.sql</c>, or a single file path.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSqlSchemas(string globPattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(globPattern);
        return AddSchema(_ => new DdlSchemaProvider(globPattern));
    }

    /// <summary>
    /// Registers the <see cref="ISchemaProvider"/> that reads the live database schema (the online source).
    /// </summary>
    public NSchemaApplicationBuilder UseCurrentSchema<T>() where T : class, ISchemaProvider
    {
        Services.Replace(ServiceDescriptor.KeyedSingleton<ISchemaProvider, T>(NSchemaKeys.OnlineSchemaProvider));
        return this;
    }
}
