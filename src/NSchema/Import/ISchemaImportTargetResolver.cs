using System.Diagnostics.CodeAnalysis;

namespace NSchema.Import;

/// <summary>
/// Resolves a registered <see cref="ISchemaImportTarget"/> by its target.
/// </summary>
public interface ISchemaImportTargetResolver
{
    /// <summary>
    /// The distinct targets that can be resolved, e.g. <c>file</c>, <c>s3</c>.
    /// </summary>
    IReadOnlyCollection<string> AvailableTargets { get; }

    /// <summary>
    /// Resolves the target registered for <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name, e.g. <c>file</c>.</param>
    ISchemaImportTarget ForTarget(string name);

    /// <summary>
    /// Attempts to resolve the registered target.
    /// </summary>
    /// <param name="name">The dialect, e.g. <c>postgres</c>.</param>
    /// <param name="target">The resolved target, or <see langword="null"/> if none is registered.</param>
    /// <returns><see langword="true"/> if a target was found; otherwise <see langword="false"/>.</returns>
    bool TryForTarget(string name, [NotNullWhen(true)]out ISchemaImportTarget? target);
}
