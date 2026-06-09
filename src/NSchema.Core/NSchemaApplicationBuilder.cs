using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSchema.Diff;
using NSchema.Diff.Policies;
using NSchema.Operations;
using NSchema.Operations.Apply;
using NSchema.Operations.Confirmation;
using NSchema.Operations.Destroy;
using NSchema.Operations.Drift;
using NSchema.Operations.ForceUnlock;
using NSchema.Operations.Import;
using NSchema.Operations.Plan;
using NSchema.Operations.PlanDestroy;
using NSchema.Operations.Refresh;
using NSchema.Operations.Services;
using NSchema.Operations.Show;
using NSchema.Operations.Validate;
using NSchema.Plan;
using NSchema.Plan.PlanFile;
using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Schema.Policies;
using NSchema.Schema.Serialization;
using NSchema.Schema.Serialization.Ddl;
using NSchema.Schema.Serialization.Json;
using NSchema.Sql;
using NSchema.State;

namespace NSchema;

/// <summary>
/// A builder for configuring and creating an <see cref="NSchemaApplication"/>.
/// </summary>
public partial class NSchemaApplicationBuilder : IHostApplicationBuilder
{
    private readonly NSchemaApplicationOptions _options;
    private readonly HostApplicationBuilder _innerBuilder;

    internal NSchemaApplicationBuilder(NSchemaApplicationOptions options)
    {
        _options = options;
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

        // The user-supplied application options are the source of ambient run config (reporter, exception behavior).
        _innerBuilder.Services.AddSingleton(Options.Create(options));
        _innerBuilder.Services.AddOptions<DestructiveActionOptions>();
        _innerBuilder.Services.AddOptions<TerraformDiffRendererOptions>();

        // Register built-in keyed implementations (last-registration-wins).
        AddReporter<DefaultOperationReporter>(DefaultOperationReporter.ReporterName);
        AddSchemaSerializer<JsonSchemaSerializer>(JsonSchemaSerializer.FormatName);
        AddSchemaSerializer<DdlSchemaSerializer>(DdlSchemaSerializer.FormatName);

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
        return new NSchemaApplication(host, _options.ExceptionBehavior);
    }

    /// <inheritdoc />
    public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
        where TContainerBuilder : notnull => _innerBuilder.ConfigureContainer(factory, configure);

    private static void ApplyServices(IServiceCollection services)
    {
        // Diffing
        services.TryAddSingleton<ISchemaComparer, DefaultSchemaComparer>();
        services.TryAddSingleton<IDiffRenderer, TerraformDiffRenderer>();

        // Operations
        services.TryAddSingleton<IMigrationWorkflow, MigrationWorkflow>();
        services.TryAddSingleton<IOperationConfirmation, AutoApproveConfirmation>();
        services.TryAddSingleton<IKeyedResolver<IOperationReporter>>(sp => new DefaultKeyedResolver<IOperationReporter, NSchemaApplicationOptions>(sp, o => o.Reporter));
        services.TryAddSingleton<IPlanOperation, PlanOperation>();
        services.TryAddSingleton<IPlanDestroyOperation, PlanDestroyOperation>();
        services.TryAddSingleton<IApplyOperation, ApplyOperation>();
        services.TryAddSingleton<IRefreshOperation, RefreshOperation>();
        services.TryAddSingleton<IImportOperation, ImportOperation>();
        services.TryAddSingleton<IValidateOperation, ValidateOperation>();
        services.TryAddSingleton<IDestroyOperation, DestroyOperation>();
        services.TryAddSingleton<IShowOperation, ShowOperation>();
        services.TryAddSingleton<IDriftOperation, DriftOperation>();
        services.TryAddSingleton<IForceUnlockOperation, ForceUnlockOperation>();

        // Plan
        services.TryAddSingleton<IPlanLinearizer, DefaultPlanLinearizer>();
        services.TryAddSingleton<IMigrationPlanner, DefaultMigrationPlanner>();
        services.TryAddSingleton<IPlanFileWriter, PlanFileWriter>();

        // Schemas
        services.TryAddSingleton<ICurrentSchemaProvider, DefaultCurrentSchemaProvider>();
        services.TryAddSingleton<IDesiredSchemaProvider, DefaultDesiredSchemaProvider>();
        services.TryAddSingleton<IKeyedResolver<ISchemaSerializer>, DefaultKeyedResolver<ISchemaSerializer, object>>();
        services.TryAddSingleton<ISchemaRenderer, DefaultSchemaRenderer>();

        // SQL
        services.TryAddSingleton<ISqlPlanRenderer, DefaultSqlPlanRenderer>();
        services.TryAddSingleton<ISqlExecutor, DefaultSqlExecutor>();
        services.TryAddSingleton<IKeyedResolver<ISqlGenerator>>(sp => new DefaultKeyedResolver<ISqlGenerator, SqlOptions>(sp, o => o.Dialect));

        // State
        services.TryAddSingleton<ISchemaStateSerializer, DefaultSchemaStateSerializer>();
        services.TryAddSingleton<IStateLock, NoOpStateLock>();
    }
}
