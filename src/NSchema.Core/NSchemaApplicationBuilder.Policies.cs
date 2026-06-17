using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Diff;
using NSchema.Schema;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Adds a policy to the application that will be used to validate the desired schema.
    /// </summary>
    /// <typeparam name="T">The type of the policy to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSchemaPolicy<T>() where T : class, ISchemaPolicy
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<ISchemaPolicy, T>());
        return this;
    }

    /// <summary>
    /// Adds a policy to the application that will be used to validate the structured diff before it is executed.
    /// </summary>
    /// <typeparam name="T">The type of the policy to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddDiffPolicy<T>() where T : class, IDiffPolicy
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDiffPolicy, T>());
        return this;
    }
}
