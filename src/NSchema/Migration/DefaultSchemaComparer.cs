using Microsoft.Extensions.Logging;
using NSchema.Migration.Diff.Model;
using NSchema.Schema;

namespace NSchema.Migration;

internal sealed partial class DefaultSchemaComparer(ILogger<DefaultSchemaComparer> logger) : ISchemaComparer
{
    public MigrationDiff Compare(DatabaseSchema current, DatabaseSchema desired)
    {
        LogBeginningComparison();

        var schemas = CompareSchemas(current.Schemas, desired.Schemas);

        LogComparisonComplete(schemas.Count);

        return new MigrationDiff(schemas, [], []);
    }

    private List<SchemaDiff> CompareSchemas(IReadOnlyList<SchemaDefinition> current, IReadOnlyList<SchemaDefinition> desired)
    {
        var result = new List<SchemaDiff>();

        foreach (var currentSchema in current)
        {
            if (desired.Any(d => d.Name == currentSchema.Name || d.OldName == currentSchema.Name))
            {
                LogSchemaExists(currentSchema.Name);
            }
            else
            {
                LogSchemaNotInDesired(currentSchema.Name);
                result.Add(new SchemaDiff(currentSchema.Name, ChangeKind.Remove, null, null, [], []));
            }
        }

        foreach (var desiredSchema in desired)
        {
            var matchingCurrent = current.FirstOrDefault(schema => schema.Name == desiredSchema.Name || schema.Name == desiredSchema.OldName);
            if (matchingCurrent is null)
            {
                LogSchemaNew(desiredSchema.Name);
                result.Add(BuildNewSchema(desiredSchema));
            }
            else if (BuildModifiedSchema(matchingCurrent, desiredSchema) is { } diff)
            {
                result.Add(diff);
            }
        }

        // The diff presents schemas ordered by name (tables likewise, within each schema).
        result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return result;
    }

    private SchemaDiff BuildNewSchema(SchemaDefinition desired)
    {
        var tables = desired.Tables
            .Select(table => BuildNewTable(desired.Name, table))
            .OrderBy(table => table.Name, StringComparer.Ordinal)
            .ToList();

        var comment = desired.Comment is not null ? new ValueChange<string>(null, desired.Comment) : null;
        var grants = desired.Grants.Select(grant => new GrantChange(ChangeKind.Add, grant.Role, null)).ToList();

        return new SchemaDiff(desired.Name, ChangeKind.Add, null, comment, grants, tables);
    }

    private SchemaDiff? BuildModifiedSchema(SchemaDefinition current, SchemaDefinition desired)
    {
        string? renamedFrom = null;
        if (current.Name == desired.Name)
        {
            LogSchemaUnchanged(desired.Name);
        }
        else
        {
            LogSchemaRenamed(current.Name, desired.Name);
            renamedFrom = current.Name;
        }

        ValueChange<string>? comment = null;
        if (current.Comment != desired.Comment)
        {
            LogSchemaCommentChanged(desired.Name);
            comment = new ValueChange<string>(current.Comment, desired.Comment);
        }

        var grants = CompareSchemaGrants(desired.Name, current.Grants, desired.Grants);
        var tables = CompareTables(desired.Name, current.Tables, desired)
            .OrderBy(table => table.Name, StringComparer.Ordinal)
            .ToList();

        // The schema entity itself only changes when it is renamed or its comment/grants change; a schema that
        // merely contains changed tables has a null Kind.
        var schemaLevelChange = renamedFrom is not null || comment is not null || grants.Count > 0;
        if (!schemaLevelChange && tables.Count == 0)
        {
            return null;
        }

        return new SchemaDiff(desired.Name, schemaLevelChange ? ChangeKind.Modify : null, renamedFrom, comment, grants, tables);
    }

    private List<TableDiff> CompareTables(string schemaName, IReadOnlyList<Table> current, SchemaDefinition desired)
    {
        var result = new List<TableDiff>();
        var droppedTables = desired.DroppedTables;

        foreach (var currentTable in current)
        {
            if (desired.Tables.Any(d => d.Name == currentTable.Name || d.OldName == currentTable.Name))
            {
                LogTableExists(schemaName, currentTable.Name);
            }
            else if (droppedTables.Contains(currentTable.Name, StringComparer.OrdinalIgnoreCase))
            {
                LogTableExplicitlyDropped(schemaName, currentTable.Name);
                result.Add(RemovedTable(schemaName, currentTable.Name));
            }
            else if (!desired.IsPartial)
            {
                LogTableNotInDesired(schemaName, currentTable.Name);
                result.Add(RemovedTable(schemaName, currentTable.Name));
            }
            else
            {
                LogTableSkippedPartial(schemaName, currentTable.Name);
            }
        }

        foreach (var desiredTable in desired.Tables)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredTable.Name || c.Name == desiredTable.OldName);
            if (matchingCurrent is null)
            {
                LogTableNew(schemaName, desiredTable.Name);
                result.Add(BuildNewTable(schemaName, desiredTable));
            }
            else if (BuildModifiedTable(schemaName, matchingCurrent, desiredTable) is { } diff)
            {
                result.Add(diff);
            }
        }

        return result;
    }

    private static TableDiff RemovedTable(string schema, string name) =>
        new(schema, name, ChangeKind.Remove, null, null, [], [], [], []);

    private TableDiff BuildNewTable(string schemaName, Table table)
    {
        LogTableCreating(schemaName, table.Name);

        var columns = table.Columns
            .Select(column => new ColumnDiff(
                column.Name, ChangeKind.Add, column, RenamedFrom: null,
                Type: null, Nullability: null, Default: null, Identity: null,
                Comment: column.Comment is not null ? new ValueChange<string>(null, column.Comment) : null))
            .ToList();

        var constraints = new List<ConstraintDiff>();
        foreach (var fk in table.ForeignKeys)
        {
            LogForeignKeyAddingToNewTable(fk.Name, schemaName, table.Name);
            constraints.Add(new ConstraintDiff(ChangeKind.Add, ConstraintType.ForeignKey, fk.Name, null, fk));
        }

        var indexes = new List<IndexDiff>();
        foreach (var idx in table.Indexes)
        {
            LogIndexAddingToNewTable(idx.Name, schemaName, table.Name);
            indexes.Add(new IndexDiff(ChangeKind.Add, idx.Name, idx, null));
            if (idx.Comment is not null)
            {
                indexes.Add(new IndexDiff(ChangeKind.Modify, idx.Name, null, new ValueChange<string>(null, idx.Comment)));
            }
        }

        var grants = new List<GrantChange>();
        foreach (var grant in table.Grants)
        {
            LogTablePrivilegesGrantingToNewTable(schemaName, table.Name, grant.Role);
            grants.Add(new GrantChange(ChangeKind.Add, grant.Role, grant.Privileges));
        }

        var comment = table.Comment is not null ? new ValueChange<string>(null, table.Comment) : null;

        // The full table definition rides along so the linearizer can emit a single CREATE TABLE (with the
        // primary key and columns inline) without reconstructing it from the column diffs.
        return new TableDiff(schemaName, table.Name, ChangeKind.Add, null, comment, columns, grants, indexes, constraints, table);
    }

    private TableDiff? BuildModifiedTable(string schemaName, Table current, Table desired)
    {
        string? renamedFrom = null;
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

        var columns = CompareColumns(schemaName, desired.Name, current.Columns, desired.Columns);

        var constraints = new List<ConstraintDiff>();
        constraints.AddRange(ComparePrimaryKey(schemaName, desired.Name, current.PrimaryKey, desired.PrimaryKey));
        constraints.AddRange(CompareForeignKeys(schemaName, desired.Name, current.ForeignKeys, desired.ForeignKeys));

        var indexes = CompareIndexes(schemaName, desired.Name, current.Indexes, desired.Indexes);
        var grants = CompareTableGrants(schemaName, desired.Name, current.Grants, desired.Grants);

        var hasChange = renamedFrom is not null || comment is not null
            || columns.Count > 0 || constraints.Count > 0 || indexes.Count > 0 || grants.Count > 0;
        if (!hasChange)
        {
            return null;
        }

        return new TableDiff(schemaName, desired.Name, ChangeKind.Modify, renamedFrom, comment, columns, grants, indexes, constraints);
    }

    private List<ColumnDiff> CompareColumns(string schemaName, string tableName, IReadOnlyList<Column> current, IReadOnlyList<Column> desired)
    {
        var result = new List<ColumnDiff>();

        foreach (var currentCol in current)
        {
            if (desired.Any(d => d.Name == currentCol.Name || d.OldName == currentCol.Name))
            {
                LogColumnExists(schemaName, tableName, currentCol.Name);
            }
            else
            {
                LogColumnNotInDesired(schemaName, tableName, currentCol.Name);
                result.Add(new ColumnDiff(currentCol.Name, ChangeKind.Remove, currentCol, null, null, null, null, null, null));
            }
        }

        foreach (var desiredCol in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredCol.Name || c.Name == desiredCol.OldName);
            if (matchingCurrent is null)
            {
                LogColumnNew(schemaName, tableName, desiredCol.Name);
                var comment = desiredCol.Comment is not null ? new ValueChange<string>(null, desiredCol.Comment) : null;
                result.Add(new ColumnDiff(desiredCol.Name, ChangeKind.Add, desiredCol, null, null, null, null, null, comment));
            }
            else if (BuildModifiedColumn(schemaName, tableName, matchingCurrent, desiredCol) is { } col)
            {
                result.Add(col);
            }
        }

        return result;
    }

    private ColumnDiff? BuildModifiedColumn(string schemaName, string tableName, Column current, Column desired)
    {
        string? renamedFrom = null;
        if (current.Name == desired.Name)
        {
            LogColumnUnchanged(schemaName, tableName, desired.Name);
        }
        else
        {
            LogColumnRenamed(schemaName, tableName, current.Name, desired.Name);
            renamedFrom = current.Name;
        }

        ValueChange<SqlType>? type = null;
        if (current.Type == desired.Type)
        {
            LogColumnTypeUnchanged(schemaName, tableName, desired.Name, desired.Type);
        }
        else
        {
            LogColumnTypeChanged(schemaName, tableName, desired.Name, current.Type, desired.Type);
            type = new ValueChange<SqlType>(current.Type, desired.Type);
        }

        ValueChange<bool>? nullability = null;
        if (current.IsNullable == desired.IsNullable)
        {
            LogColumnNullabilityUnchanged(schemaName, tableName, desired.Name, desired.IsNullable ? "NULL" : "NOT NULL");
        }
        else
        {
            LogColumnNullabilityChanged(schemaName, tableName, desired.Name, current.IsNullable, desired.IsNullable);
            nullability = new ValueChange<bool>(current.IsNullable, desired.IsNullable);
        }

        ValueChange<string>? @default = null;
        if (current.DefaultExpression == desired.DefaultExpression)
        {
            LogColumnDefaultUnchanged(schemaName, tableName, desired.Name, desired.DefaultExpression ?? "no default");
        }
        else
        {
            LogColumnDefaultChanged(schemaName, tableName, desired.Name, current.DefaultExpression, desired.DefaultExpression);
            @default = new ValueChange<string>(current.DefaultExpression, desired.DefaultExpression);
        }

        ValueChange<string>? comment = null;
        if (current.Comment != desired.Comment)
        {
            LogColumnCommentChanged(schemaName, tableName, desired.Name);
            comment = new ValueChange<string>(current.Comment, desired.Comment);
        }

        ValueChange<IdentityOptions>? identity = null;
        if (current.IsIdentity && desired.IsIdentity && current.IdentityOptions != desired.IdentityOptions)
        {
            LogColumnIdentityChanged(schemaName, tableName, desired.Name,
                current.IdentityOptions?.StartWith, desired.IdentityOptions?.StartWith,
                current.IdentityOptions?.MinValue, desired.IdentityOptions?.MinValue,
                current.IdentityOptions?.IncrementBy, desired.IdentityOptions?.IncrementBy);
            identity = new ValueChange<IdentityOptions>(current.IdentityOptions, desired.IdentityOptions);
        }

        if (renamedFrom is null && type is null && nullability is null && @default is null && comment is null && identity is null)
        {
            return null;
        }

        return new ColumnDiff(desired.Name, ChangeKind.Modify, null, renamedFrom, type, nullability, @default, identity, comment);
    }

    private List<ConstraintDiff> ComparePrimaryKey(string schemaName, string tableName, PrimaryKey? current, PrimaryKey? desired)
    {
        if (current?.Equals(desired) ?? desired == null)
        {
            LogPrimaryKeyUnchanged(schemaName, tableName);
            return [];
        }

        var result = new List<ConstraintDiff>();
        if (current is not null)
        {
            LogPrimaryKeyDropping(current.Name, schemaName, tableName);
            result.Add(new ConstraintDiff(ChangeKind.Remove, ConstraintType.PrimaryKey, current.Name, null, null));
        }
        if (desired is not null)
        {
            LogPrimaryKeyAdding(desired.Name, schemaName, tableName);
            result.Add(new ConstraintDiff(ChangeKind.Add, ConstraintType.PrimaryKey, desired.Name, desired, null));
        }
        return result;
    }

    private List<ConstraintDiff> CompareForeignKeys(string schemaName, string tableName, IReadOnlyList<ForeignKey> current, IReadOnlyList<ForeignKey> desired)
    {
        var result = new List<ConstraintDiff>();

        foreach (var currentFk in current)
        {
            var matchingDesired = desired.FirstOrDefault(d => d.Name == currentFk.Name);
            if (matchingDesired is null || !currentFk.Equals(matchingDesired))
            {
                LogForeignKeyMissingOrChanged(currentFk.Name, schemaName, tableName);
                result.Add(new ConstraintDiff(ChangeKind.Remove, ConstraintType.ForeignKey, currentFk.Name, null, null));
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
                result.Add(new ConstraintDiff(ChangeKind.Add, ConstraintType.ForeignKey, desiredFk.Name, null, desiredFk));
            }
            else
            {
                LogForeignKeyUnchanged(desiredFk.Name, schemaName, tableName);
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

    private List<GrantChange> CompareSchemaGrants(string schemaName, IReadOnlyList<SchemaGrant> current, IReadOnlyList<SchemaGrant> desired)
    {
        var result = new List<GrantChange>();
        foreach (var g in current.Where(c => desired.All(d => d.Role != c.Role)))
        {
            LogSchemaUsageRevoking(schemaName, g.Role);
            result.Add(new GrantChange(ChangeKind.Remove, g.Role, null));
        }
        foreach (var g in desired.Where(d => current.All(c => c.Role != d.Role)))
        {
            LogSchemaUsageGranting(schemaName, g.Role);
            result.Add(new GrantChange(ChangeKind.Add, g.Role, null));
        }
        return result;
    }

    private List<GrantChange> CompareTableGrants(string schemaName, string tableName, IReadOnlyList<TableGrant> current, IReadOnlyList<TableGrant> desired)
    {
        var result = new List<GrantChange>();
        foreach (var g in current)
        {
            var matching = desired.FirstOrDefault(d => d.Role == g.Role);
            if (matching is null)
            {
                LogTablePrivilegesRevoking(schemaName, tableName, g.Role);
                result.Add(new GrantChange(ChangeKind.Remove, g.Role, g.Privileges));
            }
            else if (matching.Privileges != g.Privileges)
            {
                LogTablePrivilegesUpdating(schemaName, tableName, g.Role);
                result.Add(new GrantChange(ChangeKind.Remove, g.Role, g.Privileges));
                result.Add(new GrantChange(ChangeKind.Add, g.Role, matching.Privileges));
            }
        }
        foreach (var g in desired.Where(d => current.All(c => c.Role != d.Role)))
        {
            LogTablePrivilegesGranting(schemaName, tableName, g.Role);
            result.Add(new GrantChange(ChangeKind.Add, g.Role, g.Privileges));
        }
        return result;
    }
}
