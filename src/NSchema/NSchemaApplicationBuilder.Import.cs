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
        return UseImportTarget<FileSchemaImportTarget>(FileSchemaImportTarget.TargetName);
    }

    /// <summary>
    /// Registers a new <see cref="ISchemaImportTarget"/> for <paramref name="target"/>.
    /// Throws if <paramref name="target"/> is already registered; use <see cref="UseImportTarget{T}"/> to replace.
    /// </summary>
    public NSchemaApplicationBuilder AddImportTarget<T>(string target) where T : class, ISchemaImportTarget
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        Services.TryAddKeyedSingleton<ISchemaImportTarget, T>(target);
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
