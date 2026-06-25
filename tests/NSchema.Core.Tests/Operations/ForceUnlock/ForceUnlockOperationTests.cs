using NSchema.Operations;
using NSchema.Operations.Confirmation;
using NSchema.Operations.ForceUnlock;
using NSchema.State.Model;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Operations.ForceUnlock;

public sealed class ForceUnlockOperationTests
{
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();
    private readonly IOperationConfirmation _confirmation = Substitute.For<IOperationConfirmation>();
    private readonly RecordingStateLock _stateLock = new();

    public ForceUnlockOperationTests()
    {
        _confirmation.Confirm(Arg.Any<OperationConfirmationRequest>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    private ForceUnlockOperation BuildSut() => new(_reporter, _confirmation, _stateLock);

    [Fact]
    public async Task Execute_ForciblyReleasesTheLock()
    {
        await BuildSut().Execute(new ForceUnlockArguments(), TestContext.Current.CancellationToken);

        _stateLock.ForceUnlocks.ShouldBe(1);
    }

    [Fact]
    public async Task Execute_WhenLockWasHeld_ReportsTheRemovedHolder()
    {
        _stateLock.ForceUnlockResult = new StateLockInfo("abc", "apply", "tom@dev", DateTimeOffset.UnixEpoch);

        await BuildSut().Execute(new ForceUnlockArguments(), TestContext.Current.CancellationToken);

        _reporter.Received().Report(MessageKind.Success, Arg.Is<string>(s => s.Contains("tom@dev") && s.Contains("apply")));
    }

    [Fact]
    public async Task Execute_WhenNothingHeld_ReportsNoLock()
    {
        _stateLock.ForceUnlockResult = null;

        await BuildSut().Execute(new ForceUnlockArguments(), TestContext.Current.CancellationToken);

        _reporter.Received().Report(MessageKind.Announcement, "No state lock was held.");
    }

    [Fact]
    public async Task Execute_ConfirmsAsADestructiveAction()
    {
        await BuildSut().Execute(new ForceUnlockArguments(), TestContext.Current.CancellationToken);

        await _confirmation.Received().Confirm(
            Arg.Is<ForceUnlockConfirmationRequest>(r => r.IsDestructive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenNotConfirmed_DoesNotUnlock()
    {
        _confirmation.Confirm(Arg.Any<OperationConfirmationRequest>(), Arg.Any<CancellationToken>()).Returns(false);

        await BuildSut().Execute(new ForceUnlockArguments(), TestContext.Current.CancellationToken);

        _stateLock.ForceUnlocks.ShouldBe(0);
    }

    [Fact]
    public async Task Execute_WithMatchingLockId_ReleasesTheLock()
    {
        // Arrange — the named id matches the held lock, so the force-unlock proceeds.
        _stateLock.PeekResult = new StateLockInfo("abc", "apply", "tom@dev", DateTimeOffset.UnixEpoch);

        // Act
        await BuildSut().Execute(new ForceUnlockArguments { ExpectedLockId = "abc" }, TestContext.Current.CancellationToken);

        // Assert
        _stateLock.ForceUnlocks.ShouldBe(1);
    }

    [Fact]
    public async Task Execute_WithMismatchedLockId_ThrowsAndDoesNotUnlock()
    {
        // Arrange — the held lock differs from the one the caller named (it changed since they read it).
        _stateLock.PeekResult = new StateLockInfo("abc", "apply", "tom@dev", DateTimeOffset.UnixEpoch);

        // Act / Assert — refused as a compare-and-swap safety guard, before any confirmation or removal.
        var ex = await Should.ThrowAsync<StateLockMismatchException>(() =>
            BuildSut().Execute(new ForceUnlockArguments { ExpectedLockId = "xyz" }, TestContext.Current.CancellationToken));
        ex.RequestedLockId.ShouldBe("xyz");
        ex.HeldLock.Id.ShouldBe("abc");
        _stateLock.ForceUnlocks.ShouldBe(0);
        await _confirmation.DidNotReceive().Confirm(Arg.Any<OperationConfirmationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithLockId_WhenNothingHeld_ReportsNoLock()
    {
        // Arrange — the targeted lock is already gone; the desired end state (unlocked) already holds.
        _stateLock.PeekResult = null;

        // Act
        await BuildSut().Execute(new ForceUnlockArguments { ExpectedLockId = "abc" }, TestContext.Current.CancellationToken);

        // Assert
        _reporter.Received().Report(MessageKind.Announcement, "No state lock was held.");
        _stateLock.ForceUnlocks.ShouldBe(0);
    }
}
