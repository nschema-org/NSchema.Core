using System.Diagnostics;
using System.Text.Json.Serialization;

namespace NSchema.Schema;

/// <summary>
/// Represents the overall structure of a database schema.
/// </summary>
/// <param name="Schemas">A list of SchemaDefinition objects, each representing a specific schema within the database.</param>
/// <param name="DroppedSchemas">A list of schema names that have been dropped from the database.</param>
[DebuggerDisplay("{Schemas.Count} schemas")]
public record DatabaseSchema(IReadOnlyList<SchemaDefinition> Schemas, IReadOnlyList<string> DroppedSchemas)
{
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
    /// Restricts the schema to the named schemas.
    /// </summary>
    /// <param name="schemaNames">The names of the schemas to include in the filtered result.</param>
    /// <returns>A new <see cref="DatabaseSchema"/> containing only the schemas and dropped schemas that match the provided names.</returns>
    public DatabaseSchema Filter(string[]? schemaNames)
    {
        if (schemaNames is not { Length: > 0 })
        {
            return new  DatabaseSchema(Schemas, DroppedSchemas);
        }

        var scope = new HashSet<string>(schemaNames, StringComparer.OrdinalIgnoreCase);
        var filtered = Schemas.Where(s => scope.Contains(s.Name)).ToList();
        var filteredDropped = DroppedSchemas.Where(scope.Contains).ToList();
        return new DatabaseSchema(filtered, filteredDropped);
    }
}
