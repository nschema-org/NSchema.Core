using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Sql;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
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
