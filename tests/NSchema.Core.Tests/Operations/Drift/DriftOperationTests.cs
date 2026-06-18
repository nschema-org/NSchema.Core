using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Operations.Drift;
using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.Tests.Operations.Drift;

public sealed class DriftOperationTests
{
    private readonly ICurrentSchemaProvider _currentProvider = Substitute.For<ICurrentSchemaProvider>();
    private readonly ISchemaComparer _comparer = Substitute.For<ISchemaComparer>();
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();

    private DriftOperation BuildSut() => new(_currentProvider, _reporter, _comparer);

    public DriftOperationTests()
    {
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new DatabaseSchema([]));
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(new DatabaseDiff([]));
    }

    [Fact]
    public async Task Execute_ComparesRecordedOfflineStateAgainstLiveOnlineSchema()
    {
        var recorded = new DatabaseSchema([new SchemaDefinition("recorded")]);
        var live = new DatabaseSchema([new SchemaDefinition("live")]);
        var schemas = new[] { "app" };
        _currentProvider.GetSchema(SchemaSourceMode.Offline, schemas, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(recorded);
        _currentProvider.GetSchema(SchemaSourceMode.Online, schemas, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(live);

        await BuildSut().Execute(new DriftArguments { Schemas = schemas }, TestContext.Current.CancellationToken);

        // Diff direction is recorded -> live, so drift reads as how the live database differs from what we recorded.
        _comparer.Received(1).Compare(recorded, live);
    }

    [Fact]
    public async Task Execute_WhenDiffHasChanges_ReportsDriftAndTheDiff()
    {
        var diff = new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add, null, null, [], [])]);
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(diff);

        await BuildSut().Execute(new DriftArguments(), TestContext.Current.CancellationToken);

        _reporter.Received(1).ReportDiff(diff);
        _reporter.Received().Report(MessageKind.Warning, "Drift detected.");
    }

    [Fact]
    public async Task Execute_WhenDiffIsEmpty_ReportsNoDrift()
    {
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(new DatabaseDiff([]));

        await BuildSut().Execute(new DriftArguments(), TestContext.Current.CancellationToken);

        _reporter.Received().Report(MessageKind.Success, "No drift detected.");
    }
}
