using NSchema.Diff.Model.Extensions;
using NSchema.Model.Extensions;

namespace NSchema.Diff.Model.Services;

internal sealed partial class DatabaseComparer
{
    /// <summary>
    /// Compares the database-global extensions. An extension in the current side but absent from the desired
    /// set is removed like any other object — the current side only ever contains managed extensions, so
    /// shared infrastructure installed by others (e.g. <c>plpgsql</c> in Postgres) never enters the compare.
    /// Extensions never rename, so the rename lookup is constantly null.
    /// </summary>
    private static List<ExtensionDiff> CompareExtensions(
        IReadOnlyList<Extension> current,
        IReadOnlyList<Extension> desired
    ) =>
        CompareObjects(current, desired,
            _ => null,
            extension => new ExtensionDiff(extension.Name, ChangeKind.Remove),
            BuildNewExtension,
            (currentExtension, desiredExtension, _) => BuildModifiedExtension(currentExtension, desiredExtension));

    private static ExtensionDiff BuildNewExtension(Extension extension) =>
        new(extension.Name, ChangeKind.Add, Definition: extension,
            Comment: ValueChange.Between(null, extension.Comment));

    private static ExtensionDiff? BuildModifiedExtension(Extension current, Extension desired)
    {
        // A null desired version means "accept whatever is installed", so it is never compared — an omitted
        // version cannot show as drift.
        var version = desired.Version is null ? null : ValueChange.Between(current.Version, desired.Version);
        var comment = ValueChange.Between(current.Comment, desired.Comment);

        if (version is null && comment is null)
        {
            return null;
        }

        return new ExtensionDiff(desired.Name, ChangeKind.Modify, null, version, comment);
    }
}
