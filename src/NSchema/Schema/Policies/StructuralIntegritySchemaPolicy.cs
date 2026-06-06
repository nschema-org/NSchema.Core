using NSchema.Policies;
using NSchema.Schema.Model;

namespace NSchema.Schema.Policies;

/// <summary>
/// Validates that every primary key, index, and foreign key references columns and tables that actually exist within the document.
/// </summary>
public sealed class StructuralIntegritySchemaPolicy : ISchemaPolicy
{
    private const string PolicyName = "structural-integrity";

    /// <inheritdoc />
    public IEnumerable<PolicyDiagnostic> Validate(DatabaseSchema schema)
    {
        var managedSchemas = new HashSet<string>(schema.Schemas.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
        var partialSchemas = new HashSet<string>(
            schema.Schemas.Where(s => s.IsPartial).Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
        var tablesByKey = schema.Schemas
            .SelectMany(s => s.Tables.Select(t => (Key: Key(s.Name, t.Name), Table: t)))
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First().Table);

        var diagnostics = new List<PolicyDiagnostic>();
        foreach (var definition in schema.Schemas)
        {
            foreach (var table in definition.Tables)
            {
                ValidateTable(definition, table, managedSchemas, partialSchemas, tablesByKey, diagnostics);
            }
        }

        return diagnostics;
    }

    private static void ValidateTable(
        SchemaDefinition definition,
        Table table,
        HashSet<string> managedSchemas,
        HashSet<string> partialSchemas,
        IReadOnlyDictionary<string, Table> tablesByKey,
        List<PolicyDiagnostic> diagnostics)
    {
        var qualified = $"{definition.Name}.{table.Name}";
        var columns = new HashSet<string>(table.Columns.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

        if (table.Columns.Count == 0)
        {
            diagnostics.Add(Error($"Table '{qualified}' has no columns."));
        }

        foreach (var duplicate in Duplicates(table.Columns.Select(c => c.Name)))
        {
            diagnostics.Add(Error($"Table '{qualified}' declares column '{duplicate}' more than once."));
        }

        if (table.PrimaryKey is { } primaryKey)
        {
            foreach (var missing in primaryKey.ColumnNames.Where(c => !columns.Contains(c)))
            {
                diagnostics.Add(Error($"Primary key '{primaryKey.Name}' on '{qualified}' references unknown column '{missing}'."));
            }
        }

        foreach (var index in table.Indexes)
        {
            foreach (var missing in index.ColumnNames.Where(c => !columns.Contains(c)))
            {
                diagnostics.Add(Error($"Index '{index.Name}' on '{qualified}' references unknown column '{missing}'."));
            }
        }

        foreach (var foreignKey in table.ForeignKeys)
        {
            ValidateForeignKey(qualified, foreignKey, columns, managedSchemas, partialSchemas, tablesByKey, diagnostics);
        }
    }

    private static void ValidateForeignKey(
        string qualified,
        ForeignKey foreignKey,
        HashSet<string> localColumns,
        HashSet<string> managedSchemas,
        HashSet<string> partialSchemas,
        IReadOnlyDictionary<string, Table> tablesByKey,
        List<PolicyDiagnostic> diagnostics)
    {
        foreach (var missing in foreignKey.ColumnNames.Where(c => !localColumns.Contains(c)))
        {
            diagnostics.Add(Error($"Foreign key '{foreignKey.Name}' on '{qualified}' references unknown local column '{missing}'."));
        }

        if (foreignKey.ColumnNames.Count != foreignKey.ReferencedColumnNames.Count)
        {
            diagnostics.Add(Error(
                $"Foreign key '{foreignKey.Name}' on '{qualified}' has {foreignKey.ColumnNames.Count} local column(s) " +
                $"but {foreignKey.ReferencedColumnNames.Count} referenced column(s); the counts must match."));
            return;
        }

        // Only resolve targets that this document is responsible for. An absent or partial schema is owned elsewhere.
        if (!managedSchemas.Contains(foreignKey.ReferencedSchema))
        {
            return;
        }

        var target = $"{foreignKey.ReferencedSchema}.{foreignKey.ReferencedTable}";
        if (!tablesByKey.TryGetValue(Key(foreignKey.ReferencedSchema, foreignKey.ReferencedTable), out var referencedTable))
        {
            if (!partialSchemas.Contains(foreignKey.ReferencedSchema))
            {
                diagnostics.Add(Error($"Foreign key '{foreignKey.Name}' on '{qualified}' references unknown table '{target}'."));
            }

            return;
        }

        var referencedColumns = new HashSet<string>(referencedTable.Columns.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
        var missingReferenced = foreignKey.ReferencedColumnNames.Where(c => !referencedColumns.Contains(c)).ToList();
        foreach (var missing in missingReferenced)
        {
            diagnostics.Add(Error($"Foreign key '{foreignKey.Name}' on '{qualified}' references unknown column '{missing}' on '{target}'."));
        }

        // A foreign key must reference a uniquely-constrained set of columns; check only once the target columns resolve.
        if (missingReferenced.Count == 0 && !IsUniquelyConstrained(referencedTable, foreignKey.ReferencedColumnNames))
        {
            diagnostics.Add(Error(
                $"Foreign key '{foreignKey.Name}' on '{qualified}' references columns ({string.Join(", ", foreignKey.ReferencedColumnNames)}) " +
                $"on '{target}' that are not the primary key or a unique index."));
        }
    }

    private static bool IsUniquelyConstrained(Table table, IReadOnlyList<string> columnNames)
    {
        var referenced = new HashSet<string>(columnNames, StringComparer.OrdinalIgnoreCase);

        if (table.PrimaryKey is { } primaryKey && referenced.SetEquals(primaryKey.ColumnNames))
        {
            return true;
        }

        // A partial (predicated) unique index cannot back a foreign key, so it does not count.
        return table.Indexes.Any(i => i is { IsUnique: true, Predicate: null } && referenced.SetEquals(i.ColumnNames));
    }

    private static IEnumerable<string> Duplicates(IEnumerable<string> names) => names
        .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key);

    // The NUL character cannot appear in an identifier, so it is a safe composite-key separator even for quoted names.
    private static string Key(string schema, string table) => $"{schema.ToLowerInvariant()}\0{table.ToLowerInvariant()}";

    private static PolicyDiagnostic Error(string message) => PolicyDiagnostic.Error(PolicyName, message);
}
