using NSchema.Operations.Confirmation;
using NSchema.State;
using NSchema.State.Model;

namespace NSchema.Operations.ForceUnlock;

internal sealed class ForceUnlockOperation(
    IOperationReporter reporter,
    IOperationConfirmation confirmation,
    IStateLock stateLock
) : IForceUnlockOperation
{
    public async Task Execute(ForceUnlockArguments arguments, CancellationToken cancellationToken = default)
    {
        // When a specific lock id is named, verify it against the held lock before doing anything:
        // this guards against removing a *different* lock that was acquired after the caller read the id they targeted.
        if (arguments.ExpectedLockId is { } expected)
        {
            var current = await stateLock.Peek(cancellationToken);
            if (current is null)
            {
                reporter.Announce("No state lock was held.");
                return;
            }

            if (current.Id != expected)
            {
                throw new StateLockMismatchException(expected, current);
            }
        }

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
