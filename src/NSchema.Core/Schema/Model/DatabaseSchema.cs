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
}
