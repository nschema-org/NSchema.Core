using NSchema.Diff.Model.Columns;
using NSchema.Diff.Model.Constraints;
using NSchema.Diff.Model.Indexes;
using NSchema.Diff.Model.Tables;
using NSchema.Diff.Model.Triggers;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;

namespace NSchema.Diff.Model.Services;

internal sealed partial class DatabaseComparer
{
    private List<TableDiff> CompareTables(SqlIdentifier schemaName, IReadOnlyList<Table> current, Schema desired, RenameLog renames)
    {
        var result = new List<TableDiff>();
        var (forDesired, currentMatched) = MatchEntities(current, desired.Tables);

        for (var j = 0; j < current.Count; j++)
        {
            var currentTable = current[j];
            if (currentMatched[j])
            {
                LogTableExists(schemaName, currentTable.Name);
            }
            else
            {
                LogTableNotInDesired(schemaName, currentTable.Name);
                result.Add(RemovedTable(schemaName, currentTable.Name));
            }
        }

        for (var i = 0; i < desired.Tables.Count; i++)
        {
            var desiredTable = desired.Tables[i];
            if (forDesired[i] is not { } matchingCurrent)
            {
                LogTableNew(schemaName, desiredTable.Name);
                result.Add(BuildNewTable(schemaName, desiredTable));
            }
            else
            {
                var renamedFrom = renames.RenamedFrom(new ObjectIdentity(ObjectKind.Table, schemaName, desiredTable.Name));
                if (BuildModifiedTable(schemaName, matchingCurrent, desiredTable, renamedFrom, renames) is { } diff)
                {
                    result.Add(diff);
                }
            }
        }

        return result;
    }

    private static TableDiff RemovedTable(SqlIdentifier schema, SqlIdentifier name) =>
        new(schema, name, ChangeKind.Remove);

    private TableDiff BuildNewTable(SqlIdentifier schemaName, Table table)
    {
        LogTableCreating(schemaName, table.Name);
        var owner = new ObjectAddress(schemaName, table.Name);

        var columns = table.Columns
            .Select(column => new ColumnDiff(
                column.Name, ChangeKind.Add, column, RenamedFrom: null,
                Type: null, Nullability: null, Default: null, Identity: null,
                Comment: column.Comment is not null ? new ValueChange<string>(null, column.Comment) : null))
            .ToList();

        // A constraint comment is never set inline by CREATE TABLE / ADD CONSTRAINT; it folds in as a separate
        // comment change, mirroring how a new index carries its comment as a trailing Modify.
        var foreignKeys = new List<ForeignKeyDiff>();
        foreach (var fk in table.ForeignKeys)
        {
            LogForeignKeyAddingToNewTable(fk.Name, owner);
            foreignKeys.Add(new ForeignKeyDiff(ChangeKind.Add, fk.Name, fk));
            if (fk.Comment is not null)
            {
                foreignKeys.Add(new ForeignKeyDiff(ChangeKind.Modify, fk.Name, null, new ValueChange<string>(null, fk.Comment)));
            }
        }

        var uniqueConstraints = new List<UniqueConstraintDiff>();
        foreach (var uq in table.UniqueConstraints)
        {
            LogUniqueConstraintAddingToNewTable(uq.Name, owner);
            uniqueConstraints.Add(new UniqueConstraintDiff(ChangeKind.Add, uq.Name, uq));
            if (uq.Comment is not null)
            {
                uniqueConstraints.Add(new UniqueConstraintDiff(ChangeKind.Modify, uq.Name, null, new ValueChange<string>(null, uq.Comment)));
            }
        }

        var checks = new List<CheckConstraintDiff>();
        foreach (var ck in table.CheckConstraints)
        {
            LogCheckConstraintAddingToNewTable(ck.Name, owner);
            checks.Add(new CheckConstraintDiff(ChangeKind.Add, ck.Name, ck));
            if (ck.Comment is not null)
            {
                checks.Add(new CheckConstraintDiff(ChangeKind.Modify, ck.Name, null, new ValueChange<string>(null, ck.Comment)));
            }
        }

        var exclusions = new List<ExclusionConstraintDiff>();
        foreach (var ex in table.ExclusionConstraints)
        {
            exclusions.Add(new ExclusionConstraintDiff(ChangeKind.Add, ex.Name, ex));
            if (ex.Comment is not null)
            {
                exclusions.Add(new ExclusionConstraintDiff(ChangeKind.Modify, ex.Name, null, new ValueChange<string>(null, ex.Comment)));
            }
        }

        // The primary key is created inline by CREATE TABLE, but its comment still needs a separate set.
        var primaryKey = new List<PrimaryKeyDiff>();
        if (table.PrimaryKey is { Comment: not null } pk)
        {
            primaryKey.Add(new PrimaryKeyDiff(ChangeKind.Modify, pk.Name, null, new ValueChange<string>(null, pk.Comment)));
        }

        var indexes = new List<IndexDiff>();
        foreach (var idx in table.Indexes)
        {
            LogIndexAddingToNewTable(idx.Name, owner);
            indexes.Add(new IndexDiff(ChangeKind.Add, idx.Name, idx, null));
            if (idx.Comment is not null)
            {
                indexes.Add(new IndexDiff(ChangeKind.Modify, idx.Name, null, new ValueChange<string>(null, idx.Comment)));
            }
        }

        var grants = new List<GrantChange>();
        foreach (var grant in table.Grants)
        {
            LogTablePrivilegesGrantingToNewTable(owner, grant.Role);
            grants.Add(new GrantChange(ChangeKind.Add, grant.Role, grant.Privileges));
        }

        var triggers = new List<TriggerDiff>();
        foreach (var trg in table.Triggers)
        {
            triggers.Add(new TriggerDiff(ChangeKind.Add, trg.Name, trg, null));
            if (trg.Comment is not null)
            {
                triggers.Add(new TriggerDiff(ChangeKind.Modify, trg.Name, null, new ValueChange<string>(null, trg.Comment)));
            }
        }

        var comment = table.Comment is not null ? new ValueChange<string>(null, table.Comment) : null;

        // The full table definition rides along so the linearizer can emit a single CREATE TABLE (with the
        // primary key and columns inline) without reconstructing it from the column diffs. The primary key is
        // therefore created inline (no PrimaryKeyDiff); everything else arrives as a separate add.
        return new TableDiff(schemaName, table.Name, ChangeKind.Add, null, comment, columns, grants, indexes, primaryKey, foreignKeys, uniqueConstraints, checks, exclusions, triggers, table);
    }

    private TableDiff? BuildModifiedTable(SqlIdentifier schemaName, Table current, Table desired, SqlIdentifier? renamedFrom, RenameLog renames)
    {
        if (renamedFrom is null)
        {
            LogTableUnchanged(schemaName, desired.Name);
        }
        else
        {
            LogTableRenamed(schemaName, renamedFrom, desired.Name);
        }

        ValueChange<string>? comment = null;
        if (current.Comment != desired.Comment)
        {
            LogTableCommentChanged(schemaName, desired.Name);
            comment = new ValueChange<string>(current.Comment, desired.Comment);
        }

        var owner = new ObjectAddress(schemaName, desired.Name);
        var columns = CompareColumns(owner, current.Columns, desired.Columns, renames);

        var primaryKey = ComparePrimaryKey(owner, current.PrimaryKey, desired.PrimaryKey);
        var foreignKeys = CompareForeignKeys(owner, current.ForeignKeys, desired.ForeignKeys);
        var uniqueConstraints = CompareUniqueConstraints(owner, current.UniqueConstraints, desired.UniqueConstraints);
        var checks = CompareChecks(owner, current.CheckConstraints, desired.CheckConstraints);
        var exclusions = CompareExclusionConstraints(owner, current.ExclusionConstraints, desired.ExclusionConstraints);

        var indexes = CompareIndexes(owner, current.Indexes, desired.Indexes);
        var grants = CompareTableGrants(owner, current.Grants, desired.Grants);
        var triggers = CompareTriggers(owner, current.Triggers, desired.Triggers);

        var hasChange = renamedFrom is not null || comment is not null || columns.Count > 0
            || primaryKey.Count > 0 || foreignKeys.Count > 0 || uniqueConstraints.Count > 0 || checks.Count > 0
            || exclusions.Count > 0 || indexes.Count > 0 || grants.Count > 0 || triggers.Count > 0;
        if (!hasChange)
        {
            return null;
        }

        return new TableDiff(schemaName, desired.Name, ChangeKind.Modify, renamedFrom, comment, columns, grants, indexes, primaryKey, foreignKeys, uniqueConstraints, checks, exclusions, triggers);
    }
}
