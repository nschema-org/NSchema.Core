using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSchema.Diff;
using NSchema.Diff.Policies;
using NSchema.Hosting;
using NSchema.Import;
using NSchema.Migration;
using NSchema.Operations;
using NSchema.Operations.Services;
using NSchema.Plan;
using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Schema.Policies;
using NSchema.Schema.Serialization;
using NSchema.Sql;
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

        _innerBuilder.Services.AddOptions<OperationOptions>();
        _innerBuilder.Services.AddOptions<MigrationOptions>();
        _innerBuilder.Services.AddOptions<ImportOptions>();
        _innerBuilder.Services.AddOptions<TerraformDiffRendererOptions>();

        // Register built-in keyed implementations (last-registration-wins).
        AddReporter<DefaultOperationReporter>(DefaultOperationReporter.ReporterName);
        AddSchemaSerializer<JsonSchemaDocumentSerializer>(JsonSchemaDocumentSerializer.FormatName);

        // Policies registered up front so users can remove them before Build().
        AddSchemaPolicy<StructuralIntegritySchemaPolicy>();
        AddSchemaPolicy<SchemaLintPolicy>();
        AddDiffPolicy<DestructiveActionDiffPolicy>();
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
        // Diffing
        services.TryAddSingleton<ISchemaComparer, DefaultSchemaComparer>();
        services.TryAddSingleton<IDiffRenderer, TerraformDiffRenderer>();

        // Import
        services.TryAddSingleton<IKeyedResolver<ISchemaImportTarget>>(sp => new DefaultKeyedResolver<ISchemaImportTarget, ImportOptions>(sp, o => o.Target));

        // Migration
        services.TryAddSingleton<IPlanLinearizer, DefaultPlanLinearizer>();
        services.TryAddSingleton<IMigrationPlanner, DefaultMigrationPlanner>();

        // Operations
        services.TryAddSingleton<OperationResult>();
        services.TryAddSingleton<IMigrationHelper, MigrationHelper>();
        services.TryAddSingleton<IOperationConfirmation, AutoApproveConfirmation>();
        services.TryAddSingleton<IKeyedResolver<IOperationReporter>>(sp => new DefaultKeyedResolver<IOperationReporter, OperationOptions>(sp, o => o.Reporter));
        services.TryAddKeyedSingleton<IOperation, PlanOperation>(HostOperation.Plan);
        services.TryAddKeyedSingleton<IOperation, ApplyOperation>(HostOperation.Apply);
        services.TryAddKeyedSingleton<IOperation, RefreshOperation>(HostOperation.Refresh);
        services.TryAddKeyedSingleton<IOperation, ImportOperation>(HostOperation.Import);
        services.TryAddKeyedSingleton<IOperation, ValidateOperation>(HostOperation.Validate);
        services.TryAddKeyedSingleton<IOperation, DestroyOperation>(HostOperation.Destroy);

        // Schemas
        services.TryAddSingleton<ICurrentSchemaProvider, DefaultCurrentSchemaProvider>();
        services.TryAddSingleton<IDesiredSchemaProvider, DefaultDesiredSchemaProvider>();
        services.TryAddSingleton<IKeyedResolver<ISchemaDocumentSerializer>, DefaultKeyedResolver<ISchemaDocumentSerializer, object>>();

        // SQL
        services.TryAddSingleton<ISqlPlanRenderer, DefaultSqlPlanRenderer>();
        services.TryAddSingleton<ISqlExecutor, DefaultSqlExecutor>();
        services.TryAddSingleton<IKeyedResolver<ISqlGenerator>>(sp => new DefaultKeyedResolver<ISqlGenerator, SqlOptions>(sp, o => o.Dialect));

        // State
        services.TryAddSingleton<ISchemaStateSerializer, DefaultSchemaStateSerializer>();

        // This is the service responsible for running the migration.
        services.AddHostedService<NSchemaHost>();
    }
}
