using NSchema.Diagnostics;
using NSchema.State;
using NSchema.State.Model;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.State;

public sealed class StateLockCoordinatorTests
{
    private readonly RecordingStateLock _stateLock = new();

    private static Task<Result<IStateLockHandle>> Acquire(IStateLock? stateLock, bool skipLock) =>
        new StateLockCoordinator(stateLock).Acquire("apply", skipLock, TestContext.Current.CancellationToken);

    [Fact]
    public async Task NoLockBackend_SucceedsWithTheNoOpHandle_AndSaysNothing()
    {
        // An offline run has nothing to lock — a no-op handle, no warning.
        var result = await Acquire(stateLock: null, skipLock: false);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(NullStateLockHandle.Instance);
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task SkipLock_SucceedsWithTheNoOpHandleAndAWarning_WithoutAcquiring()
    {
        var result = await Acquire(_stateLock, skipLock: true);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(NullStateLockHandle.Instance);
        result.Diagnostics.ShouldHaveSingleItem().Severity.ShouldBe(DiagnosticSeverity.Warning);
        // Peeked to name the lock it ran past, but never acquired.
        _stateLock.Peeks.ShouldBe(1);
        _stateLock.Acquisitions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Acquire_SucceedsWithTheRealHandle()
    {
        var result = await Acquire(_stateLock, skipLock: false);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(NullStateLockHandle.Instance);
        result.Diagnostics.ShouldBeEmpty();
        _stateLock.Acquisitions.ShouldHaveSingleItem().Operation.ShouldBe("apply");
    }

    [Fact]
    public async Task Contention_IsAFailureCarryingTheHolderDetails()
    {
        // The lock is already held by another operation — a recoverable, user-facing failure, not a thrown exception.
        _stateLock.OnAcquire = _ => throw new StateLockedException(
            "held", new StateLockInfo("id", "plan", "other@host", DateTimeOffset.UnixEpoch));

        var result = await Acquire(_stateLock, skipLock: false);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldSatisfyAllConditions(
            m => m.ShouldContain("locked by other@host"),
            m => m.ShouldContain("--no-lock"));
    }

    [Fact]
    public async Task ReturnedHandle_ReleasesTheLock()
    {
        // Release is explicit (the handle is not disposable — a manual lock can outlive the process).
        var handle = (await Acquire(_stateLock, skipLock: false)).Value.ShouldNotBeNull();

        await handle.Release(TestContext.Current.CancellationToken);

        _stateLock.Released.ShouldBe(1);
    }
}
