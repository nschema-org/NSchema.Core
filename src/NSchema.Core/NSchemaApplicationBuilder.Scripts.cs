using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using NSchema.Scripts;
using NSchema.Scripts.Model;

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

    /// <summary>
    /// Adds deployment scripts of the given <paramref name="type"/> from the SQL files matching <paramref name="globPattern"/> under <paramref name="baseDirectory"/>.
    /// </summary>
    /// <param name="type">When the scripts run relative to the main migration actions.</param>
    /// <param name="baseDirectory">The directory the glob is matched against.</param>
    /// <param name="globPattern">A glob pattern relative to <paramref name="baseDirectory"/>. A wildcard-free pattern names a single file.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSqlScripts(ScriptType type, string baseDirectory, string globPattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(globPattern);
        var matcher = new Matcher();
        matcher.AddInclude(globPattern);
        return AddSqlScripts(type, baseDirectory, matcher);
    }

    /// <summary>
    /// Adds deployment scripts of the given <paramref name="type"/> from the SQL files the given <see cref="Matcher"/> selects under <paramref name="baseDirectory"/>.
    /// </summary>
    /// <param name="type">When the scripts run relative to the main migration actions.</param>
    /// <param name="baseDirectory">The directory the matcher is run against.</param>
    /// <param name="matcher">A configured glob matcher (includes and optional excludes), matched relative to <paramref name="baseDirectory"/>.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSqlScripts(ScriptType type, string baseDirectory, Matcher matcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentNullException.ThrowIfNull(matcher);
        return AddScripts(new ScriptProvider(type, baseDirectory, matcher));
    }
}
