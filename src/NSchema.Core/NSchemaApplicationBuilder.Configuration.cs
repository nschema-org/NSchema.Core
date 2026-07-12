using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Apply;
using NSchema.Plan.Policies;
using NSchema.Operations.Progress;
using NSchema.Plan.Backends;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Configures the policy to apply when a destructive action is detected in the migration plan.
    /// </summary>
    public NSchemaApplicationBuilder WithDestructiveActionPolicy(PolicyEnforcement policy)
    {
        Services.Configure<DestructiveActionOptions>(o => o.Policy = policy);
        return this;
    }

    /// <summary>
    /// Configures the policy to apply when the migration plan contains a change that can fail on existing data.
    /// </summary>
    public NSchemaApplicationBuilder WithDataHazardPolicy(PolicyEnforcement policy)
    {
        Services.Configure<DataHazardOptions>(o => o.Policy = policy);
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
    /// Sets the <see cref="ISqlDialect"/> the application renders SQL with, replacing any previously set one.
    /// Typically called by a database-provider extension.
    /// </summary>
    public NSchemaApplicationBuilder UseSqlDialect<T>() where T : class, ISqlDialect
    {
        Services.Replace(ServiceDescriptor.Singleton<ISqlDialect, T>());
        return this;
    }

    /// <summary>
    /// Configures the sink that receives an operation's transient progress narration.
    /// </summary>
    public NSchemaApplicationBuilder UseProgressReporter<TProgress>() where TProgress : class, IProgress<OperationProgress>
    {
        Services.Replace(ServiceDescriptor.Singleton<IProgress<OperationProgress>, TProgress>());
        return this;
    }

    /// <summary>
    /// Configures the sink that receives an operation's transient progress narration.
    /// </summary>
    public NSchemaApplicationBuilder UseProgressReporter(IProgress<OperationProgress> reporter)
    {
        Services.Replace(ServiceDescriptor.Singleton(reporter));
        return this;
    }

    /// <summary>
    /// Configures the sink that receives an operation's transient progress narration.
    /// </summary>
    public NSchemaApplicationBuilder UseProgressReporter(Func<IServiceProvider, IProgress<OperationProgress>> factory)
    {
        Services.Replace(ServiceDescriptor.Singleton(factory));
        return this;
    }
}
