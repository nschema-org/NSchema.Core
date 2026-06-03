using NSchema.Migration.Plan;

namespace NSchema.Hosting.Services;

internal interface IMigrationHelper
{
    bool HasStore { get; }
    Task<MigrationPlan> Prepare(CancellationToken cancellationToken = default);
    Task Refresh(CancellationToken cancellationToken = default);
}
