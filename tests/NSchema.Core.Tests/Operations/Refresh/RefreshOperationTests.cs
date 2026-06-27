using NSchema.Operations;
using NSchema.Operations.Refresh;
using NSchema.Operations.Services;
using NSchema.State.Model;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Operations.Refresh;

public sealed class RefreshOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly RecordingStateLock _stateLock = new();
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();

    private RefreshOperation BuildSut() => new(_workflow, _reporter, _stateLock);

    [Fact]
    public async Task Execute_RefreshesStateRequiringAStore()
    {
        await BuildSut().Execute(new RefreshArguments(), TestContext.Current.CancellationToken);

        await _workflow.Received(1).Refresh(RefreshMode.Required, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_AcquiresAndReleasesStateLock()
    {
        await BuildSut().Execute(new RefreshArguments(), TestContext.Current.CancellationToken);

        _stateLock.Acquisitions.ShouldHaveSingleItem().Operation.ShouldBe("refresh");
        _stateLock.Released.ShouldBe(1);
    }

    [Fact]
    public async Task Execute_StateLocked_DoesNotRefresh()
    {
        _stateLock.OnAcquire = _ => throw new StateLockedException("locked");

        await Should.ThrowAsync<StateLockedException>(() => BuildSut().Execute(new RefreshArguments()));

        await _workflow.DidNotReceive().Refresh(Arg.Any<RefreshMode>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithSkipLock_RefreshesWithoutAcquiringTheLock()
    {
        await BuildSut().Execute(new RefreshArguments { SkipLock = true }, TestContext.Current.CancellationToken);

        // --no-lock: the lock is never taken, but the refresh still runs.
        _stateLock.Acquisitions.ShouldBeEmpty();
        _stateLock.Released.ShouldBe(0);
        await _workflow.Received(1).Refresh(RefreshMode.Required, Arg.Any<CancellationToken>());
    }
}
