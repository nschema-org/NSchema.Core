using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Diff;
using NSchema.Hosting;
using NSchema.Migration;
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
        Services.Configure<SqlExecutorOptions>(o => o.TransactionMode = mode);
        return this;
    }

    /// <summary>
    /// Configures the plan output to use a Terraform-style renderer.
    /// </summary>
    public NSchemaApplicationBuilder UseTerraformRenderer(Action<TerraformDiffRendererOptions> configure)
    {
        Services.Replace(ServiceDescriptor.Singleton<IDiffRenderer, TerraformDiffRenderer>());
        Services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Registers an <see cref="IMigrationReporter"/> for a new output format.
    /// Throws if <paramref name="format"/> is already registered; use <see cref="UseReporter{T}"/> to replace an existing one.
    /// </summary>
    public NSchemaApplicationBuilder AddReporter<T>(string format) where T : class, IMigrationReporter
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        Services.TryAddKeyedSingleton<IMigrationReporter, T>(format);
        return this;
    }

    /// <summary>
    /// Registers an <see cref="IMigrationReporter"/> instance for a new output format (key taken from <see cref="IMigrationReporter.Format"/>).
    /// Throws if the format is already registered; use <see cref="UseReporter"/> to replace an existing one.
    /// </summary>
    public NSchemaApplicationBuilder AddReporter(IMigrationReporter reporter)
    {
        ArgumentNullException.ThrowIfNull(reporter);
        Services.TryAddKeyedSingleton(reporter.Format, reporter);
        return this;
    }

    /// <summary>
    /// Replaces the <see cref="IMigrationReporter"/> registered for <paramref name="format"/>, or adds it if not yet registered.
    /// </summary>
    public NSchemaApplicationBuilder UseReporter<T>(string format) where T : class, IMigrationReporter
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        Services.Replace(ServiceDescriptor.KeyedSingleton<IMigrationReporter, T>(format));
        return this;
    }

    /// <summary>
    /// Replaces the <see cref="IMigrationReporter"/> registered for the instance's format, or adds it if not yet registered.
    /// </summary>
    public NSchemaApplicationBuilder UseReporter(IMigrationReporter reporter)
    {
        ArgumentNullException.ThrowIfNull(reporter);
        Services.Replace(ServiceDescriptor.KeyedSingleton(reporter.Format, reporter));
        return this;
    }

    /// <summary>
    /// Configures the operation the migration run performs.
    /// </summary>
    public NSchemaApplicationBuilder RunOperation(MigrationOperation operation)
    {
        Services.Configure<MigrationRunOptions>(o => o.Operation = operation);
        return this;
    }

    /// <summary>
    /// Configures the output format used to render run output.
    /// </summary>
    public NSchemaApplicationBuilder WithOutputFormat(string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        Services.Configure<MigrationRunOptions>(o => o.OutputFormat = format);
        return this;
    }

    /// <summary>
    /// Selects the SQL dialect to generate, when more than one <see cref="ISqlGenerator"/> is registered.
    /// </summary>
    public NSchemaApplicationBuilder WithDialect(string dialect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dialect);
        Services.Configure<MigrationRunOptions>(o => o.Dialect = dialect);
        return this;
    }

    /// <summary>
    /// Configures how exceptions are surfaced.
    /// </summary>
    public NSchemaApplicationBuilder WithExceptionBehavior(ExceptionBehavior behavior)
    {
        Services.Configure<MigrationRunOptions>(o => o.ExceptionBehavior = behavior);
        return this;
    }

    /// <summary>
    /// Scopes the migration to a specific set of schema names.
    /// </summary>
    public NSchemaApplicationBuilder ForSchemas(params string[] schemaNames)
    {
        Services.Configure<MigrationOptions>(o => o.SchemaNames = schemaNames);
        return this;
    }
}
