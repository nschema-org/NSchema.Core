using NSchema.Diff.Domain.Models;
using NSchema.Plan.Domain;
using NSchema.Project.Domain.Models;

namespace NSchema.Diff.Domain;

/// <summary>
/// Defines a contract for comparing two database schemas and producing the structured <see cref="DatabaseDiff"/>
/// that describes the changes needed to transform the current schema into the desired schema.
/// </summary>
internal interface IDatabaseComparer
{
    /// <summary>
    /// Compares the current database schema with the desired database schema and produces the structured diff
    /// describing the changes needed to transform the current schema into the desired schema. The diff is then
    /// linearized into an executable plan by <see cref="IPlanLinearizer"/>.
    /// </summary>
    /// <param name="current">The current database schema representing the existing state of the database.</param>
    /// <param name="desired">The desired database schema representing the target state of the database after migration.</param>
    /// <param name="directives">The project's management directives.</param>
    /// <returns>The structured diff describing the changes between the two schemas.</returns>
    DatabaseDiff Compare(Database current, Database desired, ProjectDirectives directives);
}
