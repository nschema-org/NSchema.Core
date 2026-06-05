using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Import;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Registers a <see cref="FileSchemaImportTarget"/> that writes imported schemas to the local filesystem.
    /// </summary>
    /// <param name="configure">An action to configure the file import target options.</param>
    public NSchemaApplicationBuilder UseFileImportTarget(Action<FileSchemaImportTargetOptions> configure)
    {
        Services.Configure(configure);
        Services.Configure<ImportOptions>(o => o.Target ??= FileSchemaImportTarget.TargetName);
        return UseImportTarget<FileSchemaImportTarget>(FileSchemaImportTarget.TargetName);
    }

    /// <summary>
    /// Registers a new <see cref="ISchemaImportTarget"/> for <paramref name="target"/>.
    /// The first registered target becomes the default if none has been set explicitly via <see cref="WithImportOptions"/>.
    /// </summary>
    public NSchemaApplicationBuilder AddImportTarget<T>(string target) where T : class, ISchemaImportTarget
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        Services.TryAddKeyedSingleton<ISchemaImportTarget, T>(target);
        Services.Configure<ImportOptions>(o => o.Target ??= target);
        return this;
    }

    /// <summary>
    /// Replaces the <see cref="ISchemaImportTarget"/> registered for <paramref name="target"/>, or adds it if not yet registered.
    /// </summary>
    public NSchemaApplicationBuilder UseImportTarget<T>(string target) where T : class, ISchemaImportTarget
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        Services.Replace(ServiceDescriptor.KeyedSingleton<ISchemaImportTarget, T>(target));
        return this;
    }

    /// <summary>
    /// Configures import options such as which schemas and tables to import.
    /// </summary>
    public NSchemaApplicationBuilder WithImportOptions(Action<ImportOptions> configure)
    {
        Services.Configure(configure);
        return this;
    }
}
