using NSchema.Diagnostics;
using NSchema.State.Model;

namespace NSchema.State;

/// <summary>
/// The default <see cref="IStateLockCoordinator"/>.
/// </summary>
/// <remarks>
/// Wraps the configured <see cref="IStateLock"/> (when any) with the offline / <c>--no-lock</c> / contention handling,
/// returning a handle the caller releases when done.
/// </remarks>
internal sealed class StateLockCoordinator(IStateLock? stateLock = null) : IStateLockCoordinator
{
    public async Task<StateLockInfo?> Peek(CancellationToken cancellationToken = default) =>
        stateLock is null ? null : await stateLock.Peek(cancellationToken);

    public async Task<Result<IStateLockHandle>> Acquire(string operation, bool skipLock = false, CancellationToken cancellationToken = default)
    {
        // No backend lock to take — this is an ordinary offline run, not a deliberate skip, so say nothing.
        if (stateLock is null)
        {
            return Result.Success<IStateLockHandle>(NullStateLockHandle.Instance);
        }

        if (skipLock)
        {
            // Peek so the warning is honest: name the lock we are running past rather than ignoring it silently.
            var held = await stateLock.Peek(cancellationToken);
            var warning = Diagnostic.Warning(operation, held is null
                ? "Running without the state lock; make sure no other operation runs against this state at the same time."
                : $"Running without the state lock; the state is currently locked by {held.Who} " +
                  $"(operation '{held.Operation}', since {held.CreatedUtc:u}) — proceeding anyway.");
            return Result.From<IStateLockHandle>(NullStateLockHandle.Instance, [warning]);
        }

        try
        {
            return Result.Success<IStateLockHandle>(await stateLock.Acquire(new StateLockRequest(operation), cancellationToken));
        }
        catch (StateLockedException ex)
        {
            // Contention is a recoverable, user-facing outcome, not a bug — surface it as a failure the caller renders.
            return Result.Failure<IStateLockHandle>(ex.ExistingLock is { } held
                ? Diagnostic.Error(operation,
                    $"The state is locked by {held.Who} (operation '{held.Operation}', since {held.CreatedUtc:u}). " +
                    "Wait for it to finish, or re-run with --no-lock to proceed anyway.")
                : Diagnostic.Error(operation, ex.Message));
        }
    }
}
