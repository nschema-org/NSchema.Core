using NSchema.Diff.Model;
using NSchema.Plan;
using NSchema.Schema.Model;

namespace NSchema.Diff;

/// <summary>
/// Defines a contract for comparing two database schemas and producing the structured <see cref="DatabaseDiff"/>
/// that describes the changes needed to transform the current schema into the desired schema.
/// </summary>
public interface ISchemaComparer
{
    /// <summary>
    /// Compares the current database schema with the desired database schema and produces the structured diff
    /// describing the changes needed to transform the current schema into the desired schema. The diff is then
    /// linearized into an executable plan by <see cref="IPlanLinearizer"/>.
    /// </summary>
    /// <param name="current">The current database schema representing the existing state of the database.</param>
    /// <param name="desired">The desired database schema representing the target state of the database after migration.</param>
    /// <returns>The structured diff describing the changes between the two schemas.</returns>
    DatabaseDiff Compare(DatabaseSchema current, DatabaseSchema desired);
}
