using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Diff;
using NSchema.Plan;
using NSchema.Schema;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Adds a transformer to the application that will be used to transform the migration plan before it is executed.
    /// </summary>
    /// <typeparam name="T">The type of the transformer to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddPlanTransformer<T>() where T : class, IMigrationPlanTransformer
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IMigrationPlanTransformer, T>());
        return this;
    }

    /// <summary>
    /// Adds all concrete types that implement <see cref="IMigrationPlanTransformer"/> to the application from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for plan transformers.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddPlanTransformersFromAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IMigrationPlanTransformer).IsAssignableFrom(t));
        foreach (var type in types)
        {
            Services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IMigrationPlanTransformer), type));
        }
        return this;
    }

    /// <summary>
    /// Adds all concrete types that implement <see cref="IMigrationPlanTransformer"/> to the application from the assembly containing the specified type.
    /// </summary>
    /// <typeparam name="T">The type whose containing assembly will be scanned for plan transformers.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddPlanTransformersFromAssemblyContaining<T>() => AddPlanTransformersFromAssembly(typeof(T).Assembly);

    /// <summary>
    /// Adds a transformer to the application that will be used to transform the desired schema before it is diffed.
    /// </summary>
    /// <typeparam name="T">The type of the transformer to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSchemaTransformer<T>() where T : class, ISchemaTransformer
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<ISchemaTransformer, T>());
        return this;
    }

    /// <summary>
    /// Adds all concrete types that implement <see cref="ISchemaTransformer"/> to the application from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for schema transformers.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSchemaTransformersFromAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(ISchemaTransformer).IsAssignableFrom(t));
        foreach (var type in types)
        {
            Services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(ISchemaTransformer), type));
        }
        return this;
    }

    /// <summary>
    /// Adds all concrete types that implement <see cref="ISchemaTransformer"/> to the application from the assembly containing the specified type.
    /// </summary>
    /// <typeparam name="T">The type whose containing assembly will be scanned for schema transformers.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSchemaTransformersFromAssemblyContaining<T>() => AddSchemaTransformersFromAssembly(typeof(T).Assembly);

    /// <summary>
    /// Adds a transformer to the application that will be used to transform the structured diff before it is linearized.
    /// </summary>
    /// <typeparam name="T">The type of the transformer to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddDiffTransformer<T>() where T : class, IDiffTransformer
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDiffTransformer, T>());
        return this;
    }

    /// <summary>
    /// Adds all concrete types that implement <see cref="IDiffTransformer"/> to the application from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for diff transformers.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddDiffTransformersFromAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IDiffTransformer).IsAssignableFrom(t));
        foreach (var type in types)
        {
            Services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IDiffTransformer), type));
        }
        return this;
    }

    /// <summary>
    /// Adds all concrete types that implement <see cref="IDiffTransformer"/> to the application from the assembly containing the specified type.
    /// </summary>
    /// <typeparam name="T">The type whose containing assembly will be scanned for diff transformers.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddDiffTransformersFromAssemblyContaining<T>() => AddDiffTransformersFromAssembly(typeof(T).Assembly);
}
