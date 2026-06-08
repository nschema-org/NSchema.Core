using Microsoft.Extensions.DependencyInjection;
using NSchema.Operations;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Hosting;

public sealed class NSchemaApplicationExceptionTests
{
    private readonly IOperation _applyOp = Substitute.For<IOperation>();
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();

    private NSchemaApplication BuildApp(Action<NSchemaApplicationBuilder>? configure = null)
    {
        var builder = NSchemaApplication.CreateBuilder();
        builder.Services.AddKeyedSingleton<IOperation>(OperationKind.Apply, (_, _) => _applyOp);
        builder.AddReporter(DefaultOperationReporter.ReporterName, _reporter);
        configure?.Invoke(builder);
        return builder.Build();
    }

    [Fact]
    public async Task Operation_ThrowsToCaller_AndReports_OnReportAndThrow()
    {
        var boom = new InvalidOperationException("boom");
        _applyOp.Execute(Arg.Any<CancellationToken>()).ThrowsAsync(boom);
        using var app = BuildApp();

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => app.Apply(TestContext.Current.CancellationToken));

        thrown.ShouldBe(boom);
        _reporter.Received(1).ReportException(boom);
    }
}
