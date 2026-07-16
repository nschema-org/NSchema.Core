using System.Text;
using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Columns;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Scripts;
using NSchema.Plan.Domain.Models;
using NSchema.Plan.PlanFile;

namespace NSchema.Tests.Plan.PlanFile;

public sealed class PlanFileManagerTests
{
    private static readonly PlanFileManager _sut = new();

    private static string Json(PlanFileEnvelope envelope) => Encoding.UTF8.GetString(_sut.Serialize(envelope).Span);

    private static PlanFileEnvelope SampleEnvelope()
    {
        // A plan carrying a rich diff (including a full Table definition), both script event kinds, and
        // statements with execution metadata, so the round-trip exercises the whole artifact.
        var backfill = new ChangeScript(new SqlIdentifier("backfill"), new SqlText("UPDATE app.users SET email = ''"),
            new SqlIdentifier("app"), ChangeTrigger.AddColumn, new SqlIdentifier("users"), new SqlIdentifier("email"));
        var email = new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text)) { MigrationScript = backfill };
        var users = new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify, Columns: [email]);
        var plan = new MigrationPlan(
            new DatabaseDiff([new SchemaDiff(new SqlIdentifier("app"), Tables: [users])])
            {
                // Both deployment bookends at the root; the change script rides its column above.
                DeploymentScripts =
                [
                    new DeploymentScript(new SqlIdentifier("seed"), new SqlText("INSERT INTO app.config VALUES (1)"), null, DeploymentPhase.Pre) { RunCondition = RunCondition.Once },
                    new DeploymentScript(new SqlIdentifier("reindex"), new SqlText("REINDEX TABLE app.users"), null, DeploymentPhase.Post) { RunOutsideTransaction = true },
                ],
            },
            [
                new SqlStatement(new SqlText("INSERT INTO app.config VALUES (1)")),
                new SqlStatement(new SqlText("CREATE INDEX CONCURRENTLY ..."), RunOutsideTransaction: true),
                new SqlStatement(new SqlText("REINDEX TABLE app.users"), RunOutsideTransaction: true),
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

        // The discriminator must reconstruct each concrete script record, not the abstract base, and keep order.
        roundTripped.Plan.Diff.AllScripts().Select(s => s.GetType()).ShouldBe(
            [typeof(ChangeScript), typeof(DeploymentScript), typeof(DeploymentScript)]);
        roundTripped.Plan.Diff.AllScripts().ShouldBe(SampleEnvelope().Plan.Diff.AllScripts());
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
        var migration = new ChangeScript(new SqlIdentifier("backfill_emails"), new SqlText("UPDATE app.users SET email = ''"),
            new SqlIdentifier("app"), ChangeTrigger.AddColumn, new SqlIdentifier("users"), new SqlIdentifier("email"))
        {
            RunOutsideTransaction = true,
        };
        var diff = new DatabaseDiff(
        [
            new SchemaDiff(new SqlIdentifier("app"), Tables:
            [
                new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify, Columns:
                [
                    new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text)) { MigrationScript = migration },
                ]),
            ]),
        ]);
        var envelope = new PlanFileEnvelope(
            new MigrationPlan(diff, [new SqlStatement(new SqlText("UPDATE app.users SET email = ''"), RunOutsideTransaction: true)]),
            DateTimeOffset.UnixEpoch);

        // Act
        var roundTripped = _sut.Deserialize(_sut.Serialize(envelope));

        // Assert — record equality covers every field, including the init-only RunOutsideTransaction.
        roundTripped.Plan.Diff.Schemas[0].Tables[0].Columns[0].MigrationScript.ShouldBe(migration);
        roundTripped.Plan.Diff.AllScripts().ShouldHaveSingleItem().ShouldBe(migration);
    }

    [Fact]
    public void Deserialize_Garbage_ThrowsPlanFileDeserializationException()
    {
        var garbage = Encoding.UTF8.GetBytes("not json at all");

        Should.Throw<PlanFileDeserializationException>(() => _sut.Deserialize(garbage));
    }
}
