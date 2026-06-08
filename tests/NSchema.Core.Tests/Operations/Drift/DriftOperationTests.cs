using NSchema.Operations;
using NSchema.Operations.Drift;
using NSchema.Operations.Services;

namespace NSchema.Tests.Operations.Drift;

public sealed class DriftOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();

    private DriftOperation BuildSut() => new(_workflow, Helpers.TestReporters.ResolverFor(_reporter));

    [Fact]
    public async Task Execute_DelegatesToWorkflowDriftWithScope()
    {
        var schemas = new[] { "app" };

        await BuildSut().Execute(new DriftArguments { Schemas = schemas }, TestContext.Current.CancellationToken);

        await _workflow.Received(1).Drift(schemas, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenDrifted_ReportsDriftDetected()
    {
        _workflow.Drift(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(true);

        await BuildSut().Execute(new DriftArguments(), TestContext.Current.CancellationToken);

        _reporter.Received().Info("Drift detected.");
    }

    [Fact]
    public async Task Execute_WhenNotDrifted_ReportsNoDrift()
    {
        _workflow.Drift(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(false);

        await BuildSut().Execute(new DriftArguments(), TestContext.Current.CancellationToken);

        _reporter.Received().Info("No drift detected.");
    }
}
