using NSchema.Operations;
using NSchema.Operations.Services;
using NSchema.Operations.Show;
using NSchema.Schema.Model;

namespace NSchema.Tests.Operations.Show;

public sealed class ShowOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();

    private ShowOperation BuildSut() => new(_workflow, Helpers.TestReporters.ResolverFor(_reporter));

    public ShowOperationTests()
    {
        _workflow.Show(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(DatabaseSchema.Create([]));
    }

    [Fact]
    public async Task Execute_DelegatesToWorkflowShowWithScope()
    {
        var schemas = new[] { "app" };

        await BuildSut().Execute(new ShowArguments { Schemas = schemas }, TestContext.Current.CancellationToken);

        await _workflow.Received(1).Show(schemas, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShowThrows_Propagates()
    {
        _workflow.Show(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns<DatabaseSchema>(_ => throw new InvalidOperationException("boom"));

        var act = () => BuildSut().Execute(new ShowArguments());

        await Should.ThrowAsync<InvalidOperationException>(act);
    }
}
