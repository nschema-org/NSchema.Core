using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Sql;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Registers an <see cref="ISqlGenerator"/> for a SQL dialect.
    /// Throws if <paramref name="dialect"/> is already registered.
    /// </summary>
    public NSchemaApplicationBuilder AddSqlGenerator<T>(string dialect) where T : class, ISqlGenerator
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dialect);
        Services.TryAddKeyedSingleton<ISqlGenerator, T>(dialect);
        return this;
    }

    /// <summary>
    /// Registers a custom SQL executor that will be used to execute the generated migration scripts against the database.
    /// </summary>
    public NSchemaApplicationBuilder UseSqlExecutor<T>() where T : class, ISqlExecutor
    {
        Services.Replace(ServiceDescriptor.Singleton<ISqlExecutor, T>());
        return this;
    }
}
