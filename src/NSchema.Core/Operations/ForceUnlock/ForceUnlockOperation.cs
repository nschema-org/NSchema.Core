using NSchema.Operations.Confirmation;
using NSchema.Resolution;
using NSchema.State;

namespace NSchema.Operations.ForceUnlock;

internal sealed class ForceUnlockOperation(
    IKeyedResolver<IOperationReporter> reporters,
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
            reporters.Current.Info("Force-unlock cancelled. The state lock was left untouched.");
            return;
        }

        reporters.Current.Info("Forcibly releasing the state lock...");

        var removed = await stateLock.ForceUnlock(cancellationToken);
        reporters.Current.Info(removed is null
            ? "No state lock was held."
            : $"Removed state lock held by {removed.Who} (operation '{removed.Operation}', since {removed.CreatedUtc:u}).");
    }
}
