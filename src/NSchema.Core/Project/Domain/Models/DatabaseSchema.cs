using System.Diagnostics;
using NSchema.Project.Domain.Models.Extensions;
using NSchema.Project.Domain.Models.Schemas;

namespace NSchema.Project.Domain.Models;

/// <summary>
/// Represents the overall structure of a database schema.
/// </summary>
/// <param name="Schemas">A list of SchemaDefinition objects, each representing a specific schema within the database.</param>
/// <param name="Extensions">A list of database-global extensions.</param>
[DebuggerDisplay("{Schemas.Count} schemas")]
public record DatabaseSchema(IReadOnlyList<SchemaDefinition>? Schemas = null, IReadOnlyList<Extension>? Extensions = null)
{
    /// <summary>
    /// A list of SchemaDefinition objects, each representing a specific schema within the database.
    /// </summary>
    public IReadOnlyList<SchemaDefinition> Schemas { get; init; } = Schemas ?? [];

    /// <summary>
    /// A list of database-global extensions. Extensions are not schema-scoped, so they live at the root of the
    /// database schema rather than inside a <see cref="SchemaDefinition"/>.
    /// </summary>
    public IReadOnlyList<Extension> Extensions { get; init; } = Extensions ?? [];
}
