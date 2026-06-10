using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

internal sealed partial class SchemaComparer
{
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

    private List<ForeignKeyDiff> CompareForeignKeys(string schemaName, string tableName, IReadOnlyList<ForeignKey> current, IReadOnlyList<ForeignKey> desired)
    {
        var result = new List<ForeignKeyDiff>();

        foreach (var currentFk in current)
        {
            var matchingDesired = desired.FirstOrDefault(d => d.Name == currentFk.Name);
            if (matchingDesired is null || !currentFk.Equals(matchingDesired))
            {
                LogForeignKeyMissingOrChanged(currentFk.Name, schemaName, tableName);
                result.Add(new ForeignKeyDiff(ChangeKind.Remove, currentFk.Name, null));
            }
            else if (currentFk.Comment != matchingDesired.Comment)
            {
                LogForeignKeyCommentChanged(currentFk.Name, schemaName, tableName);
                result.Add(new ForeignKeyDiff(ChangeKind.Modify, currentFk.Name, null, new ValueChange<string>(currentFk.Comment, matchingDesired.Comment)));
            }
            else
            {
                LogForeignKeyUnchanged(currentFk.Name, schemaName, tableName);
            }
        }

        foreach (var desiredFk in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredFk.Name);
            if (matchingCurrent is null || !matchingCurrent.Equals(desiredFk))
            {
                LogForeignKeyNewOrChanged(desiredFk.Name, schemaName, tableName);
                result.Add(new ForeignKeyDiff(ChangeKind.Add, desiredFk.Name, desiredFk));
                if (desiredFk.Comment is not null)
                {
                    result.Add(new ForeignKeyDiff(ChangeKind.Modify, desiredFk.Name, null, new ValueChange<string>(null, desiredFk.Comment)));
                }
            }
            else
            {
                LogForeignKeyUnchanged(desiredFk.Name, schemaName, tableName);
            }
        }

        return result;
    }

    private List<UniqueConstraintDiff> CompareUniqueConstraints(string schemaName, string tableName, IReadOnlyList<UniqueConstraint> current, IReadOnlyList<UniqueConstraint> desired)
    {
        var result = new List<UniqueConstraintDiff>();

        foreach (var currentUq in current)
        {
            var matchingDesired = desired.FirstOrDefault(d => d.Name == currentUq.Name);
            if (matchingDesired is null || !currentUq.Equals(matchingDesired))
            {
                LogUniqueConstraintMissingOrChanged(currentUq.Name, schemaName, tableName);
                result.Add(new UniqueConstraintDiff(ChangeKind.Remove, currentUq.Name, null));
            }
            else if (currentUq.Comment != matchingDesired.Comment)
            {
                LogUniqueConstraintCommentChanged(currentUq.Name, schemaName, tableName);
                result.Add(new UniqueConstraintDiff(ChangeKind.Modify, currentUq.Name, null, new ValueChange<string>(currentUq.Comment, matchingDesired.Comment)));
            }
            else
            {
                LogUniqueConstraintUnchanged(currentUq.Name, schemaName, tableName);
            }
        }

        foreach (var desiredUq in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredUq.Name);
            if (matchingCurrent is null || !matchingCurrent.Equals(desiredUq))
            {
                LogUniqueConstraintNewOrChanged(desiredUq.Name, schemaName, tableName);
                result.Add(new UniqueConstraintDiff(ChangeKind.Add, desiredUq.Name, desiredUq));
                if (desiredUq.Comment is not null)
                {
                    result.Add(new UniqueConstraintDiff(ChangeKind.Modify, desiredUq.Name, null, new ValueChange<string>(null, desiredUq.Comment)));
                }
            }
            else
            {
                LogUniqueConstraintUnchanged(desiredUq.Name, schemaName, tableName);
            }
        }

        return result;
    }

    private List<CheckConstraintDiff> CompareChecks(string schemaName, string tableName, IReadOnlyList<CheckConstraint> current, IReadOnlyList<CheckConstraint> desired)
    {
        var result = new List<CheckConstraintDiff>();

        foreach (var currentCk in current)
        {
            var matchingDesired = desired.FirstOrDefault(d => d.Name == currentCk.Name);
            if (matchingDesired is null || !currentCk.Equals(matchingDesired))
            {
                LogCheckConstraintMissingOrChanged(currentCk.Name, schemaName, tableName);
                result.Add(new CheckConstraintDiff(ChangeKind.Remove, currentCk.Name, null));
            }
            else if (currentCk.Comment != matchingDesired.Comment)
            {
                LogCheckConstraintCommentChanged(currentCk.Name, schemaName, tableName);
                result.Add(new CheckConstraintDiff(ChangeKind.Modify, currentCk.Name, null, new ValueChange<string>(currentCk.Comment, matchingDesired.Comment)));
            }
            else
            {
                LogCheckConstraintUnchanged(currentCk.Name, schemaName, tableName);
            }
        }

        foreach (var desiredCk in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredCk.Name);
            if (matchingCurrent is null || !matchingCurrent.Equals(desiredCk))
            {
                LogCheckConstraintNewOrChanged(desiredCk.Name, schemaName, tableName);
                result.Add(new CheckConstraintDiff(ChangeKind.Add, desiredCk.Name, desiredCk));
                if (desiredCk.Comment is not null)
                {
                    result.Add(new CheckConstraintDiff(ChangeKind.Modify, desiredCk.Name, null, new ValueChange<string>(null, desiredCk.Comment)));
                }
            }
            else
            {
                LogCheckConstraintUnchanged(desiredCk.Name, schemaName, tableName);
            }
        }

        return result;
    }

    private List<IndexDiff> CompareIndexes(string schemaName, string tableName, IReadOnlyList<TableIndex> current, IReadOnlyList<TableIndex> desired)
    {
        var result = new List<IndexDiff>();

        foreach (var currentIdx in current)
        {
            var matchingDesired = desired.FirstOrDefault(d => d.Name == currentIdx.Name);
            if (matchingDesired is null || !currentIdx.Equals(matchingDesired))
            {
                LogIndexMissingOrChanged(currentIdx.Name, schemaName, tableName);
                result.Add(new IndexDiff(ChangeKind.Remove, currentIdx.Name, null, null));
            }
            else if (currentIdx.Comment != matchingDesired.Comment)
            {
                LogIndexCommentChanged(currentIdx.Name, schemaName, tableName);
                result.Add(new IndexDiff(ChangeKind.Modify, currentIdx.Name, null, new ValueChange<string>(currentIdx.Comment, matchingDesired.Comment)));
            }
            else
            {
                LogIndexUnchanged(currentIdx.Name, schemaName, tableName);
            }
        }

        foreach (var desiredIdx in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredIdx.Name);
            if (matchingCurrent is null || !matchingCurrent.Equals(desiredIdx))
            {
                LogIndexNewOrChanged(desiredIdx.Name, schemaName, tableName);
                result.Add(new IndexDiff(ChangeKind.Add, desiredIdx.Name, desiredIdx, null));
                if (desiredIdx.Comment is not null)
                {
                    result.Add(new IndexDiff(ChangeKind.Modify, desiredIdx.Name, null, new ValueChange<string>(null, desiredIdx.Comment)));
                }
            }
            else
            {
                LogIndexUnchanged(desiredIdx.Name, schemaName, tableName);
            }
        }

        return result;
    }
}
