using NSchema.Operations.Confirmation;
using NSchema.State;

namespace NSchema.Operations.ForceUnlock;

internal sealed class ForceUnlockOperation(
    IOperationReporter reporter,
    IOperationConfirmation confirmation,
    IStateLock stateLock
) : IForceUnlockOperation
{
    public async Task Execute(ForceUnlockArguments arguments, CancellationToken cancellationToken = default)
    {
        // Offer an interactive front-end the chance to prompt before overriding the lock — forcibly unlocking while
        // another operation legitimately holds the lock can corrupt the shared state.
        if (!await confirmation.Confirm(new ForceUnlockConfirmationRequest(), cancellationToken))
        {
            reporter.Announce("Force-unlock cancelled. The state lock was left untouched.");
            return;
        }

        reporter.Progress("Forcibly releasing the state lock...");

        var removed = await stateLock.ForceUnlock(cancellationToken);
        if (removed is null)
        {
            reporter.Announce("No state lock was held.");
        }
        else
        {
            reporter.Success($"Removed state lock held by {removed.Who} (operation '{removed.Operation}', since {removed.CreatedUtc:u}).");
        }
    }
}
