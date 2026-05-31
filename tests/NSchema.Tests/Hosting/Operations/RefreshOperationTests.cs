using NSchema.Hosting;
using NSchema.Hosting.Operations;

namespace NSchema.Tests.Hosting.Operations;

public sealed class RefreshOperationTests
{
    private readonly IStateCapturer _stateCapturer = Substitute.For<IStateCapturer>();

    [Fact]
    public async Task Execute_CapturesStateWithoutPlanning()
    {
        _stateCapturer.Capture(Arg.Any<CancellationToken>()).Returns(true);
        var sut = new RefreshOperation(_stateCapturer);

        await sut.Execute();

        await _stateCapturer.Received(1).Capture(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoStateStore_Throws()
    {
        _stateCapturer.Capture(Arg.Any<CancellationToken>()).Returns(false);
        var sut = new RefreshOperation(_stateCapturer);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Execute());
    }
}
