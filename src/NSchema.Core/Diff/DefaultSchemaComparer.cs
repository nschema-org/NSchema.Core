using Microsoft.Extensions.Logging;
using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

internal sealed partial class DefaultSchemaComparer(ILogger<DefaultSchemaComparer> logger) : ISchemaComparer
{
    public DatabaseDiff Compare(DatabaseSchema current, DatabaseSchema desired)
    {
        LogBeginningComparison();

        var schemas = CompareSchemas(current.Schemas, desired.Schemas);

        LogComparisonComplete(schemas.Count);

        return new DatabaseDiff(schemas);
    }

    private List<SchemaDiff> CompareSchemas(IReadOnlyList<SchemaDefinition> current, IReadOnlyList<SchemaDefinition> desired)
    {
        var result = new List<SchemaDiff>();
        var (forDesired, currentMatched) = MatchEntities(current, desired, s => s.Name, s => s.OldName, "schema");

        for (var j = 0; j < current.Count; j++)
        {
            if (currentMatched[j])
            {
                LogSchemaExists(current[j].Name);
            }
            else
            {
                LogSchemaNotInDesired(current[j].Name);
                result.Add(new SchemaDiff(current[j].Name, ChangeKind.Remove, null, null, [], []));
            }
        }

        for (var i = 0; i < desired.Count; i++)
        {
            if (forDesired[i] is not { } matchingCurrent)
            {
                LogSchemaNew(desired[i].Name);
                result.Add(BuildNewSchema(desired[i]));
            }
            else if (BuildModifiedSchema(matchingCurrent, desired[i]) is { } diff)
            {
                result.Add(diff);
            }
        }

        // The diff presents schemas ordered by name (tables likewise, within each schema).
        result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return result;
    }

    /// <summary>
    /// Pairs each current entity with at most one desired entity. Renames (an explicit <c>OldName</c>) are
    /// matched first, then remaining desired entities claim a current entity by exact name.
    /// </summary>
    /// <remarks>
    /// A rename is rejected when it collides with a surviving entity, because the result cannot be ordered
    /// safely and is indistinguishable from an add/drop pair: either the previous name is still declared in
    /// the desired set (so we cannot tell a rename from a retain-plus-create), or the new name is already
    /// taken by another current entity. The user must split the rename and the conflicting change into
    /// separate migrations.
    /// </remarks>
    /// <param name="current">The current-state entities.</param>
    /// <param name="desired">The desired-state entities.</param>
    /// <param name="name">Projects an entity's name.</param>
    /// <param name="oldName">Projects an entity's previous name (its rename source), or <c>null</c>.</param>
    /// <param name="entityKind">The noun used in the ambiguity error (e.g. <c>"table"</c>).</param>
    /// <param name="container">An optional qualifier for the entity in the error (e.g. the schema name).</param>
    /// <returns>
    /// <c>ForDesired[i]</c> is the current entity matched to <c>desired[i]</c>, or <c>null</c> if it is new;
    /// <c>CurrentMatched[j]</c> is whether <c>current[j]</c> was claimed (otherwise it has been removed).
    /// </returns>
    private static (T?[] ForDesired, bool[] CurrentMatched) MatchEntities<T>(
        IReadOnlyList<T> current,
        IReadOnlyList<T> desired,
        Func<T, string> name,
        Func<T, string?> oldName,
        string entityKind,
        string? container = null
    ) where T : class
    {
        var forDesired = new T?[desired.Count];
        var currentMatched = new bool[current.Count];
        var matchedByRename = new bool[desired.Count];

        // Pass 1: explicit renames take priority.
        for (var i = 0; i < desired.Count; i++)
        {
            if (oldName(desired[i]) is not { } renamedFrom)
            {
                continue;
            }

            for (var j = 0; j < current.Count; j++)
            {
                if (!currentMatched[j] && name(current[j]) == renamedFrom)
                {
                    forDesired[i] = current[j];
                    currentMatched[j] = true;
                    matchedByRename[i] = true;
                    break;
                }
            }
        }

        // Pass 2: exact name matches for whatever current entities remain unclaimed.
        for (var i = 0; i < desired.Count; i++)
        {
            if (forDesired[i] is not null)
            {
                continue;
            }

            for (var j = 0; j < current.Count; j++)
            {
                if (!currentMatched[j] && name(current[j]) == name(desired[i]))
                {
                    forDesired[i] = current[j];
                    currentMatched[j] = true;
                    break;
                }
            }
        }

        // Reject renames that collide with a surviving entity (see remarks).
        for (var i = 0; i < desired.Count; i++)
        {
            if (!matchedByRename[i])
            {
                continue;
            }

            var newName = name(desired[i]);
            var renamedFrom = oldName(desired[i])!;
            var index = i;

            var oldNameStillDeclared = desired.Where((_, d) => d != index).Any(d => name(d) == renamedFrom);
            var newNameAlreadyTaken = current.Any(c => name(c) == newName && !ReferenceEquals(c, forDesired[index]));

            if (oldNameStillDeclared || newNameAlreadyTaken)
            {
                var qualified = container is null ? newName : $"{container}.{newName}";
                var reason = oldNameStillDeclared
                    ? $"its previous name '{renamedFrom}' is still declared"
                    : $"a {entityKind} named '{newName}' already exists";
                throw new InvalidOperationException(
                    $"Ambiguous rename of {entityKind} '{qualified}' from '{renamedFrom}': {reason}. " +
                    "Perform the rename and the conflicting change in separate migrations.");
            }
        }

        return (forDesired, currentMatched);
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
        var (forDesired, currentMatched) = MatchEntities(current, desired.Tables, t => t.Name, t => t.OldName, "table", schemaName);

        for (var j = 0; j < current.Count; j++)
        {
            var currentTable = current[j];
            if (currentMatched[j])
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

        for (var i = 0; i < desired.Tables.Count; i++)
        {
            var desiredTable = desired.Tables[i];
            if (forDesired[i] is not { } matchingCurrent)
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
        new(schema, name, ChangeKind.Remove, null, null, [], [], [], [], [], [], []);

    private TableDiff BuildNewTable(string schemaName, Table table)
    {
        LogTableCreating(schemaName, table.Name);

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
            LogForeignKeyAddingToNewTable(fk.Name, schemaName, table.Name);
            foreignKeys.Add(new ForeignKeyDiff(ChangeKind.Add, fk.Name, fk));
            if (fk.Comment is not null)
            {
                foreignKeys.Add(new ForeignKeyDiff(ChangeKind.Modify, fk.Name, null, new ValueChange<string>(null, fk.Comment)));
            }
        }

        var uniqueConstraints = new List<UniqueConstraintDiff>();
        foreach (var uq in table.UniqueConstraints)
        {
            LogUniqueConstraintAddingToNewTable(uq.Name, schemaName, table.Name);
            uniqueConstraints.Add(new UniqueConstraintDiff(ChangeKind.Add, uq.Name, uq));
            if (uq.Comment is not null)
            {
                uniqueConstraints.Add(new UniqueConstraintDiff(ChangeKind.Modify, uq.Name, null, new ValueChange<string>(null, uq.Comment)));
            }
        }

        var checks = new List<CheckConstraintDiff>();
        foreach (var ck in table.CheckConstraints)
        {
            LogCheckConstraintAddingToNewTable(ck.Name, schemaName, table.Name);
            checks.Add(new CheckConstraintDiff(ChangeKind.Add, ck.Name, ck));
            if (ck.Comment is not null)
            {
                checks.Add(new CheckConstraintDiff(ChangeKind.Modify, ck.Name, null, new ValueChange<string>(null, ck.Comment)));
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
        // primary key and columns inline) without reconstructing it from the column diffs. The primary key is
        // therefore created inline (no PrimaryKeyDiff); everything else arrives as a separate add.
        return new TableDiff(schemaName, table.Name, ChangeKind.Add, null, comment, columns, grants, indexes, primaryKey, foreignKeys, uniqueConstraints, checks, table);
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

        var primaryKey = ComparePrimaryKey(schemaName, desired.Name, current.PrimaryKey, desired.PrimaryKey);
        var foreignKeys = CompareForeignKeys(schemaName, desired.Name, current.ForeignKeys, desired.ForeignKeys);
        var uniqueConstraints = CompareUniqueConstraints(schemaName, desired.Name, current.UniqueConstraints, desired.UniqueConstraints);
        var checks = CompareChecks(schemaName, desired.Name, current.CheckConstraints, desired.CheckConstraints);

        var indexes = CompareIndexes(schemaName, desired.Name, current.Indexes, desired.Indexes);
        var grants = CompareTableGrants(schemaName, desired.Name, current.Grants, desired.Grants);

        var hasChange = renamedFrom is not null || comment is not null || columns.Count > 0
            || primaryKey.Count > 0 || foreignKeys.Count > 0 || uniqueConstraints.Count > 0 || checks.Count > 0
            || indexes.Count > 0 || grants.Count > 0;
        if (!hasChange)
        {
            return null;
        }

        return new TableDiff(schemaName, desired.Name, ChangeKind.Modify, renamedFrom, comment, columns, grants, indexes, primaryKey, foreignKeys, uniqueConstraints, checks);
    }

    private List<ColumnDiff> CompareColumns(string schemaName, string tableName, IReadOnlyList<Column> current, IReadOnlyList<Column> desired)
    {
        var result = new List<ColumnDiff>();
        var (forDesired, currentMatched) = MatchEntities(current, desired, c => c.Name, c => c.OldName, "column", $"{schemaName}.{tableName}");

        for (var j = 0; j < current.Count; j++)
        {
            if (currentMatched[j])
            {
                LogColumnExists(schemaName, tableName, current[j].Name);
            }
            else
            {
                LogColumnNotInDesired(schemaName, tableName, current[j].Name);
                result.Add(new ColumnDiff(current[j].Name, ChangeKind.Remove, current[j], null, null, null, null, null, null));
            }
        }

        for (var i = 0; i < desired.Count; i++)
        {
            var desiredCol = desired[i];
            if (forDesired[i] is not { } matchingCurrent)
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

        // Identity changes when the column is toggled into or out of identity, or when both columns are
        // identity but their sequence options differ. Old/New options are null on the side that isn't identity.
        ValueChange<IdentityOptions>? identity = null;
        var identityToggled = current.IsIdentity != desired.IsIdentity;
        var identityOptionsChanged = current.IsIdentity && desired.IsIdentity && current.IdentityOptions != desired.IdentityOptions;
        if (identityToggled || identityOptionsChanged)
        {
            var oldOptions = current.IsIdentity ? current.IdentityOptions : null;
            var newOptions = desired.IsIdentity ? desired.IdentityOptions : null;
            LogColumnIdentityChanged(schemaName, tableName, desired.Name,
                oldOptions?.StartWith, newOptions?.StartWith,
                oldOptions?.MinValue, newOptions?.MinValue,
                oldOptions?.IncrementBy, newOptions?.IncrementBy);
            identity = new ValueChange<IdentityOptions>(oldOptions, newOptions);
        }

        if (renamedFrom is null && type is null && nullability is null && @default is null && comment is null && identity is null)
        {
            return null;
        }

        return new ColumnDiff(desired.Name, ChangeKind.Modify, null, renamedFrom, type, nullability, @default, identity, comment);
    }

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
