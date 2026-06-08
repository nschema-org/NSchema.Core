using NSchema.Operations;
using NSchema.Operations.Refresh;
using NSchema.Operations.Services;

namespace NSchema.Tests.Operations.Refresh;

public sealed class RefreshOperationTests
{
    private readonly IMigrationHelper _helper = Substitute.For<IMigrationHelper>();
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();

    private RefreshOperation BuildSut() => new(_helper, Helpers.TestReporters.ResolverFor(_reporter));

    [Fact]
    public async Task Execute_WithStore_DelegatesToHelperRefresh()
    {
        _helper.HasStore.Returns(true);

        await BuildSut().Execute(new RefreshArguments(), TestContext.Current.CancellationToken);

        await _helper.Received(1).Refresh(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoStore_Throws()
    {
        _helper.HasStore.Returns(false);

        await Should.ThrowAsync<InvalidOperationException>(() => BuildSut().Execute(new RefreshArguments()));
        await _helper.DidNotReceive().Refresh(Arg.Any<CancellationToken>());
    }
}
