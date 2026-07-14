using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Sequences;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Sequences;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Sequences
    // -------------------------------------------------------------------------

    /// <summary>Diffs two <c>app</c> schemas holding the given sequences, returning the single sequence diff (null when unchanged).</summary>
    private SequenceDiff? DiffSequences(IReadOnlyList<Sequence> current, IReadOnlyList<Sequence> desired, ProjectDirectives? directives = null) =>
        Compare(Db(new Schema(new SqlIdentifier("app"), Sequences: current)), Db(new Schema(new SqlIdentifier("app"), Sequences: desired)), directives)
        .Schemas.SingleOrDefault()?.Sequences.SingleOrDefault();

    [Fact]
    public void Compare_NewSequence_IsAddCarryingDefinition()
    {
        var diff = DiffSequences([], [new Sequence(new SqlIdentifier("order_id"), new SequenceOptions(StartWith: 100))]);

        diff!.Kind.ShouldBe(ChangeKind.Add);
        diff.Definition!.Options.StartWith.ShouldBe(100);
    }

    [Fact]
    public void Compare_RemovedSequence_IsRemove()
    {
        var diff = DiffSequences([new Sequence(new SqlIdentifier("order_id"))], []);

        diff!.Kind.ShouldBe(ChangeKind.Remove);
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_UnchangedSequence_ProducesNoDiff()
        => DiffSequences(
            [new Sequence(new SqlIdentifier("order_id"), new SequenceOptions(StartWith: 1, IncrementBy: 5))],
            [new Sequence(new SqlIdentifier("order_id"), new SequenceOptions(StartWith: 1, IncrementBy: 5))]).ShouldBeNull();

    [Fact]
    public void Compare_SequenceWithNullAndEmptyOptions_ProducesNoDiff()
        // The model normalizes null options to an empty set, so the two spellings never read as a change.
        => DiffSequences([new Sequence(new SqlIdentifier("order_id"))], [new Sequence(new SqlIdentifier("order_id"), new SequenceOptions())]).ShouldBeNull();

    [Fact]
    public void Compare_RenamedSequence_SetsRenamedFrom()
    {
        var diff = DiffSequences(
            [new Sequence(new SqlIdentifier("bill_id"))],
            [new Sequence(new SqlIdentifier("invoice_id"))],
            new ProjectDirectives(Sequences: new SequenceDirectives(Renames: [new ObjectRename(App("bill_id"), new SqlIdentifier("invoice_id"))])));

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.RenamedFrom.ShouldBe("bill_id");
        diff.Name.ShouldBe("invoice_id");
        diff.Options.ShouldBeNull(); // options unchanged, so it is a rename only
    }

    [Fact]
    public void Compare_SequenceCommentOnlyChange_IsModify()
    {
        var diff = DiffSequences(
            [new Sequence(new SqlIdentifier("order_id"), Comment: "old")],
            [new Sequence(new SqlIdentifier("order_id"), Comment: "new")]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Options.ShouldBeNull();
    }

    [Fact]
    public void Compare_SequenceOptionsChange_CarriesOldAndNewOptions()
    {
        var diff = DiffSequences(
            [new Sequence(new SqlIdentifier("order_id"), new SequenceOptions(StartWith: 1, IncrementBy: 1))],
            [new Sequence(new SqlIdentifier("order_id"), new SequenceOptions(StartWith: 1, IncrementBy: 5, Cycle: true))]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.Options.ShouldBe(new ValueChange<SequenceOptions>(
            new SequenceOptions(StartWith: 1, IncrementBy: 1),
            new SequenceOptions(StartWith: 1, IncrementBy: 5, Cycle: true)));
    }

    [Fact]
    public void Compare_PartialSchema_LeavesUnmanagedSequenceAlone()
    {
        var diff = Compare(
            Db(new Schema(new SqlIdentifier("app"), Sequences: [new Sequence(new SqlIdentifier("order_id"))])),
            Db(new Schema(new SqlIdentifier("app"))), PartialApp());

        diff.Schemas.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_PartialSchema_DropsExplicitlyDroppedSequence()
    {
        var diff = Compare(
            Db(new Schema(new SqlIdentifier("app"), Sequences: [new Sequence(new SqlIdentifier("order_id"))])),
            Db(new Schema(new SqlIdentifier("app"))),
            PartialApp() with { Sequences = new SequenceDirectives(Drops: [App("order_id")]) });

        diff.Schemas.ShouldHaveSingleItem().Sequences.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
    }
}
