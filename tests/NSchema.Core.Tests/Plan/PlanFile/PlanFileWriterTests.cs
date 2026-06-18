using System.Text;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Columns;
using NSchema.Plan.Model.Enums;
using NSchema.Plan.Model.Routines;
using NSchema.Plan.Model.Schemas;
using NSchema.Plan.Model.Sequence;
using NSchema.Plan.Model.Tables;
using NSchema.Plan.PlanFile;
using NSchema.Schema.Model.Enums;
using NSchema.Schema.Model.Routines;
using NSchema.Schema.Model.Scripts;
using NSchema.Schema.Model.Sequences;
using NSchema.Sql.Model;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Plan.PlanFile;

public sealed class PlanFileWriterTests
{
    private static readonly PlanFileWriter _sut = new();

    private static string Json(PlanFileEnvelope envelope) => Encoding.UTF8.GetString(_sut.Serialize(envelope).Span);

    private static PlanFileEnvelope SampleEnvelope()
    {
        // A plan mixing several action types (including one carrying a full Table definition) so the round-trip
        // exercises the polymorphic MigrationAction hierarchy and the nested schema model together.
        var table = TestData.RichSchema().Schemas[0].Tables[0];

        var plan = new MigrationPlan(
            [
                new CreateSchema("app"),
                new CreateEnum("app", new EnumType("status", ["pending", "shipped"])),
                new RenameEnum("app", "importance", "priority"),
                new AddEnumValue("app", "priority", "medium", After: "low"),
                new SetEnumComment("app", "priority", null, "ranking"),
                new CreateSequence("app", new Sequence("order_id", new SequenceOptions(StartWith: 100, IncrementBy: 5, Cycle: true))),
                new RenameSequence("app", "bill_id", "invoice_id"),
                new AlterSequence("app", "order_id", new SequenceOptions(StartWith: 100), new SequenceOptions(StartWith: 1000)),
                new SetSequenceComment("app", "order_id", null, "order numbers"),
                new CreateRoutine("app", new Routine("add_tax", RoutineKind.Function, "amount numeric", "RETURNS numeric AS $$ SELECT amount; $$")),
                new RecreateRoutine("app", new Routine("score", RoutineKind.Function, "a int, b int", "RETURNS int AS $$ SELECT a + b; $$", Comment: "scoring")),
                new RenameRoutine("app", "old_fn", "new_fn", RoutineKind.Function),
                new SetRoutineComment("app", "add_tax", null, "adds tax", RoutineKind.Function),
                new CreateRoutine("app", new Routine("archive", RoutineKind.Procedure, "before date", "LANGUAGE sql AS $$ DELETE; $$")),
                new RecreateRoutine("app", new Routine("cleanup", RoutineKind.Procedure, "", "LANGUAGE sql AS $$ TRUNCATE; $$")),
                new RenameRoutine("app", "old_proc", "new_proc", RoutineKind.Procedure),
                new SetRoutineComment("app", "archive", null, "archival", RoutineKind.Procedure),
                new CreateTable("app", table),
                new AddColumn("app", "users", table.Columns[1]),
                new DropTable("app", "legacy"),
                new DropEnum("app", "stale_enum"),
                new DropSequence("app", "stale_seq"),
                new DropRoutine("app", "stale_fn", RoutineKind.Function),
                new DropRoutine("app", "stale_proc", RoutineKind.Procedure),
            ],
            [new Script("seed", "INSERT INTO app.config VALUES (1)", ScriptType.PreDeployment)],
            [new Script("reindex", "REINDEX TABLE app.users", ScriptType.PostDeployment) { RunOutsideTransaction = true }]);

        var sql = new SqlPlan([
            new SqlStatement("CREATE SCHEMA app"),
            new SqlStatement("CREATE INDEX CONCURRENTLY ...", RunOutsideTransaction: true),
        ]);

        return new PlanFileEnvelope(plan, sql, TestData.DestructiveDiff, DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsTheWholeEnvelope()
    {
        var original = SampleEnvelope();

        var json = Json(original);
        var roundTripped = _sut.Deserialize(_sut.Serialize(original));

        // A read + write cycle reproduces the exact same document, including the polymorphic actions and the diff.
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
    public void Deserialize_RestoresConcreteActionTypesInOrder()
    {
        var roundTripped = _sut.Deserialize(_sut.Serialize(SampleEnvelope()));

        // The discriminator must reconstruct each concrete record, not the abstract base, and keep order.
        roundTripped.Plan.Actions.Select(a => a.GetType()).ShouldBe(
            [
                typeof(CreateSchema),
                typeof(CreateEnum), typeof(RenameEnum), typeof(AddEnumValue), typeof(SetEnumComment),
                typeof(CreateSequence), typeof(RenameSequence), typeof(AlterSequence), typeof(SetSequenceComment),
                typeof(CreateRoutine), typeof(RecreateRoutine), typeof(RenameRoutine), typeof(SetRoutineComment),
                typeof(CreateRoutine), typeof(RecreateRoutine), typeof(RenameRoutine), typeof(SetRoutineComment),
                typeof(CreateTable), typeof(AddColumn), typeof(DropTable),
                typeof(DropEnum), typeof(DropSequence), typeof(DropRoutine), typeof(DropRoutine),
            ]);
    }

    [Fact]
    public void Deserialize_RestoresSqlAndScriptDetail()
    {
        var roundTripped = _sut.Deserialize(_sut.Serialize(SampleEnvelope()));

        roundTripped.Sql.Statements[1].RunOutsideTransaction.ShouldBeTrue();
        roundTripped.Plan.PostDeploymentScripts[0].RunOutsideTransaction.ShouldBeTrue();
        roundTripped.Plan.PreDeploymentScripts[0].Type.ShouldBe(ScriptType.PreDeployment);
    }

    [Fact]
    public async Task Write_ThenRead_RoundTripsThroughAFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nschema-plan-{Guid.NewGuid():N}.json");
        try
        {
            await _sut.Write(path, SampleEnvelope(), TestContext.Current.CancellationToken);
            var roundTripped = await _sut.Read(path, TestContext.Current.CancellationToken);

            Json(roundTripped).ShouldBe(Json(SampleEnvelope()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Deserialize_Garbage_ThrowsPlanFileDeserializationException()
    {
        var garbage = Encoding.UTF8.GetBytes("not json at all");

        Should.Throw<PlanFileDeserializationException>(() => _sut.Deserialize(garbage));
    }
}
