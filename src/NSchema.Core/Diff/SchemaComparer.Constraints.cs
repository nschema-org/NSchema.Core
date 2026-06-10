using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

internal sealed partial class SchemaComparer
{
    // The primary key is single-valued (not a list member), so it keeps its own comparison rather than the
    // shared CompareTableMembers skeleton.
    private List<PrimaryKeyDiff> ComparePrimaryKey(string schemaName, string tableName, PrimaryKey? current, PrimaryKey? desired)
    {
        var result = new List<PrimaryKeyDiff>();

        // Structurally identical (Equals excludes the comment): at most a comment-only change, applied in place.
        if (current is not null && desired is not null && current.Equals(desired))
        {
            if (current.Comment != desired.Comment)
            {
                LogPrimaryKeyCommentChanged(desired.Name, schemaName, tableName);
                result.Add(new PrimaryKeyDiff(ChangeKind.Modify, desired.Name, null, new ValueChange<string>(current.Comment, desired.Comment)));
            }
            else
            {
                LogPrimaryKeyUnchanged(schemaName, tableName);
            }
            return result;
        }

        if (current is null && desired is null)
        {
            LogPrimaryKeyUnchanged(schemaName, tableName);
            return result;
        }

        if (current is not null)
        {
            LogPrimaryKeyDropping(current.Name, schemaName, tableName);
            result.Add(new PrimaryKeyDiff(ChangeKind.Remove, current.Name, null));
        }
        if (desired is not null)
        {
            LogPrimaryKeyAdding(desired.Name, schemaName, tableName);
            result.Add(new PrimaryKeyDiff(ChangeKind.Add, desired.Name, desired));
            if (desired.Comment is not null)
            {
                result.Add(new PrimaryKeyDiff(ChangeKind.Modify, desired.Name, null, new ValueChange<string>(null, desired.Comment)));
            }
        }
        return result;
    }

    private List<ForeignKeyDiff> CompareForeignKeys(string schemaName, string tableName, IReadOnlyList<ForeignKey> current, IReadOnlyList<ForeignKey> desired) =>
        CompareTableMembers(schemaName, tableName, "Foreign key", current, desired,
            (kind, name, definition, comment) => new ForeignKeyDiff(kind, name, definition, comment));

    private List<UniqueConstraintDiff> CompareUniqueConstraints(string schemaName, string tableName, IReadOnlyList<UniqueConstraint> current, IReadOnlyList<UniqueConstraint> desired) =>
        CompareTableMembers(schemaName, tableName, "Unique constraint", current, desired,
            (kind, name, definition, comment) => new UniqueConstraintDiff(kind, name, definition, comment));

    private List<CheckConstraintDiff> CompareChecks(string schemaName, string tableName, IReadOnlyList<CheckConstraint> current, IReadOnlyList<CheckConstraint> desired) =>
        CompareTableMembers(schemaName, tableName, "Check constraint", current, desired,
            (kind, name, definition, comment) => new CheckConstraintDiff(kind, name, definition, comment));

    private List<IndexDiff> CompareIndexes(string schemaName, string tableName, IReadOnlyList<TableIndex> current, IReadOnlyList<TableIndex> desired) =>
        CompareTableMembers(schemaName, tableName, "Index", current, desired,
            (kind, name, definition, comment) => new IndexDiff(kind, name, definition, comment));
}
