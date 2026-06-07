using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Operations;

namespace NSchema.Tests;

public sealed class NSchemaApplicationTests
{
    private readonly INSchemaOperation _planOp = Substitute.For<INSchemaOperation>();
    private readonly INSchemaOperation _applyOp = Substitute.For<INSchemaOperation>();
    private readonly INSchemaOperation _refreshOp = Substitute.For<INSchemaOperation>();
    private readonly INSchemaOperation _destroyOp = Substitute.For<INSchemaOperation>();

    private NSchemaApplication BuildApp(Action<NSchemaApplicationBuilder>? configure = null)
    {
        var builder = NSchemaApplication.CreateBuilder();
        // Register substitutes before Build() so TryAddKeyedSingleton in ApplyServices doesn't override them.
        builder.Services.AddKeyedSingleton<INSchemaOperation>(MigrationOperation.Plan, (_, _) => _planOp);
        builder.Services.AddKeyedSingleton<INSchemaOperation>(MigrationOperation.Apply, (_, _) => _applyOp);
        builder.Services.AddKeyedSingleton<INSchemaOperation>(MigrationOperation.Refresh, (_, _) => _refreshOp);
        builder.Services.AddKeyedSingleton<INSchemaOperation>(MigrationOperation.Destroy, (_, _) => _destroyOp);
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
    public async Task ExplicitOperation_OverridesConfiguredOperation()
    {
        // Configured to Apply, but Plan() is invoked explicitly.
        using var app = BuildApp(b => b.RunOperation(MigrationOperation.Apply));

        await app.Plan(TestContext.Current.CancellationToken);

        await _planOp.Received(1).Execute(Arg.Any<CancellationToken>());
        await _applyOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_UsesConfiguredOperation_WhenNoOverride()
    {
        using var app = BuildApp(b => b.RunOperation(MigrationOperation.Plan));

        await app.RunAsync(TestContext.Current.CancellationToken);

        await _planOp.Received(1).Execute(Arg.Any<CancellationToken>());
        await _applyOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SecondRun_Throws()
    {
        using var app = BuildApp();
        await app.Plan(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(() => app.Apply());
    }
}
