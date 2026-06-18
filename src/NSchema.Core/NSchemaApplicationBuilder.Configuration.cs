using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Diff;
using NSchema.Diff.Policies;
using NSchema.Operations;
using NSchema.Schema;
using NSchema.Sql;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Configures the policy to apply when a destructive action is detected in the migration plan.
    /// </summary>
    public NSchemaApplicationBuilder WithDestructiveActionPolicy(DestructiveActionPolicy policy)
    {
        Services.Configure<DestructiveActionOptions>(o => o.Policy = policy);
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
        return UseDiffRenderer<TerraformDiffRenderer>();
    }

    /// <summary>
    /// Configures the diff output to use the given <see cref="IDiffRenderer"/>.
    /// </summary>
    public NSchemaApplicationBuilder UseDiffRenderer<TRenderer>() where TRenderer : class, IDiffRenderer
    {
        Services.Replace(ServiceDescriptor.Singleton<IDiffRenderer, TRenderer>());
        return this;
    }

    /// <summary>
    /// Configures the SQL preview output to use the given <see cref="ISqlPlanRenderer"/>.
    /// </summary>
    public NSchemaApplicationBuilder UseSqlPlanRenderer<TRenderer>() where TRenderer : class, ISqlPlanRenderer
    {
        Services.Replace(ServiceDescriptor.Singleton<ISqlPlanRenderer, TRenderer>());
        return this;
    }

    /// <summary>
    /// Configures the single-schema output (used by the show operation) to use the given <see cref="ISchemaRenderer"/>.
    /// </summary>
    public NSchemaApplicationBuilder UseSchemaRenderer<TRenderer>() where TRenderer : class, ISchemaRenderer
    {
        Services.Replace(ServiceDescriptor.Singleton<ISchemaRenderer, TRenderer>());
        return this;
    }

    /// <summary>
    /// Replaces the <see cref="IOperationReporter"/> the application reports through.
    /// </summary>
    public NSchemaApplicationBuilder UseReporter<T>() where T : class, IOperationReporter
    {
        Services.Replace(ServiceDescriptor.Singleton<IOperationReporter, T>());
        return this;
    }

    /// <summary>
    /// Replaces the <see cref="IOperationReporter"/> the application reports through with a specific instance.
    /// </summary>
    public NSchemaApplicationBuilder UseReporter(IOperationReporter reporter)
    {
        ArgumentNullException.ThrowIfNull(reporter);
        Services.Replace(ServiceDescriptor.Singleton(reporter));
        return this;
    }

    /// <summary>
    /// Sets the <see cref="ISqlGenerator"/> the application generates SQL with, replacing any previously set one.
    /// Typically called by a database-provider extension. With none set, plans are reported without a SQL preview.
    /// </summary>
    public NSchemaApplicationBuilder UseSqlGenerator<T>() where T : class, ISqlGenerator
    {
        Services.Replace(ServiceDescriptor.Singleton<ISqlGenerator, T>());
        return this;
    }
}
