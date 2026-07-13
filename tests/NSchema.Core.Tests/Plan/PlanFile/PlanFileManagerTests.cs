using NSchema.Project.Domain.Models;
using System.Text;
using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Columns;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Plan.Domain.Models;
using NSchema.Plan.PlanFile;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Tests.Plan.PlanFile;

public sealed class PlanFileManagerTests
{
    private static readonly PlanFileManager _sut = new();

    private static string Json(PlanFileEnvelope envelope) => Encoding.UTF8.GetString(_sut.Serialize(envelope).Span);

    private static PlanFileEnvelope SampleEnvelope()
    {
        // A plan carrying a rich diff (including a full Table definition), both script event kinds, and
        // statements with execution metadata, so the round-trip exercises the whole artifact.
        var plan = new MigrationPlan(
            TestData.DestructiveDiff with
            {
                Scripts =
                [
                    new Script(new SqlIdentifier("seed"), "INSERT INTO app.config VALUES (1)", new DeploymentEvent(DeploymentPhase.Pre)),
                    new Script(new SqlIdentifier("backfill"), "UPDATE app.users SET email = ''", new ChangeEvent(ChangeTrigger.AddColumn, new SqlIdentifier("users"), new SqlIdentifier("email")) { ScopeSchema = new SqlIdentifier("app") }) { RunCondition = RunCondition.Once },
                    new Script(new SqlIdentifier("reindex"), "REINDEX TABLE app.users", new DeploymentEvent(DeploymentPhase.Post)) { RunOutsideTransaction = true },
                ],
            },
            [
                new SqlStatement("INSERT INTO app.config VALUES (1)"),
                new SqlStatement("CREATE INDEX CONCURRENTLY ...", RunOutsideTransaction: true),
                new SqlStatement("REINDEX TABLE app.users", RunOutsideTransaction: true),
            ]);

        return new PlanFileEnvelope(plan, DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsTheWholeEnvelope()
    {
        var original = SampleEnvelope();

        var json = Json(original);
        var roundTripped = _sut.Deserialize(_sut.Serialize(original));

        // A read + write cycle reproduces the exact same document, including the polymorphic script events and the diff.
        Json(roundTripped).ShouldBe(json);
    }

    [Fact]
    public void Serialize_StampsTheCurrentVersion_WithoutTheCallerSupplyingIt()
    {
        // The caller never passes a version; the format owns it.
        var roundTripped = _sut.Deserialize(_sut.Serialize(SampleEnvelope()));

        roundTripped.Version.ShouldBe(PlanFileEnvelope.CurrentVersion);
        roundTripped.CreatedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void Deserialize_RestoresConcreteScriptEventsInOrder()
    {
        var roundTripped = _sut.Deserialize(_sut.Serialize(SampleEnvelope()));

        // The discriminator must reconstruct each concrete event record, not the abstract base, and keep order.
        roundTripped.Plan.Diff.Scripts.Select(s => s.Event.GetType()).ShouldBe(
            [typeof(DeploymentEvent), typeof(ChangeEvent), typeof(DeploymentEvent)]);
        roundTripped.Plan.Diff.Scripts.ShouldBe(SampleEnvelope().Plan.Diff.Scripts);
    }

    [Fact]
    public void Deserialize_RestoresStatementDetail()
    {
        var roundTripped = _sut.Deserialize(_sut.Serialize(SampleEnvelope()));

        roundTripped.Plan.Statements.ShouldBe(SampleEnvelope().Plan.Statements);
        roundTripped.Plan.Statements[1].RunOutsideTransaction.ShouldBeTrue();
    }

    [Fact]
    public async Task Write_ThenRead_RoundTripsThroughAFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nschema-plan-{Guid.NewGuid():N}.json");
        try
        {
            await _sut.Write(path, SampleEnvelope(), TestContext.Current.CancellationToken);
            var roundTripped = await _sut.Read(path, TestContext.Current.CancellationToken);

            Json(roundTripped.Require()).ShouldBe(Json(SampleEnvelope()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsDiffAnnotations()
    {
        // Arrange — a diff whose column add is annotated with its matched script, so diff-node persistence and
        // the script-event discriminator inside the diff are both exercised.
        var migration = new Script(new SqlIdentifier("backfill emails"), "UPDATE app.users SET email = ''",
            new ChangeEvent(ChangeTrigger.AddColumn, new SqlIdentifier("users"), new SqlIdentifier("email")) { ScopeSchema = new SqlIdentifier("app") })
        {
            RunOutsideTransaction = true,
        };
        var diff = new DatabaseDiff(
        [
            new SchemaDiff(new SqlIdentifier("app"), Tables:
            [
                new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify, Columns:
                [
                    new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text)) { MigrationScript = migration.Name },
                ]),
            ]),
        ]);
        var envelope = new PlanFileEnvelope(
            new MigrationPlan(diff with { Scripts = [migration] }, [new SqlStatement("UPDATE app.users SET email = ''", RunOutsideTransaction: true)]),
            DateTimeOffset.UnixEpoch);

        // Act
        var roundTripped = _sut.Deserialize(_sut.Serialize(envelope));

        // Assert — record equality covers every field, including the init-only RunOutsideTransaction.
        roundTripped.Plan.Diff.Schemas[0].Tables[0].Columns[0].MigrationScript.ShouldBe(migration.Name);
        roundTripped.Plan.Diff.Scripts.ShouldHaveSingleItem().ShouldBe(migration);
    }

    [Fact]
    public void Deserialize_Garbage_ThrowsPlanFileDeserializationException()
    {
        var garbage = Encoding.UTF8.GetBytes("not json at all");

        Should.Throw<PlanFileDeserializationException>(() => _sut.Deserialize(garbage));
    }
}
