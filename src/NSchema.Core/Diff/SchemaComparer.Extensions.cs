using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

internal sealed partial class SchemaComparer
{
    /// <summary>
    /// Compares the database-global extensions. Unlike the per-schema objects, an extension is removed
    /// <em>only</em> when it is explicitly dropped (named in <paramref name="droppedNames"/>): an extension
    /// present in the database but absent from the desired set is left alone. Extensions are shared
    /// infrastructure — every database has some installed by default (e.g. <c>plpgsql</c> in Postgres) — so
    /// absence must never imply a drop. Extensions never rename, so there is no <see cref="MatchEntities{T}"/>
    /// pass; matching is by exact name.
    /// </summary>
    private static List<ExtensionDiff> CompareExtensions(
        IReadOnlyList<Extension> current,
        IReadOnlyList<Extension> desired,
        IReadOnlyList<string> droppedNames
    )
    {
        var result = new List<ExtensionDiff>();

        foreach (var name in droppedNames)
        {
            var existing = current.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                result.Add(new ExtensionDiff(existing.Name, ChangeKind.Remove));
            }
        }

        foreach (var desiredExtension in desired)
        {
            var match = current.FirstOrDefault(e => string.Equals(e.Name, desiredExtension.Name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                result.Add(BuildNewExtension(desiredExtension));
            }
            else if (BuildModifiedExtension(match, desiredExtension) is { } diff)
            {
                result.Add(diff);
            }
        }

        result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return result;
    }

    private static ExtensionDiff BuildNewExtension(Extension extension) =>
        new(extension.Name, ChangeKind.Add, Definition: extension,
            Comment: ValueChanges.Changed(null, extension.Comment));

    private static ExtensionDiff? BuildModifiedExtension(Extension current, Extension desired)
    {
        // A null desired version means "accept whatever is installed", so it is never compared — an omitted
        // version cannot show as drift.
        var version = desired.Version is null ? null : ValueChanges.Changed(current.Version, desired.Version);
        var comment = ValueChanges.Changed(current.Comment, desired.Comment);

        if (version is null && comment is null)
        {
            return null;
        }

        return new ExtensionDiff(desired.Name, ChangeKind.Modify, null, version, comment);
    }
}
