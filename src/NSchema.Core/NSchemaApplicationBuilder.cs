using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSchema.Diagnostics;
using NSchema.Diff;
using NSchema.Diff.Policies;
using NSchema.Operations;
using NSchema.Operations.Apply;
using NSchema.Operations.Doctor;
using NSchema.Operations.Drift;
using NSchema.Operations.Import;
using NSchema.Operations.Plan;
using NSchema.Operations.PlanDestroy;
using NSchema.Operations.Progress;
using NSchema.Operations.Refresh;
using NSchema.Operations.Services;
using NSchema.Operations.Validate;
using NSchema.Plan;
using NSchema.Plan.PlanFile;
using NSchema.Schema;
using NSchema.Schema.Policies;
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

        // Policies registered up front so users can remove them before Build().
        AddSchemaPolicy<StructuralIntegritySchemaPolicy>();
        AddSchemaPolicy<SchemaLintPolicy>();
        AddDiffPolicy<DestructiveActionDiffPolicy>();
        AddDiffPolicy<EnumValueRemovalDiffPolicy>();
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
        services.TryAddSingleton<ISchemaComparer, SchemaComparer>();
        services.TryAddSingleton<IDiffRenderer, TerraformDiffRenderer>();

        // Operations
        services.TryAddSingleton<IMigrationWorkflow, MigrationWorkflow>();
        services.TryAddSingleton<IProgress<OperationProgress>, NullOperationProgress>();
        services.TryAddSingleton<PlanComposer>();
        services.TryAddSingleton<IOperation<PlanArguments, Result<PlanResult>>, PlanOperation>();
        services.TryAddSingleton<IOperation<PlanDestroyArguments, Result<PlanResult>>, PlanDestroyOperation>();
        services.TryAddSingleton<IOperation<PlanResult, Result>, ApplyOperation>();
        services.TryAddSingleton<IOperation<RefreshArguments, Result>, RefreshOperation>();
        services.TryAddSingleton<IOperation<ValidateArguments, Result>, ValidateOperation>();
        services.TryAddSingleton<IOperation<DriftArguments, Result<DriftResult>>, DriftOperation>();
        services.TryAddSingleton<IOperation<ImportArguments, Result>, ImportOperation>();
        services.TryAddSingleton<IOperation<DoctorArguments, Result>, DoctorOperation>();
        services.TryAddSingleton<INSchemaOperations, NSchemaOperations>();

        // Plan
        services.TryAddSingleton<IPlanLinearizer, PlanLinearizer>();
        services.TryAddSingleton<IMigrationPlanner, MigrationPlanner>();
        services.TryAddSingleton<IPlanFileWriter, PlanFileWriter>();

        // Schemas
        services.TryAddSingleton<ICurrentSchemaProvider, CurrentSchemaProvider>();
        services.TryAddSingleton<IDesiredSchemaProvider, DesiredSchemaProvider>();
        services.TryAddSingleton<ISchemaRenderer, DefaultSchemaRenderer>();

        // SQL
        services.TryAddSingleton<ISqlPlanRenderer, DefaultSqlPlanRenderer>();
        services.TryAddSingleton<ISqlExecutor, SqlExecutor>();

        // State
        services.TryAddSingleton<ISchemaStateSerializer, SchemaStateSerializer>();
        services.TryAddSingleton<IStateLockCoordinator, StateLockCoordinator>();
    }
}
