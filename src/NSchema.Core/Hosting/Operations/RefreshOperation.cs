using NSchema.Hosting.Services;
using NSchema.Migration;
using NSchema.Resolution;

namespace NSchema.Hosting.Operations;

internal sealed class RefreshOperation(IMigrationHelper helper, IKeyedResolver<IMigrationReporter> reporters) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (!helper.HasStore)
        {
            throw new InvalidOperationException("Unable to perform refresh without configured state store.");
        }

        reporters.Current.Info("Refreshing state store...");
        await helper.Refresh(cancellationToken);
        reporters.Current.Info("State store refreshed successfully.");
    }
}
