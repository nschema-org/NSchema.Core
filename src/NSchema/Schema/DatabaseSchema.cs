using System.Diagnostics;

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
}
