using NSchema.Operations.Refresh;
using NSchema.Operations.Services;

namespace NSchema.Tests.Operations.Refresh;

public sealed class RefreshOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();

    private RefreshOperation BuildSut() => new(_workflow);

    [Fact]
    public async Task Execute_RefreshesStateRequiringAStore()
    {
        await BuildSut().Execute(new RefreshArguments(), TestContext.Current.CancellationToken);

        await _workflow.Received(1).Refresh(RefreshMode.Required, Arg.Any<CancellationToken>());
    }
}
