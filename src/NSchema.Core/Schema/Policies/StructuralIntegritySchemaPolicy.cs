using NSchema.Diagnostics;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Tables;

namespace NSchema.Schema.Policies;

/// <summary>
/// Validates that every primary key, index, and foreign key references columns and tables that actually exist within the document.
/// </summary>
internal sealed class StructuralIntegritySchemaPolicy : ISchemaPolicy
{
    private const string PolicyName = "structural-integrity";

    /// <inheritdoc />
    public IEnumerable<Diagnostic> Validate(DatabaseSchema schema)
    {
        var managedSchemas = new HashSet<string>(schema.Schemas.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
        var partialSchemas = new HashSet<string>(
            schema.Schemas.Where(s => s.IsPartial).Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
        var tablesByKey = schema.Schemas
            .SelectMany(s => s.Tables.Select(t => (Key: Key(s.Name, t.Name), Table: t)))
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First().Table);

        var diagnostics = new List<Diagnostic>();
        foreach (var definition in schema.Schemas)
        {
            foreach (var table in definition.Tables)
            {
                ValidateTable(definition, table, managedSchemas, partialSchemas, tablesByKey, diagnostics);
            }

            ValidateObjectNames(definition, diagnostics);
            ValidateRoutineNames(definition, diagnostics);
            ValidateIndexNames(definition, diagnostics);
        }

        return diagnostics;
    }

    // Index names are schema-scoped in the database (indexes live in pg_class alongside tables).
    private static void ValidateIndexNames(SchemaDefinition definition, List<Diagnostic> diagnostics)
    {
        var named = definition.Tables
            .SelectMany(t => t.Indexes.Select(i => (i.Name, Kind: "index", On: t.Name))
                .Concat(t.PrimaryKey is { } pk
                    ? [(pk.Name, Kind: "primary key", On: t.Name)]
                    : Array.Empty<(string Name, string Kind, string On)>())
                .Concat(t.UniqueConstraints.Select(u => (u.Name, Kind: "unique constraint", On: t.Name)))
                .Concat(t.ExclusionConstraints.Select(x => (x.Name, Kind: "exclusion constraint", On: t.Name))))
            .Concat(definition.Views.Where(v => v.IsMaterialized)
                .SelectMany(v => v.Indexes.Select(i => (i.Name, Kind: "index", On: v.Name))));

        foreach (var collision in named.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            var sites = string.Join(", ", collision.Select(x => $"{x.Kind} on '{definition.Name}.{x.On}'"));
            diagnostics.Add(Error(
                $"Schema '{definition.Name}' declares the index name '{collision.Key}' more than once ({sites}); " +
                "index and index-backed constraint names are schema-scoped."));
        }
    }

    // Tables, views, materialized views, sequences, and composite types all occupy a single name space per
    // schema in the database (Postgres's pg_class), and they additionally share pg_type with enums and domains
    // (every relation has a row type), so none of these kinds may reuse a name within a schema — a table and a
    // view called 'foo' cannot coexist. Routines live in a separate name space (pg_proc) and are checked apart.
    private static void ValidateObjectNames(SchemaDefinition definition, List<Diagnostic> diagnostics)
    {
        var named = definition.Tables.Select(t => (t.Name, Kind: "table"))
            .Concat(definition.Views.Select(v => (v.Name, Kind: v.IsMaterialized ? "materialized view" : "view")))
            .Concat(definition.Sequences.Select(s => (s.Name, Kind: "sequence")))
            .Concat(definition.CompositeTypes.Select(c => (c.Name, Kind: "composite type")))
            .Concat(definition.Enums.Select(e => (e.Name, Kind: "enum")))
            .Concat(definition.Domains.Select(d => (d.Name, Kind: "domain")));

        foreach (var collision in named.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            var kinds = collision.Select(x => x.Kind).ToList();
            // A single kind appearing twice (e.g. two sequences) reads as a plain duplicate; a mix of kinds reads
            // as a name-space collision. Either way the database would reject it.
            diagnostics.Add(kinds.Distinct().Count() == 1
                ? Error($"Schema '{definition.Name}' declares {kinds[0]} '{collision.Key}' more than once.")
                : Error($"Schema '{definition.Name}' reuses the name '{collision.Key}' across object kinds that share a name space ({string.Join(", ", kinds.OrderBy(k => k, StringComparer.Ordinal))})."));
        }
    }

    // Functions and procedures share one name space, as they do in the database, so they live in a single
    // routine list; a single duplicate-name check covers both same-kind duplicates and function/procedure
    // collisions. The DDL parser and document aggregation enforce this for parsed schemas; this is the catch-all
    // for code-built ones.
    private static void ValidateRoutineNames(SchemaDefinition definition, List<Diagnostic> diagnostics)
    {
        foreach (var duplicate in Duplicates(definition.Routines.Select(r => r.Name)))
        {
            diagnostics.Add(Error(
                $"Schema '{definition.Name}' declares routine '{duplicate}' more than once " +
                "(functions and procedures share a single name space)."));
        }
    }

    private static void ValidateTable(
        SchemaDefinition definition,
        Table table,
        HashSet<string> managedSchemas,
        HashSet<string> partialSchemas,
        IReadOnlyDictionary<string, Table> tablesByKey,
        List<Diagnostic> diagnostics)
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

        // A generated column is computed from an expression, so it cannot also carry a default — the database
        // rejects a column that declares both.
        foreach (var column in table.Columns.Where(c => c.DefaultExpression is not null && c.GeneratedExpression is not null))
        {
            diagnostics.Add(Error($"Column '{qualified}.{column.Name}' has both a DEFAULT and a GENERATED expression; a generated column cannot have a default."));
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
            // Only plain-column keys (and covering INCLUDE columns) reference table columns directly; an
            // expression key (e.g. (lower(email))) names columns inside opaque text we don't parse.
            var referenced = index.Columns.Where(c => !c.IsExpression).Select(c => c.Expression).Concat(index.Include);
            foreach (var missing in referenced.Where(c => !columns.Contains(c)))
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
        List<Diagnostic> diagnostics)
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

        // A partial (predicated) unique index cannot back a foreign key, and an expression index cannot either
        // (its keys aren't plain columns), so neither counts. INCLUDE columns aren't part of the uniqueness key,
        // so they don't affect the match.
        return table.Indexes.Any(i => i is { IsUnique: true, Predicate: null }
            && i.Columns.All(c => !c.IsExpression)
            && referenced.SetEquals(i.Columns.Select(c => c.Expression)));
    }

    private static IEnumerable<string> Duplicates(IEnumerable<string> names) => names
        .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key);

    // The NUL character cannot appear in an identifier, so it is a safe composite-key separator even for quoted names.
    private static string Key(string schema, string table) => $"{schema.ToLowerInvariant()}\0{table.ToLowerInvariant()}";

    private static Diagnostic Error(string message) => Diagnostic.Error(PolicyName, message);
}
