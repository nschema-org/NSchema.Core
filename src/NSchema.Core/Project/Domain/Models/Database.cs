using System.Diagnostics;
using NSchema.Project.Domain.Models.Extensions;
using NSchema.Project.Domain.Models.Schemas;

namespace NSchema.Project.Domain.Models;

/// <summary>
/// Represents the overall structure of a database.
/// </summary>
/// <param name="Schemas">The schemas declared in the database.</param>
/// <param name="Extensions">Any extensions that are installed in the database.</param>
[DebuggerDisplay("{Schemas.Count} schemas")]
public record Database(IReadOnlyList<Schema>? Schemas = null, IReadOnlyList<Extension>? Extensions = null)
{
    /// <summary>
    /// A list of Schema objects, each representing a specific schema within the database.
    /// </summary>
    public IReadOnlyList<Schema> Schemas { get; init; } = Schemas ?? [];

    /// <summary>
    /// A list of database-global extensions. Extensions are not schema-scoped, so they live at the root of the
    /// database schema rather than inside a <see cref="Schema"/>.
    /// </summary>
    public IReadOnlyList<Extension> Extensions { get; init; } = Extensions ?? [];
}
