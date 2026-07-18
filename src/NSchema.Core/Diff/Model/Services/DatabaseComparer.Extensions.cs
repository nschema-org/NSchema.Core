using NSchema.Diff.Model.Extensions;
using NSchema.Model.Extensions;

namespace NSchema.Diff.Model.Services;

internal sealed partial class DatabaseComparer
{
    /// <summary>
    /// Compares the database-global extensions. An extension in the current side but absent from the desired
    /// set is removed like any other object — the current side only ever contains managed extensions, so
    /// shared infrastructure installed by others (e.g. <c>plpgsql</c> in Postgres) never enters the compare.
    /// Extensions never rename, so there is no <see cref="MatchEntities{T}"/> pass; matching is by exact name.
    /// </summary>
    private static List<ExtensionDiff> CompareExtensions(
        IReadOnlyList<Extension> current,
        IReadOnlyList<Extension> desired
    )
    {
        var result = new List<ExtensionDiff>();

        foreach (var currentExtension in current)
        {
            if (desired.All(e => e.Name != currentExtension.Name))
            {
                result.Add(new ExtensionDiff(currentExtension.Name, ChangeKind.Remove));
            }
        }

        foreach (var desiredExtension in desired)
        {
            var match = current.FirstOrDefault(e => e.Name == desiredExtension.Name);
            if (match is null)
            {
                result.Add(BuildNewExtension(desiredExtension));
            }
            else if (BuildModifiedExtension(match, desiredExtension) is { } diff)
            {
                result.Add(diff);
            }
        }

        result.Sort((a, b) => a.Name.CompareTo(b.Name));
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
