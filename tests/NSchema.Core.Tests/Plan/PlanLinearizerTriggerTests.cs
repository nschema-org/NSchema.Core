using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Diff.Domain.Models.Triggers;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Models;
using NSchema.Plan.Domain.Models.Tables;
using NSchema.Plan.Domain.Models.Triggers;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Triggers;

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
        var trigger = new Trigger("audit", TriggerTiming.After, TriggerEvent.Insert, "app.log");
        var table = new TableDiff("app", "users", ChangeKind.Add,
            Triggers: [new TriggerDiff(ChangeKind.Add, "audit", trigger)],
            Definition: new Table("users", Columns: [new Column("id", SqlType.Int)]));

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
        var trigger = new Trigger("audit", TriggerTiming.After, TriggerEvent.Insert, "app.log", Comment: "note");
        var table = new TableDiff("app", "users", ChangeKind.Add,
            Triggers:
            [
                new TriggerDiff(ChangeKind.Add, "audit", trigger),
                new TriggerDiff(ChangeKind.Modify, "audit", null, new ValueChange<string>(null, "note")),
            ],
            Definition: new Table("users", Columns: [new Column("id", SqlType.Int)]));

        var actions = _linearizer.Linearize(new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add, Tables: [table])]));

        actions.OfType<CreateTrigger>().ShouldHaveSingleItem().Trigger.Name.ShouldBe("audit");
        actions.OfType<SetTriggerComment>().ShouldHaveSingleItem().NewComment.ShouldBe("note");
    }
}
