using Microsoft.Extensions.DependencyInjection;
using NSchema.Migration.Sql;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Registers the <see cref="ISqlGenerator"/> that generates the SQL for a migration plan.
    /// </summary>
    /// <typeparam name="T">The type of the SQL planner to register.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseSqlGenerator<T>() where T : class, ISqlGenerator
    {
        Services.AddSingleton<ISqlGenerator, T>();
        return this;
    }

    /// <summary>
    /// Adds a custom SQL executor to the application that will be used to execute the generated migration scripts against the database.
    /// </summary>
    /// <typeparam name="T">The type of the SQL executor to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseSqlExecutor<T>() where T : class, ISqlExecutor
    {
        Services.AddSingleton<ISqlExecutor, T>();
        return this;
    }
}
