using Microsoft.Extensions.DependencyInjection;
using NSchema.Operations;

namespace NSchema.Tests;

public sealed class NSchemaApplicationTests
{
    private readonly IOperation _planOp = Substitute.For<IOperation>();
    private readonly IOperation _applyOp = Substitute.For<IOperation>();
    private readonly IOperation _refreshOp = Substitute.For<IOperation>();
    private readonly IOperation _destroyOp = Substitute.For<IOperation>();

    private NSchemaApplication BuildApp(Action<NSchemaApplicationBuilder>? configure = null)
    {
        var builder = NSchemaApplication.CreateBuilder();
        // Register substitutes before Build() so TryAddKeyedSingleton in ApplyServices doesn't override them.
        builder.Services.AddKeyedSingleton<IOperation>(OperationKind.Plan, (_, _) => _planOp);
        builder.Services.AddKeyedSingleton<IOperation>(OperationKind.Apply, (_, _) => _applyOp);
        builder.Services.AddKeyedSingleton<IOperation>(OperationKind.Refresh, (_, _) => _refreshOp);
        builder.Services.AddKeyedSingleton<IOperation>(OperationKind.Destroy, (_, _) => _destroyOp);
        configure?.Invoke(builder);
        return builder.Build();
    }

    [Fact]
    public async Task Plan_RunsPlanOperation()
    {
        using var app = BuildApp();

        await app.Plan(TestContext.Current.CancellationToken);

        await _planOp.Received(1).Execute(Arg.Any<CancellationToken>());
        await _applyOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_RunsApplyOperation()
    {
        using var app = BuildApp();

        await app.Apply(TestContext.Current.CancellationToken);

        await _applyOp.Received(1).Execute(Arg.Any<CancellationToken>());
        await _planOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_RunsRefreshOperation()
    {
        using var app = BuildApp();

        await app.Refresh(TestContext.Current.CancellationToken);

        await _refreshOp.Received(1).Execute(Arg.Any<CancellationToken>());
        await _applyOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
        await _planOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Destroy_RunsDestroyOperation()
    {
        using var app = BuildApp();

        await app.Destroy(TestContext.Current.CancellationToken);

        await _destroyOp.Received(1).Execute(Arg.Any<CancellationToken>());
        await _applyOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
        await _planOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SecondRun_Throws()
    {
        using var app = BuildApp();
        await app.Plan(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(() => app.Apply());
    }
}
