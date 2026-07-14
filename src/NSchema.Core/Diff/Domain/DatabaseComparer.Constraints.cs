using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Constraints;
using NSchema.Diff.Domain.Models.Indexes;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Constraints;
using NSchema.Project.Domain.Models.Indexes;
using NSchema.Project.Domain.Models.Tables;

namespace NSchema.Diff.Domain;

internal sealed partial class DatabaseComparer
{
    // The primary key is single-valued (not a list member), so it keeps its own comparison rather than the
    // shared CompareTableMembers skeleton.
    private List<PrimaryKeyDiff> ComparePrimaryKey(ObjectReference owner, PrimaryKey? current, PrimaryKey? desired)
    {
        var result = new List<PrimaryKeyDiff>();

        // Structurally identical (Equals excludes the comment): at most a comment-only change, applied in place.
        if (current is not null && desired is not null && current.Equals(desired))
        {
            if (current.Comment != desired.Comment)
            {
                LogPrimaryKeyCommentChanged(desired.Name, owner);
                result.Add(new PrimaryKeyDiff(ChangeKind.Modify, desired.Name, null, new ValueChange<string>(current.Comment, desired.Comment)));
            }
            else
            {
                LogPrimaryKeyUnchanged(owner);
            }
            return result;
        }

        if (current is null && desired is null)
        {
            LogPrimaryKeyUnchanged(owner);
            return result;
        }

        if (current is not null)
        {
            LogPrimaryKeyDropping(current.Name, owner);
            result.Add(new PrimaryKeyDiff(ChangeKind.Remove, current.Name, null));
        }
        if (desired is not null)
        {
            LogPrimaryKeyAdding(desired.Name, owner);
            result.Add(new PrimaryKeyDiff(ChangeKind.Add, desired.Name, desired));
            if (desired.Comment is not null)
            {
                result.Add(new PrimaryKeyDiff(ChangeKind.Modify, desired.Name, null, new ValueChange<string>(null, desired.Comment)));
            }
        }
        return result;
    }

    private List<ForeignKeyDiff> CompareForeignKeys(ObjectReference owner, IReadOnlyList<ForeignKey> current, IReadOnlyList<ForeignKey> desired) =>
        CompareTableMembers(owner, "Foreign key", current, desired,
            (kind, name, definition, comment) => new ForeignKeyDiff(kind, name, definition, comment));

    private List<UniqueConstraintDiff> CompareUniqueConstraints(ObjectReference owner, IReadOnlyList<UniqueConstraint> current, IReadOnlyList<UniqueConstraint> desired) =>
        CompareTableMembers(owner, "Unique constraint", current, desired,
            (kind, name, definition, comment) => new UniqueConstraintDiff(kind, name, definition, comment));

    private List<CheckConstraintDiff> CompareChecks(ObjectReference owner, IReadOnlyList<CheckConstraint> current, IReadOnlyList<CheckConstraint> desired) =>
        CompareTableMembers(owner, "Check constraint", current, desired,
            (kind, name, definition, comment) => new CheckConstraintDiff(kind, name, definition, comment));

    private List<ExclusionConstraintDiff> CompareExclusionConstraints(ObjectReference owner, IReadOnlyList<ExclusionConstraint> current, IReadOnlyList<ExclusionConstraint> desired) =>
        CompareTableMembers(owner, "Exclusion constraint", current, desired,
            (kind, name, definition, comment) => new ExclusionConstraintDiff(kind, name, definition, comment));

    private List<IndexDiff> CompareIndexes(ObjectReference owner, IReadOnlyList<TableIndex> current, IReadOnlyList<TableIndex> desired) =>
        CompareTableMembers(owner, "Index", current, desired,
            (kind, name, definition, comment) => new IndexDiff(kind, name, definition, comment));
}
