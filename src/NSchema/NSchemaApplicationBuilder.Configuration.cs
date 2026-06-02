using Microsoft.Extensions.DependencyInjection;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Migration.Sql;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Configures the policy to apply when a destructive action is detected in the migration plan.
    /// </summary>
    /// <param name="policy">The policy to apply.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder WithDestructiveActionPolicy(DestructiveActionPolicy policy)
    {
        Services.Configure<MigrationOptions>(o => o.DestructiveActionPolicy = policy);
        return this;
    }

    /// <summary>
    /// Configures the transaction mode to use when executing the migration plan.
    /// </summary>
    /// <param name="mode">The transaction mode to use.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder WithTransactionMode(TransactionMode mode)
    {
        Services.Configure<SqlExecutorOptions>(o => o.TransactionMode = mode);
        return this;
    }

    /// <summary>
    /// Configures the operation the migration run performs.
    /// </summary>
    /// <param name="operation">The operation to perform.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder RunOperation(MigrationOperation operation)
    {
        Services.Configure<MigrationRunOptions>(o => o.Operation = operation);
        return this;
    }

    /// <summary>
    /// Configures how exceptions are surfaced.
    /// </summary>
    /// <param name="behavior">The exception behavior to apply.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder WithExceptionBehavior(ExceptionBehavior behavior)
    {
        Services.Configure<MigrationRunOptions>(o => o.ExceptionBehavior = behavior);
        return this;
    }

    /// <summary>
    /// Scopes the migration to a specific set of schema names.
    /// </summary>
    /// <param name="schemaNames">The schema names to include in the migration.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder ForSchemas(params string[] schemaNames)
    {
        Services.Configure<MigrationOptions>(o => o.SchemaNames = schemaNames);
        return this;
    }
}
