using NSchema.Diff.Model;
using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Tables;
using NSchema.Diff.Model.Triggers;
using NSchema.Model.Columns;
using NSchema.Model.Routines;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Services;
using NSchema.Plan.Model.Tables;
using NSchema.Plan.Model.Triggers;

namespace NSchema.Tests.Plan;

/// <summary>
/// Pins trigger ordering: a trigger is created after its table (and after the function it calls, which already
/// precedes tables) and dropped before its table.
/// </summary>
public sealed class PlanLinearizerTriggerTests
{
    private readonly PlanLinearizer _linearizer = new();

    private static int IndexOf<T>(IReadOnlyList<MigrationAction> actions) =>
        actions.Select((a, i) => (a, i)).First(x => x.a is T).i;

    [Fact]
    public void CreateTrigger_IsEmittedAfterItsTableIsCreated()
    {
        var trigger = new Trigger { Name = "audit", Timing = TriggerTiming.After, Events = TriggerEvent.Insert, Function = new RoutineReference("app", "log") };
        var table = new TableDiff("app", "users", ChangeKind.Add,
            Triggers: [new TriggerDiff(ChangeKind.Add, "audit", trigger)],
            Definition: new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] });

        var actions = _linearizer.Linearize(new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add, Tables: [table])]));

        IndexOf<CreateTrigger>(actions).ShouldBeGreaterThan(IndexOf<CreateTable>(actions));
    }

    [Fact]
    public void DropTrigger_IsEmittedBeforeTablesAreDropped()
    {
        var modified = new TableDiff("app", "users", ChangeKind.Modify,
            Triggers: [new TriggerDiff(ChangeKind.Remove, "audit")]);
        var dropped = new TableDiff("app", "legacy", ChangeKind.Remove);

        var actions = _linearizer.Linearize(new DatabaseDiff([new SchemaDiff("app", Tables: [modified, dropped])]));

        IndexOf<DropTrigger>(actions).ShouldBeLessThan(IndexOf<DropTable>(actions));
    }

    [Fact]
    public void AddedTrigger_WithComment_EmitsCreateThenSetComment()
    {
        var trigger = new Trigger { Name = "audit", Timing = TriggerTiming.After, Events = TriggerEvent.Insert, Function = new RoutineReference("app", "log"), Comment = "note" };
        var table = new TableDiff("app", "users", ChangeKind.Add,
            Triggers:
            [
                new TriggerDiff(ChangeKind.Add, "audit", trigger),
                new TriggerDiff(ChangeKind.Modify, "audit", null, new ValueChange<string>(null, "note")),
            ],
            Definition: new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] });

        var actions = _linearizer.Linearize(new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add, Tables: [table])]));

        actions.OfType<CreateTrigger>().ShouldHaveSingleItem().Trigger.Name.ShouldBe("audit");
        actions.OfType<SetTriggerComment>().ShouldHaveSingleItem().NewComment.ShouldBe("note");
    }
}
