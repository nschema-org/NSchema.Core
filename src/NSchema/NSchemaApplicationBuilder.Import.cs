using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Import;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Registers a target for importing schemas.
    /// </summary>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddImportTarget<T>() where T : class, ISchemaImportTarget
    {
        Services.Replace(ServiceDescriptor.Singleton<ISchemaImportTarget, T>());
        return this;
    }

    /// <summary>
    /// Registers a <see cref="FileSchemaImportTarget"/> that writes imported schemas to the local filesystem.
    /// </summary>
    /// <param name="configure">An action to configure the import target options.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseFileImportTarget(Action<FileSchemaImportTargetOptions> configure)
    {
        Services.Configure(configure);
        return AddImportTarget<FileSchemaImportTarget>();
    }

    /// <summary>
    /// Restricts the import to the specified schema namespaces.
    /// </summary>
    /// <param name="configure">An action to configure the import options.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder WithImportOptions(Action<ImportOptions> configure)
    {
        Services.Configure(configure);
        return this;
    }
}
