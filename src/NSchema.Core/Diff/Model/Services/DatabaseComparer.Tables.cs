using NSchema.Diff.Model.Columns;
using NSchema.Diff.Model.Constraints;
using NSchema.Diff.Model.Indexes;
using NSchema.Diff.Model.Tables;
using NSchema.Diff.Model.Triggers;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;

namespace NSchema.Diff.Model.Services;

internal sealed partial class DatabaseComparer
{
    private List<TableDiff> CompareTables(SqlIdentifier schemaName, SqlIdentifier currentSchemaName, IReadOnlyList<Table> current, Schema desired, DirectiveLookup directives)
    {
        var result = new List<TableDiff>();
        var droppedTables = directives.Drops(ObjectKind.Table, currentSchemaName);
        var isPartial = directives.IsPartial(schemaName);
        var (forDesired, currentMatched) = MatchEntities(current, desired.Tables, directives.Renames(ObjectKind.Table, currentSchemaName), "table", schemaName.Value);

        for (var j = 0; j < current.Count; j++)
        {
            var currentTable = current[j];
            if (currentMatched[j])
            {
                LogTableExists(schemaName, currentTable.Name);
            }
            else if (droppedTables.Contains(currentTable.Name))
            {
                LogTableExplicitlyDropped(schemaName, currentTable.Name);
                result.Add(RemovedTable(schemaName, currentTable.Name));
            }
            else if (!isPartial)
            {
                LogTableNotInDesired(schemaName, currentTable.Name);
                result.Add(RemovedTable(schemaName, currentTable.Name));
            }
            else
            {
                LogTableSkippedPartial(schemaName, currentTable.Name);
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
            else if (BuildModifiedTable(schemaName, currentSchemaName, matchingCurrent, desiredTable, directives) is { } diff)
            {
                // Change-event scripts ride the changes they accompany: attach each to its member diff here,
                // in the per-table pass that already built those diffs.
                result.Add(AttachChangeScripts(diff, directives.ChangeScripts(schemaName, desiredTable.Name)));
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

    private TableDiff? BuildModifiedTable(SqlIdentifier schemaName, SqlIdentifier currentSchemaName, Table current, Table desired, DirectiveLookup directives)
    {
        SqlIdentifier? renamedFrom = null;
        if (current.Name == desired.Name)
        {
            LogTableUnchanged(schemaName, desired.Name);
        }
        else
        {
            LogTableRenamed(schemaName, current.Name, desired.Name);
            renamedFrom = current.Name;
        }

        ValueChange<string>? comment = null;
        if (current.Comment != desired.Comment)
        {
            LogTableCommentChanged(schemaName, desired.Name);
            comment = new ValueChange<string>(current.Comment, desired.Comment);
        }

        var owner = new ObjectAddress(schemaName, desired.Name);
        var columns = CompareColumns(owner, current.Columns, desired.Columns, directives.ColumnRenames(currentSchemaName, current.Name));

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

    /// <summary>
    /// Attaches each change-event script to the member diff it accompanies — an add-column/alter-type on a
    /// column, an add-constraint on a constraint — matching by trigger and member name. The script rides the
    /// node directly, so the linearizer runs it without a lookup.
    /// </summary>
    private static TableDiff AttachChangeScripts(TableDiff table, IReadOnlyList<ChangeScript> scripts)
    {
        if (scripts.Count == 0)
        {
            return table;
        }

        ChangeScript? Match(ChangeTrigger trigger, SqlIdentifier member) => scripts.FirstOrDefault(s =>
            s.Trigger == trigger && s.MemberName == member);

        return table with
        {
            Columns = table.Columns.Select(column => column switch
            {
                { Kind: ChangeKind.Add } when Match(ChangeTrigger.AddColumn, column.Name) is { } m => column with { MigrationScript = m },
                { Kind: ChangeKind.Modify, Type: not null } when Match(ChangeTrigger.AlterColumnType, column.Name) is { } m => column with { MigrationScript = m },
                _ => column,
            }).ToList(),
            PrimaryKey = table.PrimaryKey
                .Select(pk => pk.Kind == ChangeKind.Add && Match(ChangeTrigger.AddConstraint, pk.Name) is { } m ? pk with { MigrationScript = m } : pk)
                .ToList(),
            UniqueConstraints = table.UniqueConstraints
                .Select(uc => uc.Kind == ChangeKind.Add && Match(ChangeTrigger.AddConstraint, uc.Name) is { } m ? uc with { MigrationScript = m } : uc)
                .ToList(),
            ForeignKeys = table.ForeignKeys
                .Select(fk => fk.Kind == ChangeKind.Add && Match(ChangeTrigger.AddConstraint, fk.Name) is { } m ? fk with { MigrationScript = m } : fk)
                .ToList(),
            Checks = table.Checks
                .Select(check => check.Kind == ChangeKind.Add && Match(ChangeTrigger.AddConstraint, check.Name) is { } m ? check with { MigrationScript = m } : check)
                .ToList(),
            ExclusionConstraints = table.ExclusionConstraints
                .Select(ex => ex.Kind == ChangeKind.Add && Match(ChangeTrigger.AddConstraint, ex.Name) is { } m ? ex with { MigrationScript = m } : ex)
                .ToList(),
        };
    }
}
