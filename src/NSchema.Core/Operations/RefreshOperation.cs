using NSchema.Migration;
using NSchema.Operations.Services;
using NSchema.Resolution;

namespace NSchema.Operations;

internal sealed class RefreshOperation(IMigrationHelper helper, IKeyedResolver<IMigrationReporter> reporters) : INSchemaOperation
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
