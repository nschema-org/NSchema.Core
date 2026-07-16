using System.Diagnostics;
using NSchema.Model.Extensions;
using NSchema.Model.Schemas;

namespace NSchema.Model;

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

    /// <summary>
    /// Returns a new database model restricted to the current scope.
    /// </summary>
    public Database ScopedTo(PlanningScope scope)
    {
        // Extensions are database-global, not schema-scoped, so they pass through a namespace filter untouched:
        // an extension is a prerequisite of the whole database regardless of which schemas are in scope.
        if (scope.IsAll)
        {
            return this;
        }

        var filtered = Schemas.Where(s => scope.Includes(s.Name)).ToList();
        return new Database(filtered, Extensions);
    }
}
