using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileSystemGlobbing;
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
    /// Adds schemas from the SQL files matching <paramref name="globPattern"/> under <paramref name="baseDirectory"/>.
    /// </summary>
    /// <param name="baseDirectory">The directory the glob is matched against.</param>
    /// <param name="globPattern">A glob pattern relative to <paramref name="baseDirectory"/>. Defaults to <c>**/*.sql</c> (every SQL file, recursively). A wildcard-free pattern names a single file.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSqlSchemas(string baseDirectory, string globPattern = "**/*.sql")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(globPattern);
        var matcher = new Matcher();
        matcher.AddInclude(globPattern);
        return AddSqlSchemas(baseDirectory, matcher);
    }

    /// <summary>
    /// Adds schemas from the SQL files the given <see cref="Matcher"/> selects under <paramref name="baseDirectory"/>.
    /// </summary>
    /// <param name="baseDirectory">The directory the matcher is run against.</param>
    /// <param name="matcher">A configured glob matcher (includes and optional excludes), matched relative to <paramref name="baseDirectory"/>.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSqlSchemas(string baseDirectory, Matcher matcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentNullException.ThrowIfNull(matcher);
        return AddSchema(_ => new DdlSchemaProvider(baseDirectory, matcher));
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
