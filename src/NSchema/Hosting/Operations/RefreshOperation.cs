using NSchema.Hosting.Services;
using NSchema.Migration;

namespace NSchema.Hosting.Operations;

internal sealed class RefreshOperation(IMigrationHelper helper, IMigrationReporter reporter) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (!helper.HasStore)
        {
            throw new InvalidOperationException("Unable to perform refresh without configured state store.");
        }

        reporter.Info("Refreshing state store...");
        await helper.Refresh(cancellationToken);
        reporter.Info("State store refreshed successfully.");
    }
}
