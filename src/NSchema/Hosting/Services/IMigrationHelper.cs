using NSchema.Migration;
using NSchema.Migration.Plan;

namespace NSchema.Hosting.Services;

internal interface IMigrationHelper
{
    bool HasStore { get; }

    /// <summary>
    /// Loads the desired and current schemas, computes the migration plan, and reports it.
    /// </summary>
    /// <param name="currentSource">Which source to read the current schema from.</param>
    /// <param name="required">Whether <paramref name="currentSource"/> must be available, or may fall back to the other source.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<MigrationPlan> Prepare(SchemaSourceMode currentSource, bool required, CancellationToken cancellationToken = default);

    Task Refresh(CancellationToken cancellationToken = default);
}
