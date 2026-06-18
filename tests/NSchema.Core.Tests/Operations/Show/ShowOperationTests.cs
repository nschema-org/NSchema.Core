using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Operations.Show;
using NSchema.Plan.Model;
using NSchema.Plan.PlanFile;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Sql.Model;

namespace NSchema.Tests.Operations.Show;

public sealed class ShowOperationTests
{
    private readonly ICurrentSchemaProvider _currentProvider = Substitute.For<ICurrentSchemaProvider>();
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();

    private readonly MigrationPlan _plan = new([new CreateSchema("app")], [], []);
    private readonly DatabaseDiff _diff = new([]);
    private readonly SqlPlan _sqlPlan = new([new SqlStatement("CREATE SCHEMA app")]);

    private ShowOperation BuildSut() => new(_currentProvider, _reporter, new PlanFileWriter());

    private string WritePlanFile()
    {
        var path = Path.GetTempFileName();
        var envelope = new PlanFileEnvelope(_plan, _sqlPlan, _diff, DateTimeOffset.UnixEpoch);
        File.WriteAllBytes(path, new PlanFileWriter().Serialize(envelope).ToArray());
        return path;
    }

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

    [Fact]
    public async Task Execute_WithPlanFile_ReportsDiffPlanAndSql()
    {
        var path = WritePlanFile();
        try
        {
            await BuildSut().Execute(new ShowArguments { PlanFile = path }, TestContext.Current.CancellationToken);

            _reporter.Received(1).ReportDiff(Arg.Any<DatabaseDiff>());
            _reporter.Received(1).ReportPlan(Arg.Any<MigrationPlan>());
            _reporter.Received(1).ReportSqlPlan(Arg.Any<SqlPlan>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Execute_WithPlanFile_DoesNotReadStateOrReportSchema()
    {
        var path = WritePlanFile();
        try
        {
            await BuildSut().Execute(new ShowArguments { PlanFile = path }, TestContext.Current.CancellationToken);

            await _currentProvider.DidNotReceive()
                .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
            _reporter.DidNotReceive().ReportSchema(Arg.Any<DatabaseSchema>());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
