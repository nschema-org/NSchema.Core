using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Import;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Registers a new <see cref="ISchemaImportTarget"/> for <paramref name="target"/>.
    /// </summary>
    public NSchemaApplicationBuilder AddImportTarget<T>(string target) where T : class, ISchemaImportTarget
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        Services.Replace(ServiceDescriptor.KeyedSingleton<ISchemaImportTarget, T>(target));
        return this;
    }

    /// <summary>
    /// Registers a <see cref="FileSchemaImportTarget"/> that writes imported schemas to the local filesystem.
    /// </summary>
    /// <param name="configure">An action to configure the file import target options.</param>
    public NSchemaApplicationBuilder AddFileImportTarget(Action<FileSchemaImportTargetOptions> configure)
    {
        Services.Configure(configure);
        return AddImportTarget<FileSchemaImportTarget>(FileSchemaImportTarget.TargetName);
    }
}
