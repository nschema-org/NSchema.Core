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

    private ForceUnlockOperation BuildSut() => new(TestReporters.ResolverFor(_reporter), _confirmation, _stateLock);

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

        _reporter.Received().Info(Arg.Is<string>(s => s.Contains("tom@dev") && s.Contains("apply")));
    }

    [Fact]
    public async Task Execute_WhenNothingHeld_ReportsNoLock()
    {
        _stateLock.ForceUnlockResult = null;

        await BuildSut().Execute(new ForceUnlockArguments(), TestContext.Current.CancellationToken);

        _reporter.Received().Info("No state lock was held.");
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
}
