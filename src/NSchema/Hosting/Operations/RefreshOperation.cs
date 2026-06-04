using NSchema.Hosting.Services;
using NSchema.Migration;

namespace NSchema.Hosting.Operations;

internal sealed class RefreshOperation(IMigrationHelper helper, IMigrationReporterResolver reporter) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (!helper.HasStore)
        {
            throw new InvalidOperationException("Unable to perform refresh without configured state store.");
        }

        reporter.Current.Info("Refreshing state store...");
        await helper.Refresh(cancellationToken);
        reporter.Current.Info("State store refreshed successfully.");
    }
}
