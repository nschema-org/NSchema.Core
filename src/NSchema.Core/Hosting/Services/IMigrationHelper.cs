using NSchema.Plan.Model;
using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.Hosting.Services;

internal interface IMigrationHelper
{
    bool HasStore { get; }

    /// <summary>
    /// Validates the desired schema against the schema policies, throwing on errors.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The loaded, validated desired schema.</returns>
    Task<DatabaseSchema> Validate(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the desired and current schemas, computes the migration plan, and reports it.
    /// </summary>
    /// <param name="currentSource">Which source to read the current schema from.</param>
    /// <param name="required">Whether <paramref name="currentSource"/> must be available, or may fall back to the other source.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<MigrationPlan> Plan(SchemaSourceMode currentSource, bool required, CancellationToken cancellationToken = default);

    Task Refresh(CancellationToken cancellationToken = default);
}
