using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSchema.Hosting;
using NSchema.Hosting.Operations;
using NSchema.Migration;
using NSchema.Migration.Sql;
using NSchema.Policies;
using NSchema.State;

namespace NSchema;

/// <summary>
/// A builder for configuring and creating an <see cref="NSchemaApplication"/>.
/// </summary>
public partial class NSchemaApplicationBuilder : IHostApplicationBuilder
{
    private readonly HostApplicationBuilder _innerBuilder;

    internal NSchemaApplicationBuilder(NSchemaApplicationOptions options)
    {
        // When left empty, the content root usually defaults to the current working directory.
        // Since NSchema will usually be run from a project/repository directory, it won't be able to
        // find things like appsettings.json.
        var contentRoot = options.ContentRootPath ?? AppContext.BaseDirectory;

        _innerBuilder = new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = options.Args,
            ApplicationName = options.ApplicationName,
            EnvironmentName = options.EnvironmentName,
            ContentRootPath = contentRoot,
            Configuration = new ConfigurationManager(),
        });

        // Drop the default console logger so third-party libraries don't spam the terminal.
        _innerBuilder.Logging.ClearProviders();

        _innerBuilder.Services.AddOptions<MigrationOptions>();
        _innerBuilder.Services.AddOptions<MigrationRunOptions>();
        _innerBuilder.Services.AddOptions<SqlExecutorOptions>();
    }

    /// <inheritdoc />
    public IDictionary<object, object> Properties => ((IHostApplicationBuilder)_innerBuilder).Properties;

    /// <inheritdoc />
    public IConfigurationManager Configuration => _innerBuilder.Configuration;

    /// <inheritdoc />
    public IHostEnvironment Environment => _innerBuilder.Environment;

    /// <inheritdoc />
    public ILoggingBuilder Logging => _innerBuilder.Logging;

    /// <inheritdoc />
    public IMetricsBuilder Metrics => _innerBuilder.Metrics;

    /// <inheritdoc />
    public IServiceCollection Services => _innerBuilder.Services;

    /// <summary>
    /// Builds the <see cref="NSchemaApplication" />.
    /// </summary>
    /// <returns>The configured application.</returns>
    public NSchemaApplication Build()
    {
        // We add services here to give the user a chance to supply their own before building the application.
        ApplyServices(Services);

        var host = _innerBuilder.Build();
        return new NSchemaApplication(host);
    }

    /// <inheritdoc />
    public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
        where TContainerBuilder : notnull => _innerBuilder.ConfigureContainer(factory, configure);

    private static void ApplyServices(IServiceCollection services)
    {
        services.TryAddSingleton<ISchemaStateSerializer, DefaultSchemaStateSerializer>();
        services.TryAddSingleton<IMigrationReporter, DefaultMigrationReporter>();
        services.TryAddSingleton<IMigrationPlanRenderer, DefaultMigrationPlanRenderer>();
        services.TryAddSingleton<ISchemaComparer, DefaultSchemaComparer>();
        services.TryAddSingleton<ISchemaAggregator, DefaultSchemaAggregator>();
        services.TryAddSingleton<IMigrationPlanner, DefaultMigrationPlanner>();
        services.TryAddSingleton<ICurrentSchemaProvider, DefaultCurrentSchemaProvider>();
        services.TryAddSingleton<IDesiredSchemaProvider, DefaultDesiredSchemaProvider>();

        services.TryAddKeyedSingleton<IMigrationOperation, PlanOperation>(MigrationOperation.Plan);
        services.TryAddKeyedSingleton<IMigrationOperation, ApplyOperation>(MigrationOperation.Apply);
        services.TryAddKeyedSingleton<IMigrationOperation, RefreshOperation>(MigrationOperation.Refresh);

        // The SQL compiler and executor are only meaningful when a database provider has registered an
        // ISqlPlanner. An offline run (e.g. a PR preview that plans against a state store) registers none, so
        // we leave IMigrationCompiler unregistered and the pipeline reports the plan without a SQL preview.
        // A user who supplies their own compiler via UseMigrationCompiler<T>() already registered it above.
        if (services.Any(d => d.ServiceType == typeof(ISqlPlanner)))
        {
            services.TryAddSingleton<ISqlExecutor, DefaultSqlExecutor>();
            services.TryAddSingleton<IMigrationCompiler, SqlMigrationCompiler>();
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMigrationPlanTransformer, ActionOrderingTransformer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMigrationPolicy, DestructiveActionMigrationPolicy>());

        // This is the service responsible for running the migration.
        services.AddHostedService<NSchemaHost>();
    }
}
