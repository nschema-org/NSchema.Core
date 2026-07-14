using Microsoft.Extensions.Options;
using NSchema.State.Locks;
using NSchema.State.Locks.Backends;

namespace NSchema.Tests.State;

public sealed class FileStateLockTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"nschema-lock-{Guid.NewGuid():N}");
    private readonly string _path;
    private readonly FileStateLock _sut;

    public FileStateLockTests()
    {
        _path = Path.Combine(_directory, "nested", "state.lock");
        _sut = new FileStateLock(Options.Create(new FileStateLockOptions { Path = _path }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task Acquire_CreatesLockFileAndMissingDirectories()
    {
        var handle = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);

        File.Exists(_path).ShouldBeTrue();
        handle.Info.Id.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Acquire_WithoutTimeToLive_RecordsNoExpiry()
    {
        var handle = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);

        handle.Info.ExpiresUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Acquire_WithTimeToLive_RecordsExpiryRelativeToCreation()
    {
        var ttl = TimeSpan.FromMinutes(30);

        var handle = await _sut.Acquire(new StateLockRequest("manual", ttl), TestContext.Current.CancellationToken);

        handle.Info.ExpiresUtc.ShouldNotBeNull();
        // The expiry is the creation time plus the requested lifetime.
        (handle.Info.ExpiresUtc.Value - handle.Info.CreatedUtc).ShouldBe(ttl);
    }

    [Fact]
    public async Task Acquire_WithTimeToLive_PersistsExpiryForLaterReaders()
    {
        await _sut.Acquire(new StateLockRequest("manual", TimeSpan.FromMinutes(30)), TestContext.Current.CancellationToken);

        // A separate reader (e.g. lock status in another process) sees the recorded expiry.
        var peeked = await _sut.Peek(TestContext.Current.CancellationToken);

        peeked.ShouldNotBeNull();
        peeked.ExpiresUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Release_ReleasesLockFile()
    {
        var handle = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);
        await handle.Release(TestContext.Current.CancellationToken);

        File.Exists(_path).ShouldBeFalse();
    }

    [Fact]
    public async Task Acquire_WhenAlreadyHeld_ThrowsWithHolderInfo()
    {
        await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);

        var ex = await Should.ThrowAsync<StateLockedException>(
            () => _sut.Acquire(new StateLockRequest("destroy")));

        ex.ExistingLock.ShouldNotBeNull();
        ex.ExistingLock.Operation.ShouldBe("apply");
        ex.Message.ShouldContain(_path);
    }

    [Fact]
    public async Task Acquire_AfterRelease_Succeeds()
    {
        var first = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);
        await first.Release(TestContext.Current.CancellationToken);

        // Should not throw now that the first lock is released.
        await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);
        File.Exists(_path).ShouldBeTrue();
    }

    [Fact]
    public async Task Release_IsIdempotent()
    {
        var handle = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);

        await handle.Release(TestContext.Current.CancellationToken);
        await Should.NotThrowAsync(async () => await handle.Release(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NeverReleasing_LeavesTheLockHeld()
    {
        // A manual hold acquires and intentionally never releases, so the lock outlives the handle.
        await _sut.Acquire(new StateLockRequest("manual"), TestContext.Current.CancellationToken);

        File.Exists(_path).ShouldBeTrue();
        var stillHeld = await _sut.Peek(TestContext.Current.CancellationToken);
        stillHeld.ShouldNotBeNull();
    }

    [Fact]
    public async Task Release_RemovesAHeldLock()
    {
        // A handle is held but we forcibly release it (as if from another process recovering a stale lock).
        await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);

        await _sut.Release(TestContext.Current.CancellationToken);

        File.Exists(_path).ShouldBeFalse();
        (await _sut.Peek(TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Release_WhenNothingHeld_DoesNothing()
        => await Should.NotThrowAsync(async () => await _sut.Release(TestContext.Current.CancellationToken));

    [Fact]
    public async Task Release_ThenAcquire_Succeeds()
    {
        await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);
        await _sut.Release(TestContext.Current.CancellationToken);

        await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);
        File.Exists(_path).ShouldBeTrue();
    }

    [Fact]
    public async Task Release_DoesNotDeleteALockHeldByAnother()
    {
        // Acquire, then simulate a force-unlock (the file is removed by hand) and a fresh acquire by another
        // holder. Releasing the first handle must leave the second holder's lock alone — the file now records a
        // different lock id.
        var first = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);
        File.Delete(_path);
        var second = await _sut.Acquire(new StateLockRequest("destroy"), TestContext.Current.CancellationToken);

        await first.Release(TestContext.Current.CancellationToken);

        File.Exists(_path).ShouldBeTrue();
        await second.Release(TestContext.Current.CancellationToken);
    }
}
