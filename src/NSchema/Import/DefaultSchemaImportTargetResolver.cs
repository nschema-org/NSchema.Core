using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using NSchema.Resolution;

namespace NSchema.Import;

/// <summary>
/// Resolves <see cref="Current"/>s from the set registered in DI.
/// </summary>
internal sealed class DefaultSchemaImportTargetResolver(IOptions<ImportOptions> options, IEnumerable<ISchemaImportTarget> targets)
    : KeyedResolver<string, ISchemaImportTarget>(targets, t => t.Target, "import target", StringComparer.OrdinalIgnoreCase),
        ISchemaImportTargetResolver
{
    public IReadOnlyCollection<string> AvailableTargets => Keys;

    public ISchemaImportTarget? Current
    {
        get => TryResolve(options.Value.Target, out var result) ? result : throw new InvalidOperationException("No schema import target configured.");
    }

    public ISchemaImportTarget ForTarget(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Resolve(name);
    }

    public bool TryForTarget(string name, [NotNullWhen(true)] out ISchemaImportTarget? target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return TryResolve(name, out target);
    }
}
