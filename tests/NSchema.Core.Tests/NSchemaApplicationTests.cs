using Microsoft.Extensions.DependencyInjection;
using NSchema.Operations.Apply;
using NSchema.Operations.Destroy;
using NSchema.Operations.Plan;
using NSchema.Operations.Refresh;

namespace NSchema.Tests;

public sealed class NSchemaApplicationTests
{
    private readonly IPlanOperation _planOp = Substitute.For<IPlanOperation>();
    private readonly IApplyOperation _applyOp = Substitute.For<IApplyOperation>();
    private readonly IRefreshOperation _refreshOp = Substitute.For<IRefreshOperation>();
    private readonly IDestroyOperation _destroyOp = Substitute.For<IDestroyOperation>();

    private NSchemaApplication BuildApp(Action<NSchemaApplicationBuilder>? configure = null)
    {
        var builder = NSchemaApplication.CreateBuilder();
        // Register substitutes before Build() so TryAddSingleton in ApplyServices doesn't override them.
        builder.Services.AddSingleton(_planOp);
        builder.Services.AddSingleton(_applyOp);
        builder.Services.AddSingleton(_refreshOp);
        builder.Services.AddSingleton(_destroyOp);
        configure?.Invoke(builder);
        return builder.Build();
    }

    [Fact]
    public async Task Plan_RunsPlanOperation()
    {
        using var app = BuildApp();

        await app.Plan(new PlanArguments(), TestContext.Current.CancellationToken);

        await _planOp.Received(1).Execute(Arg.Any<PlanArguments>(), Arg.Any<CancellationToken>());
        await _applyOp.DidNotReceive().Execute(Arg.Any<ApplyArguments>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_RunsApplyOperation()
    {
        using var app = BuildApp();

        await app.Apply(new ApplyArguments(), TestContext.Current.CancellationToken);

        await _applyOp.Received(1).Execute(Arg.Any<ApplyArguments>(), Arg.Any<CancellationToken>());
        await _planOp.DidNotReceive().Execute(Arg.Any<PlanArguments>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_RunsRefreshOperation()
    {
        using var app = BuildApp();

        await app.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken);

        await _refreshOp.Received(1).Execute(Arg.Any<RefreshArguments>(), Arg.Any<CancellationToken>());
        await _applyOp.DidNotReceive().Execute(Arg.Any<ApplyArguments>(), Arg.Any<CancellationToken>());
        await _planOp.DidNotReceive().Execute(Arg.Any<PlanArguments>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Destroy_RunsDestroyOperation()
    {
        using var app = BuildApp();

        await app.Destroy(new DestroyArguments(), TestContext.Current.CancellationToken);

        await _destroyOp.Received(1).Execute(Arg.Any<DestroyArguments>(), Arg.Any<CancellationToken>());
        await _applyOp.DidNotReceive().Execute(Arg.Any<ApplyArguments>(), Arg.Any<CancellationToken>());
        await _planOp.DidNotReceive().Execute(Arg.Any<PlanArguments>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Plan_ForwardsProvidedArguments()
    {
        using var app = BuildApp();
        var arguments = new PlanArguments();

        await app.Plan(arguments, TestContext.Current.CancellationToken);

        await _planOp.Received(1).Execute(arguments, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SecondRun_Throws()
    {
        using var app = BuildApp();
        await app.Plan(new PlanArguments(), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(() => app.Apply(new ApplyArguments()));
    }
}
