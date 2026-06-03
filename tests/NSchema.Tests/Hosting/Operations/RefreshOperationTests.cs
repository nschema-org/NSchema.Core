using NSchema.Hosting.Operations;
using NSchema.Hosting.Services;
using NSchema.Migration;

namespace NSchema.Tests.Hosting.Operations;

public sealed class RefreshOperationTests
{
    private readonly IMigrationHelper _helper = Substitute.For<IMigrationHelper>();
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();

    private RefreshOperation BuildSut() => new(_helper, _reporter);

    [Fact]
    public async Task Execute_WithStore_DelegatesToHelperRefresh()
    {
        _helper.HasStore.Returns(true);

        await BuildSut().Execute();

        await _helper.Received(1).Refresh(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoStore_Throws()
    {
        _helper.HasStore.Returns(false);

        await Should.ThrowAsync<InvalidOperationException>(() => BuildSut().Execute());
        await _helper.DidNotReceive().Refresh(Arg.Any<CancellationToken>());
    }
}
