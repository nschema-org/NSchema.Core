using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Diff;
using NSchema.Migration;
using NSchema.Plan;
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
    /// Adds all concrete types that implement <see cref="ISchemaPolicy"/> to the application from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for schema policies.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSchemaPoliciesFromAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(ISchemaPolicy).IsAssignableFrom(t));
        foreach (var type in types)
        {
            Services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(ISchemaPolicy), type));
        }
        return this;
    }

    /// <summary>
    /// Adds all concrete types that implement <see cref="ISchemaPolicy"/> to the application from the assembly containing the specified type.
    /// </summary>
    /// <typeparam name="T">The type whose containing assembly will be scanned for schema policies.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSchemaPoliciesFromAssemblyContaining<T>() => AddSchemaPoliciesFromAssembly(typeof(T).Assembly);

    /// <summary>
    /// Adds a policy to the application that will be used to validate the generated migration plan before it is executed.
    /// </summary>
    /// <typeparam name="T">The type of the policy to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddMigrationPolicy<T>() where T : class, IMigrationPolicy
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IMigrationPolicy, T>());
        return this;
    }

    /// <summary>
    /// Adds all concrete types that implement <see cref="IMigrationPolicy"/> to the application from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for migration policies.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddMigrationPoliciesFromAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IMigrationPolicy).IsAssignableFrom(t));
        foreach (var type in types)
        {
            Services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IMigrationPolicy), type));
        }
        return this;
    }

    /// <summary>
    /// Adds all concrete types that implement <see cref="IMigrationPolicy"/> to the application from the assembly containing the specified type.
    /// </summary>
    /// <typeparam name="T">The type whose containing assembly will be scanned for migration policies.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddMigrationPoliciesFromAssemblyContaining<T>() => AddMigrationPoliciesFromAssembly(typeof(T).Assembly);

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

    /// <summary>
    /// Adds all concrete types that implement <see cref="IDiffPolicy"/> to the application from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for diff policies.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddDiffPoliciesFromAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IDiffPolicy).IsAssignableFrom(t));
        foreach (var type in types)
        {
            Services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IDiffPolicy), type));
        }
        return this;
    }

    /// <summary>
    /// Adds all concrete types that implement <see cref="IDiffPolicy"/> to the application from the assembly containing the specified type.
    /// </summary>
    /// <typeparam name="T">The type whose containing assembly will be scanned for diff policies.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddDiffPoliciesFromAssemblyContaining<T>() => AddDiffPoliciesFromAssembly(typeof(T).Assembly);
}
