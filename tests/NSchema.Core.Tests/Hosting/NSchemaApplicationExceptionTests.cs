using Microsoft.Extensions.DependencyInjection;
using NSchema.Operations;
using NSchema.Operations.Apply;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Hosting;

public sealed class NSchemaApplicationExceptionTests
{
    private readonly IApplyOperation _applyOp = Substitute.For<IApplyOperation>();
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();

    private NSchemaApplication BuildApp(ExceptionBehavior behavior = ExceptionBehavior.ReportAndThrow)
    {
        var builder = NSchemaApplication.CreateBuilder(new NSchemaApplicationOptions { ExceptionBehavior = behavior });
        builder.Services.AddSingleton(_applyOp);
        builder.UseReporter(_reporter);
        return builder.Build();
    }

    [Fact]
    public async Task Operation_ThrowsToCaller_AndReports_OnReportAndThrow()
    {
        var boom = new InvalidOperationException("boom");
        _applyOp.Execute(Arg.Any<ApplyArguments>(), Arg.Any<CancellationToken>()).ThrowsAsync(boom);
        using var app = BuildApp();

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => app.Apply(new ApplyArguments(), TestContext.Current.CancellationToken));

        thrown.ShouldBe(boom);
        _reporter.Received(1).ReportException(boom);
    }

    [Fact]
    public async Task Operation_ThrowsToCaller_WithoutReporting_OnThrowBehavior()
    {
        var boom = new InvalidOperationException("boom");
        _applyOp.Execute(Arg.Any<ApplyArguments>(), Arg.Any<CancellationToken>()).ThrowsAsync(boom);
        using var app = BuildApp(ExceptionBehavior.Throw);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => app.Apply(new ApplyArguments(), TestContext.Current.CancellationToken));

        thrown.ShouldBe(boom);
        _reporter.DidNotReceive().ReportException(Arg.Any<Exception>());
    }
}
