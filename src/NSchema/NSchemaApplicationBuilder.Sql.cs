using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Sql;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Registers an <see cref="ISqlGenerator"/> that generates the SQL for a migration plan.
    /// </summary>
    /// <typeparam name="T">The type of the SQL generator to register.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSqlGenerator<T>() where T : class, ISqlGenerator
    {
        Services.AddSingleton<ISqlGenerator, T>();
        return this;
    }

    /// <summary>
    /// Registers a custom SQL executor that will be used to execute the generated migration scripts against the database.
    /// </summary>
    /// <typeparam name="T">The type of the SQL executor to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseSqlExecutor<T>() where T : class, ISqlExecutor
    {
        Services.Replace(ServiceDescriptor.Singleton<ISqlExecutor, T>());
        return this;
    }
}
