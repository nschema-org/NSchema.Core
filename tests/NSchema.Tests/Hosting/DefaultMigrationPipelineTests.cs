using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;

namespace NSchema.Tests.Hosting;

public sealed class DefaultMigrationPipelineTests
{
    private static MigrationPlan EmptyPlan() => new([], DatabaseSchema.Create([]));

    private static IMigrationPlanner PlannerReturning(MigrationPlan plan)
    {
        var p = Substitute.For<IMigrationPlanner>();
        p.Plan(Arg.Any<CancellationToken>()).Returns(Task.FromResult(plan));
        return p;
    }

    private static DefaultMigrationPipeline Build(
        IMigrationPlanner planner,
        IMigrationExecutor executor,
        IMigrationReporter? reporter = null,
        MigrationOptions? options = null
    ) => new(
        Options.Create(options ?? new MigrationOptions()),
        reporter ?? Substitute.For<IMigrationReporter>(),
        Substitute.For<IMigrationPlanRenderer>(),
        planner,
        executor);

    [Fact]
    public async Task Run_DryRun_InvokesExecutorWithDryRunFlag()
    {
        var executor = Substitute.For<IMigrationExecutor>();
        var sut = Build(PlannerReturning(EmptyPlan()), executor, options: new MigrationOptions { DryRun = true });

        await sut.Run();

        await executor.Received(1).Apply(Arg.Any<MigrationPlan>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_NotDryRun_PassesPlanToExecutor()
    {
        var plan = new MigrationPlan([new CreateSchema("app")], DatabaseSchema.Create([]));
        var executor = Substitute.For<IMigrationExecutor>();
        var sut = Build(PlannerReturning(plan), executor);

        await sut.Run();

        await executor.Received(1).Apply(plan, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_NotDryRun_StillCallsExecutor_WhenPlanIsEmpty()
    {
        var executor = Substitute.For<IMigrationExecutor>();
        var sut = Build(PlannerReturning(EmptyPlan()), executor);

        await sut.Run();

        await executor.Received(1).Apply(Arg.Any<MigrationPlan>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_PolicyViolation_ReportsErrorsAndRethrows()
    {
        var planner = Substitute.For<IMigrationPlanner>();
        var errors = new[] { new PolicyError("P1", "msg1"), new PolicyError("P2", "msg2") };
        planner.Plan(Arg.Any<CancellationToken>())
            .Returns<Task<MigrationPlan>>(_ => throw new PolicyViolationException(errors));
        var reporter = Substitute.For<IMigrationReporter>();
        var executor = Substitute.For<IMigrationExecutor>();
        var sut = Build(planner, executor, reporter);

        await Should.ThrowAsync<PolicyViolationException>(() => sut.Run());

        reporter.Received().Error(Arg.Is<string>(s => s.Contains("msg1")));
        reporter.Received().Error(Arg.Is<string>(s => s.Contains("msg2")));
        await executor.DidNotReceive().Apply(Arg.Any<MigrationPlan>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_ExecutorThrows_ReportsErrorAndRethrows()
    {
        var executor = Substitute.For<IMigrationExecutor>();
        executor.Apply(Arg.Any<MigrationPlan>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("boom"));
        var reporter = Substitute.For<IMigrationReporter>();
        var sut = Build(PlannerReturning(EmptyPlan()), executor, reporter);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Run());

        reporter.Received().Error(Arg.Is<string>(s => s.Contains("boom")));
    }
}
