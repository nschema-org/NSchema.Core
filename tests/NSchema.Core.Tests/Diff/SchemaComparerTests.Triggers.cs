using NSchema.Diff.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Triggers;

namespace NSchema.Tests.Diff;

public partial class SchemaComparerTests
{
    // -------------------------------------------------------------------------
    // Triggers (table members)
    // -------------------------------------------------------------------------

    private IReadOnlyList<TriggerDiff> DiffTriggers(IReadOnlyList<Trigger> current, IReadOnlyList<Trigger> desired) =>
        DiffTable(
            new Table("t", Columns: [new Column("id", SqlType.Int)], Triggers: current),
            new Table("t", Columns: [new Column("id", SqlType.Int)], Triggers: desired))?.Triggers ?? [];

    private static Trigger AfterInsert(string name, string function = "app.log", string? comment = null) =>
        new(name, TriggerTiming.After, TriggerEvent.Insert, function, TriggerLevel.Row, Comment: comment);

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
        var current = new Trigger("audit", TriggerTiming.Before, TriggerEvent.Insert, "app.log");
        var desired = new Trigger("audit", TriggerTiming.After, TriggerEvent.Insert, "app.log");

        var diffs = DiffTriggers([current], [desired]);

        diffs.Select(d => d.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add], ignoreOrder: true);
    }

    [Fact]
    public void Compare_UpdateOfColumnsChange_IsStructural()
    {
        var current = new Trigger("audit", TriggerTiming.After, TriggerEvent.Update, "app.log", UpdateOfColumns: ["a"]);
        var desired = new Trigger("audit", TriggerTiming.After, TriggerEvent.Update, "app.log", UpdateOfColumns: ["a", "b"]);

        DiffTriggers([current], [desired]).Select(d => d.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add], ignoreOrder: true);
    }
}
