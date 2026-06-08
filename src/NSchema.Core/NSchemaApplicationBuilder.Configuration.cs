using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Diff;
using NSchema.Migration;
using NSchema.Operations;
using NSchema.Sql;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Configures the policy to apply when a destructive action is detected in the migration plan.
    /// </summary>
    public NSchemaApplicationBuilder WithDestructiveActionPolicy(DestructiveActionPolicy policy)
    {
        Services.Configure<MigrationOptions>(o => o.DestructiveActionPolicy = policy);
        return this;
    }

    /// <summary>
    /// Configures the transaction mode to use when executing the migration plan.
    /// </summary>
    public NSchemaApplicationBuilder WithTransactionMode(TransactionMode mode)
    {
        Services.Configure<SqlOptions>(o => o.TransactionMode = mode);
        return this;
    }

    /// <summary>
    /// Configures the plan output to use a Terraform-style renderer.
    /// </summary>
    public NSchemaApplicationBuilder UseTerraformRenderer(Action<TerraformDiffRendererOptions> configure)
    {
        Services.Configure(configure);
        return UseRenderer<TerraformDiffRenderer>();
    }

    /// <summary>
    /// Configures the plan output to use the given renderer.
    /// </summary>
    public NSchemaApplicationBuilder UseRenderer<TRenderer>() where TRenderer : class, IDiffRenderer
    {
        Services.Replace(ServiceDescriptor.Singleton<IDiffRenderer, TRenderer>());
        return this;
    }

    /// <summary>
    /// Registers an <see cref="IOperationReporter"/> for a new output format.
    /// </summary>
    public NSchemaApplicationBuilder AddReporter<T>(string format) where T : class, IOperationReporter
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        Services.Replace(ServiceDescriptor.KeyedSingleton<IOperationReporter, T>(format));
        return this;
    }

    /// <summary>
    /// Registers an <see cref="IOperationReporter"/> instance for a new output format.
    /// </summary>
    public NSchemaApplicationBuilder AddReporter(string format, IOperationReporter reporter)
    {
        ArgumentNullException.ThrowIfNull(reporter);
        Services.Replace(ServiceDescriptor.KeyedSingleton(format, reporter));
        return this;
    }

}
