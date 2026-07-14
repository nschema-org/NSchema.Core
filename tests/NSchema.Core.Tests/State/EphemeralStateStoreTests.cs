using NSchema.State.Locks;
using NSchema.State.Backends;

namespace NSchema.Tests.State;

public sealed class EphemeralStateStoreTests
{
    private readonly EphemeralStateStore _sut = new();

    [Fact]
    public async Task Read_BeforeAnyWrite_ReturnsNull()
        => (await _sut.Read(TestContext.Current.CancellationToken)).ShouldBeNull();

    [Fact]
    public async Task Write_ThenRead_RoundTripsThePayload()
    {
        // Arrange
        byte[] payload = [1, 2, 3];

        // Act
        await _sut.Write(payload, TestContext.Current.CancellationToken);

        // Assert
        (await _sut.Read(TestContext.Current.CancellationToken))!.Value.ToArray().ShouldBe(payload);
    }

    [Fact]
    public async Task Write_CopiesThePayload_SoLaterMutationDoesNotLeakIn()
    {
        // Arrange
        byte[] payload = [1, 2, 3];
        await _sut.Write(payload, TestContext.Current.CancellationToken);

        // Act
        payload[0] = 9;

        // Assert
        (await _sut.Read(TestContext.Current.CancellationToken))!.Value.ToArray().ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Acquire_WhenAlreadyHeld_ThrowsWithTheHoldersInfo()
    {
        // Arrange
        var handle = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);

        // Act & Assert
        var locked = await Should.ThrowAsync<StateLockedException>(
            () => _sut.Acquire(new StateLockRequest("refresh"), TestContext.Current.CancellationToken));
        locked.ExistingLock.ShouldBe(handle.Info);
    }

    [Fact]
    public async Task Release_OnTheHandle_AllowsReacquisition()
    {
        // Arrange
        var handle = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);

        // Act
        await handle.Release(TestContext.Current.CancellationToken);

        // Assert — and releasing again is a no-op.
        await handle.Release(TestContext.Current.CancellationToken);
        (await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Release_OnAStaleHandle_DoesNotDropSomeoneElsesLock()
    {
        // Arrange — the first hold is force-released and the lock re-acquired by another operation.
        var stale = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);
        await _sut.Release(TestContext.Current.CancellationToken);
        var current = await _sut.Acquire(new StateLockRequest("refresh"), TestContext.Current.CancellationToken);

        // Act
        await stale.Release(TestContext.Current.CancellationToken);

        // Assert — the new hold survives the stale handle's release.
        (await _sut.Peek(TestContext.Current.CancellationToken)).ShouldBe(current.Info);
    }

    [Fact]
    public async Task Peek_ReportsTheHold_WithoutAcquiring()
    {
        // Arrange
        (await _sut.Peek(TestContext.Current.CancellationToken)).ShouldBeNull();
        var handle = await _sut.Acquire(new StateLockRequest("apply"), TestContext.Current.CancellationToken);

        // Act & Assert
        (await _sut.Peek(TestContext.Current.CancellationToken)).ShouldBe(handle.Info);
    }
}
