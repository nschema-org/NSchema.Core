using NSchema.Diff.Model;
using NSchema.Diff.Model.Triggers;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Routines;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Triggers (table members)
    // -------------------------------------------------------------------------

    private IReadOnlyList<TriggerDiff> DiffTriggers(IReadOnlyList<Trigger> current, IReadOnlyList<Trigger> desired) =>
        DiffTable(
            new Table(new SqlIdentifier("t"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)], triggers: [.. current]),
            new Table(new SqlIdentifier("t"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)], triggers: [.. desired]))?.Triggers ?? [];

    private static Trigger AfterInsert(string name, string function = "log", string? comment = null) =>
        new(new SqlIdentifier(name), TriggerTiming.After, TriggerEvent.Insert,
            new RoutineReference(new SqlIdentifier("app"), new SqlIdentifier(function)), TriggerLevel.Row)
        { Comment = comment };

    [Fact]
    public void Compare_NewTrigger_IsAddCarryingDefinition()
    {
        var diff = DiffTriggers([], [AfterInsert("audit")]).ShouldHaveSingleItem();

        diff.Kind.ShouldBe(ChangeKind.Add);
        diff.Definition!.Function.ShouldBe("app.log");
        diff.Definition.Events.ShouldBe(TriggerEvent.Insert);
    }

    [Fact]
    public void Compare_RemovedTrigger_IsRemove()
    {
        var diff = DiffTriggers([AfterInsert("audit")], []).ShouldHaveSingleItem();

        diff.Kind.ShouldBe(ChangeKind.Remove);
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_UnchangedTrigger_ProducesNoDiff()
        => DiffTriggers([AfterInsert("audit")], [AfterInsert("audit")]).ShouldBeEmpty();

    [Fact]
    public void Compare_TriggerCommentOnlyChange_IsModifyInPlace()
    {
        // Equality excludes the comment, so a comment-only change is a single in-place modify, not a recreate.
        var diff = DiffTriggers([AfterInsert("audit", comment: "old")], [AfterInsert("audit", comment: "new")])
            .ShouldHaveSingleItem();

        diff.Kind.ShouldBe(ChangeKind.Modify);
        diff.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_TriggerStructuralChange_IsRemoveThenAdd()
    {
        // Triggers don't rename or alter; a timing change is a drop + recreate, like an index.
        var current = new Trigger(new SqlIdentifier("audit"), TriggerTiming.Before, TriggerEvent.Insert, new RoutineReference(new SqlIdentifier("app"), new SqlIdentifier("log")));
        var desired = new Trigger(new SqlIdentifier("audit"), TriggerTiming.After, TriggerEvent.Insert, new RoutineReference(new SqlIdentifier("app"), new SqlIdentifier("log")));

        var diffs = DiffTriggers([current], [desired]);

        diffs.Select(d => d.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add], ignoreOrder: true);
    }

    [Fact]
    public void Compare_UpdateOfColumnsChange_IsStructural()
    {
        var current = new Trigger(new SqlIdentifier("audit"), TriggerTiming.After, TriggerEvent.Update, new RoutineReference(new SqlIdentifier("app"), new SqlIdentifier("log")), updateOfColumns: [new SqlIdentifier("a")]);
        var desired = new Trigger(new SqlIdentifier("audit"), TriggerTiming.After, TriggerEvent.Update, new RoutineReference(new SqlIdentifier("app"), new SqlIdentifier("log")), updateOfColumns: [new SqlIdentifier("a"), new SqlIdentifier("b")]);

        DiffTriggers([current], [desired]).Select(d => d.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add], ignoreOrder: true);
    }

    [Fact]
    public void Compare_TriggerBodyChange_IsStructural()
    {
        // An inline-body change is part of structural equality, so it is a drop + recreate (not a comment-only modify).
        var current = new Trigger(new SqlIdentifier("audit"), TriggerTiming.After, TriggerEvent.Insert, body: new SqlText("BEGIN SELECT 1 END"));
        var desired = new Trigger(new SqlIdentifier("audit"), TriggerTiming.After, TriggerEvent.Insert, body: new SqlText("BEGIN SELECT 2 END"));

        DiffTriggers([current], [desired]).Select(d => d.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add], ignoreOrder: true);
    }
}
