using NSchema.Resolution;
using NSchema.State;

namespace NSchema.Operations.ForceUnlock;

internal sealed class ForceUnlockOperation(IKeyedResolver<IOperationReporter> reporters, IStateLock stateLock) : IForceUnlockOperation
{
    public async Task Execute(ForceUnlockArguments arguments, CancellationToken cancellationToken = default)
    {
        reporters.Current.Info("Forcibly releasing the state lock...");

        var removed = await stateLock.ForceUnlock(cancellationToken);
        reporters.Current.Info(removed is null
            ? "No state lock was held."
            : $"Removed state lock held by {removed.Who} (operation '{removed.Operation}', since {removed.CreatedUtc:u}).");
    }
}
