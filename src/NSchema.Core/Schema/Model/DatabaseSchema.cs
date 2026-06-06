using System.Diagnostics;
using System.Text.Json.Serialization;

namespace NSchema.Schema.Model;

/// <summary>
/// Represents the overall structure of a database schema.
/// </summary>
/// <param name="Schemas">A list of SchemaDefinition objects, each representing a specific schema within the database.</param>
/// <param name="DroppedSchemas">A list of schema names that have been dropped from the database.</param>
[DebuggerDisplay("{Schemas.Count} schemas")]
public record DatabaseSchema(IReadOnlyList<SchemaDefinition> Schemas, IReadOnlyList<string> DroppedSchemas)
{
    /// <summary>
    /// A list of SchemaDefinition objects, each representing a specific schema within the database.
    /// </summary>
    public IReadOnlyList<SchemaDefinition> Schemas { get; init; } = Schemas ?? [];

    /// <summary>
    /// A list of schema names that have been dropped from the database.
    /// </summary>
    public IReadOnlyList<string> DroppedSchemas { get; init; } = DroppedSchemas ?? [];

    /// <summary>
    /// Gets a combined list of all schema names, including both existing schemas and those that have been dropped.
    /// </summary>
    [JsonIgnore]
    public string[] AllSchemaNames => Schemas
        .Select(s => s.Name)
        .Concat(DroppedSchemas)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// Creates a new <see cref="DatabaseSchema"/> with the given options, defaulting unspecified members.
    /// </summary>
    /// <param name="schemas">A list of SchemaDefinition objects, each representing a specific schema within the database.</param>
    /// <param name="droppedSchemas">A list of schema names that have been dropped from the database.</param>
    public static DatabaseSchema Create(IReadOnlyList<SchemaDefinition> schemas, IReadOnlyList<string>? droppedSchemas = null) => new(schemas, droppedSchemas ?? []);

    /// <summary>
    /// Restricts the schema to tables matching the given names across all schema namespaces.
    /// Schema namespaces with no matching tables are excluded from the result.
    /// </summary>
    /// <param name="tableNames">The names of the tables to include.</param>
    public DatabaseSchema FilterTables(string[] tableNames)
    {
        var scope = new HashSet<string>(tableNames, StringComparer.OrdinalIgnoreCase);
        var filtered = Schemas
            .Select(s => s with { Tables = s.Tables.Where(t => scope.Contains(t.Name)).ToList() })
            .Where(s => s.Tables.Count > 0)
            .ToList();
        return new DatabaseSchema(filtered, DroppedSchemas);
    }

    /// <summary>
    /// Restricts the schema to the named schemas.
    /// </summary>
    /// <param name="schemaNames">The names of the schemas to include in the filtered result.</param>
    /// <returns>A new <see cref="DatabaseSchema"/> containing only the schemas and dropped schemas that match the provided names.</returns>
    public DatabaseSchema Filter(string[]? schemaNames)
    {
        if (schemaNames is not { Length: > 0 })
        {
            return new DatabaseSchema(Schemas, DroppedSchemas);
        }

        var scope = new HashSet<string>(schemaNames, StringComparer.OrdinalIgnoreCase);
        var filtered = Schemas.Where(s => scope.Contains(s.Name)).ToList();
        var filteredDropped = DroppedSchemas.Where(scope.Contains).ToList();
        return new DatabaseSchema(filtered, filteredDropped);
    }

    /// <summary>
    /// Combines the current <see cref="DatabaseSchema"/> with another.
    /// </summary>
    /// <param name="schema">The schema to combine with.</param>
    /// <returns></returns>
    public DatabaseSchema Combine(DatabaseSchema schema)
    {
        var mergedSchemas = new[] { this, schema }
            .SelectMany(db => db.Schemas)
            .GroupBy(s => s.Name)
            .Select(s => AggregateSchemaGroup(s.ToList()))
            .ToList();

        var droppedSchemas = DroppedSchemas
            .Concat(schema.DroppedSchemas)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DatabaseSchema(mergedSchemas, droppedSchemas);
    }

    private static SchemaDefinition AggregateSchemaGroup(IReadOnlyList<SchemaDefinition> schemas)
    {
        var tables = new List<Table>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var schemaName = schemas[0].Name;
        foreach (var schema in schemas)
        {
            foreach (var table in schema.Tables)
            {
                if (!seen.Add(table.Name))
                {
                    throw new InvalidOperationException($"Duplicate table '{table.Name}' found in schema '{schemaName}'.");
                }

                tables.Add(table);
            }
        }

        var comments = schemas.Select(s => s.Comment).Where(c => c is not null).Distinct().ToList();
        if (comments.Count > 1)
        {
            throw new InvalidOperationException($"Conflicting comments specified for schema '{schemaName}'.");
        }
        var comment = comments.FirstOrDefault();

        var isPartial = schemas.Any(s => s.IsPartial);
        var droppedTables = schemas
            .SelectMany(s => s.DroppedTables)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var oldName = schemas.Select(s => s.OldName).FirstOrDefault(n => n is not null);

        var grants = schemas
            .SelectMany(s => s.Grants)
            .Distinct()
            .ToList();

        return new SchemaDefinition(schemaName, oldName, isPartial, comment, tables, droppedTables, grants);
    }
}
