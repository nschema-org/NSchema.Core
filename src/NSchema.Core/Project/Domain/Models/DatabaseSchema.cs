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
    IReadOnlyList<SqlIdentifier>? DroppedSchemas = null,
    IReadOnlyList<Extension>? Extensions = null,
    IReadOnlyList<SqlIdentifier>? DroppedExtensions = null
)
{
    /// <summary>
    /// A list of SchemaDefinition objects, each representing a specific schema within the database.
    /// </summary>
    public IReadOnlyList<SchemaDefinition> Schemas { get; init; } = Schemas ?? [];

    /// <summary>
    /// A list of schema names that have been dropped from the database.
    /// </summary>
    public IReadOnlyList<SqlIdentifier> DroppedSchemas { get; init; } = DroppedSchemas ?? [];

    /// <summary>
    /// A list of database-global extensions. Extensions are not schema-scoped, so they live at the root of the
    /// database schema rather than inside a <see cref="SchemaDefinition"/>.
    /// </summary>
    public IReadOnlyList<Extension> Extensions { get; init; } = Extensions ?? [];

    /// <summary>
    /// A list of extension names that have been dropped from the database.
    /// </summary>
    public IReadOnlyList<SqlIdentifier> DroppedExtensions { get; init; } = DroppedExtensions ?? [];

    /// <summary>
    /// Gets a combined list of all schema names, including both existing schemas and those that have been dropped.
    /// </summary>
    public SqlIdentifier[] AllSchemaNames => Schemas
        .Select(s => s.Name)
        .Concat(DroppedSchemas)
        .Distinct()
        .ToArray();

}
