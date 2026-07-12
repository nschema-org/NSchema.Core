using System.Diagnostics;
using NSchema.Project.Domain.Models.Extensions;
using NSchema.Project.Domain.Models.Schemas;

namespace NSchema.Project.Domain.Models;

/// <summary>
/// Represents the overall structure of a database schema.
/// </summary>
/// <param name="Schemas">A list of SchemaDefinition objects, each representing a specific schema within the database.</param>
/// <param name="DroppedSchemas">A list of schema names that have been dropped from the database.</param>
/// <param name="Extensions">A list of database-global extensions.</param>
/// <param name="DroppedExtensions">A list of extension names that have been dropped from the database.</param>
[DebuggerDisplay("{Schemas.Count} schemas")]
public record DatabaseSchema(
    IReadOnlyList<SchemaDefinition>? Schemas = null,
    IReadOnlyList<string>? DroppedSchemas = null,
    IReadOnlyList<Extension>? Extensions = null,
    IReadOnlyList<string>? DroppedExtensions = null
)
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
    /// A list of database-global extensions. Extensions are not schema-scoped, so they live at the root of the
    /// database schema rather than inside a <see cref="SchemaDefinition"/>.
    /// </summary>
    public IReadOnlyList<Extension> Extensions { get; init; } = Extensions ?? [];

    /// <summary>
    /// A list of extension names that have been dropped from the database.
    /// </summary>
    public IReadOnlyList<string> DroppedExtensions { get; init; } = DroppedExtensions ?? [];

    /// <summary>
    /// Gets a combined list of all schema names, including both existing schemas and those that have been dropped.
    /// </summary>
    public string[] AllSchemaNames => Schemas
        .Select(s => s.Name)
        .Concat(DroppedSchemas)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// Restricts the schema to the named schemas.
    /// </summary>
    /// <param name="schemaNames">The names of the schemas to include in the filtered result.</param>
    /// <returns>A new <see cref="DatabaseSchema"/> containing only the schemas and dropped schemas that match the provided names.</returns>
    public DatabaseSchema Filter(string[]? schemaNames)
    {
        // Extensions are database-global, not schema-scoped, so they pass through a namespace filter untouched:
        // an extension is a prerequisite of the whole database regardless of which schemas are in scope.
        if (schemaNames is not { Length: > 0 })
        {
            return new DatabaseSchema(Schemas, DroppedSchemas, Extensions, DroppedExtensions);
        }

        var scope = new HashSet<string>(schemaNames, StringComparer.OrdinalIgnoreCase);
        var filtered = Schemas.Where(s => scope.Contains(s.Name)).ToList();
        var filteredDropped = DroppedSchemas.Where(scope.Contains).ToList();
        return new DatabaseSchema(filtered, filteredDropped, Extensions, DroppedExtensions);
    }

}
