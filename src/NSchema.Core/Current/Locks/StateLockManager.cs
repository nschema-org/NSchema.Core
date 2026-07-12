using NSchema.Current.Locks.Backends;

namespace NSchema.Current.Locks;

/// <summary>
/// The default <see cref="IStateLockManager"/>.
/// </summary>
/// <remarks>
/// Wraps the configured <see cref="IStateLock"/> (when any) with the offline / <c>--no-lock</c> / contention handling,
/// returning a handle the caller releases when done.
/// </remarks>
internal sealed class StateLockManager(IStateLock? stateLock = null) : IStateLockManager
{
    public async Task<StateLockInfo?> Peek(CancellationToken cancellationToken = default) =>
        stateLock is null ? null : await stateLock.Peek(cancellationToken);

    public async Task<Result<IStateLockHandle>> Acquire(AcquireLockArguments arguments, CancellationToken cancellationToken = default)
    {
        // No backend lock to take — this is an ordinary offline run, not a deliberate skip, so say nothing.
        if (stateLock is null)
        {
            return Result.Success<IStateLockHandle>(NullStateLockHandle.Instance);
        }

        if (arguments.SkipLock)
        {
            // Peek so the warning is honest: name the lock we are running past rather than ignoring it silently.
            var held = await stateLock.Peek(cancellationToken);
            return Result.From<IStateLockHandle>(NullStateLockHandle.Instance, [LockDiagnostics.RunningUnlocked(arguments.Operation, held)]);
        }

        try
        {
            return Result.Success<IStateLockHandle>(await stateLock.Acquire(new StateLockRequest(arguments.Operation, arguments.TimeToLive), cancellationToken));
        }
        catch (StateLockedException ex)
        {
            // Contention is a recoverable, user-facing outcome, not a bug — surface it as a failure the caller renders.
            return Result.Failure<IStateLockHandle>(LockDiagnostics.StateLocked(arguments.Operation, ex));
        }
    }

    public async Task<StateLockInfo?> Release(CancellationToken cancellationToken = default)
    {
        // Nothing to release when the state is unlockable.
        if (stateLock is null)
        {
            return null;
        }

        // Capture what is held so the caller can report it, then force-release. A null peek means the state was already
        // free — there is nothing to remove.
        var held = await stateLock.Peek(cancellationToken);
        if (held is null)
        {
            return null;
        }

        await stateLock.Release(cancellationToken);
        return held;
    }
}
