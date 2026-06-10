using NSchema.Operations;
using NSchema.Operations.Show;
using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.Tests.Operations.Show;

public sealed class ShowOperationTests
{
    private readonly ICurrentSchemaProvider _currentProvider = Substitute.For<ICurrentSchemaProvider>();
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();

    private ShowOperation BuildSut() => new(_currentProvider, Helpers.TestReporters.ResolverFor(_reporter));

    public ShowOperationTests()
    {
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new DatabaseSchema([]));
    }

    [Fact]
    public async Task Execute_ReadsOfflineStateScopedToArguments()
    {
        var schemas = new[] { "app" };

        await BuildSut().Execute(new ShowArguments { Schemas = schemas }, TestContext.Current.CancellationToken);

        await _currentProvider.Received(1)
            .GetSchema(SchemaSourceMode.Offline, schemas, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ReportsTheRecordedSchema()
    {
        var recorded = new DatabaseSchema([new SchemaDefinition("app")]);
        _currentProvider
            .GetSchema(SchemaSourceMode.Offline, Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(recorded);

        await BuildSut().Execute(new ShowArguments(), TestContext.Current.CancellationToken);

        _reporter.Received(1).ReportSchema(recorded);
    }

    [Fact]
    public async Task Execute_WhenNoStateStore_Propagates()
    {
        _currentProvider
            .GetSchema(SchemaSourceMode.Offline, Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<DatabaseSchema>(_ => throw new InvalidOperationException("no store"));

        var act = () => BuildSut().Execute(new ShowArguments());

        await Should.ThrowAsync<InvalidOperationException>(act);
    }
}
