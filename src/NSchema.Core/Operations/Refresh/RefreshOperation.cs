using NSchema.Operations.Services;
using NSchema.Resolution;

namespace NSchema.Operations.Refresh;

internal sealed class RefreshOperation(IMigrationHelper helper, IKeyedResolver<IOperationReporter> reporters) : IRefreshOperation
{
    public async Task Execute(RefreshArguments arguments, CancellationToken cancellationToken = default)
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
