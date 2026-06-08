using Microsoft.Extensions.Options;
using NSchema.State;

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
        await using var handle = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);

        File.Exists(_path).ShouldBeTrue();
        handle.LockId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Dispose_ReleasesLockFile()
    {
        var handle = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);
        await handle.DisposeAsync();

        File.Exists(_path).ShouldBeFalse();
    }

    [Fact]
    public async Task Acquire_WhenAlreadyHeld_ThrowsWithHolderInfo()
    {
        await using var first = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);

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
        await first.DisposeAsync();

        // Should not throw now that the first lock is released.
        await using var second = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);
        File.Exists(_path).ShouldBeTrue();
    }

    [Fact]
    public async Task Dispose_IsIdempotent()
    {
        var handle = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);

        await handle.DisposeAsync();
        await Should.NotThrowAsync(async () => await handle.DisposeAsync());
    }

    [Fact]
    public async Task Dispose_DoesNotDeleteALockHeldByAnother()
    {
        // Acquire, then simulate a force-unlock (the file is removed by hand) and a fresh acquire by another
        // holder. Disposing the first handle must leave the second holder's lock alone — the file now records a
        // different lock id.
        var first = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);
        File.Delete(_path);
        var second = await _sut.Acquire(new StateLockRequest("destroy"), TestContext.Current.CancellationToken);

        await first.DisposeAsync();

        File.Exists(_path).ShouldBeTrue();
        await second.DisposeAsync();
    }
}
