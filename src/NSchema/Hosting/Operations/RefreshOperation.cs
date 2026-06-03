using NSchema.Hosting.Services;

namespace NSchema.Hosting.Operations;

internal sealed class RefreshOperation(IMigrationHelper helper) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (!helper.HasStore)
        {
            throw new InvalidOperationException("Unable to perform refresh without configured state store.");
        }

        await helper.Refresh(cancellationToken);
    }
}
