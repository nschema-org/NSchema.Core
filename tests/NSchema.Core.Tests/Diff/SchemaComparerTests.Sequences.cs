using NSchema.Diff.Model;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Sequences;

namespace NSchema.Tests.Diff;

public partial class SchemaComparerTests
{
    // -------------------------------------------------------------------------
    // Sequences
    // -------------------------------------------------------------------------

    /// <summary>Diffs two <c>app</c> schemas holding the given sequences, returning the single sequence diff (null when unchanged).</summary>
    private SequenceDiff? DiffSequences(IReadOnlyList<Sequence> current, IReadOnlyList<Sequence> desired) => _sut
        .Compare(Db(new SchemaDefinition("app", Sequences: current)), Db(new SchemaDefinition("app", Sequences: desired)))
        .Schemas.SingleOrDefault()?.Sequences.SingleOrDefault();

    [Fact]
    public void Compare_NewSequence_IsAddCarryingDefinition()
    {
        var diff = DiffSequences([], [new Sequence("order_id", new SequenceOptions(StartWith: 100))]);

        diff!.Kind.ShouldBe(ChangeKind.Add);
        diff.Definition!.Options.StartWith.ShouldBe(100);
    }

    [Fact]
    public void Compare_RemovedSequence_IsRemove()
    {
        var diff = DiffSequences([new Sequence("order_id")], []);

        diff!.Kind.ShouldBe(ChangeKind.Remove);
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_UnchangedSequence_ProducesNoDiff()
        => DiffSequences(
            [new Sequence("order_id", new SequenceOptions(StartWith: 1, IncrementBy: 5))],
            [new Sequence("order_id", new SequenceOptions(StartWith: 1, IncrementBy: 5))]).ShouldBeNull();

    [Fact]
    public void Compare_SequenceWithNullAndEmptyOptions_ProducesNoDiff()
        // The model normalizes null options to an empty set, so the two spellings never read as a change.
        => DiffSequences([new Sequence("order_id")], [new Sequence("order_id", new SequenceOptions())]).ShouldBeNull();

    [Fact]
    public void Compare_RenamedSequence_SetsRenamedFrom()
    {
        var diff = DiffSequences(
            [new Sequence("bill_id")],
            [new Sequence("invoice_id", OldName: "bill_id")]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.RenamedFrom.ShouldBe("bill_id");
        diff.Name.ShouldBe("invoice_id");
        diff.Options.ShouldBeNull(); // options unchanged, so it is a rename only
    }

    [Fact]
    public void Compare_SequenceCommentOnlyChange_IsModify()
    {
        var diff = DiffSequences(
            [new Sequence("order_id", Comment: "old")],
            [new Sequence("order_id", Comment: "new")]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Options.ShouldBeNull();
    }

    [Fact]
    public void Compare_SequenceOptionsChange_CarriesOldAndNewOptions()
    {
        var diff = DiffSequences(
            [new Sequence("order_id", new SequenceOptions(StartWith: 1, IncrementBy: 1))],
            [new Sequence("order_id", new SequenceOptions(StartWith: 1, IncrementBy: 5, Cycle: true))]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.Options.ShouldBe(new ValueChange<SequenceOptions>(
            new SequenceOptions(StartWith: 1, IncrementBy: 1),
            new SequenceOptions(StartWith: 1, IncrementBy: 5, Cycle: true)));
    }

    [Fact]
    public void Compare_PartialSchema_LeavesUnmanagedSequenceAlone()
    {
        var diff = _sut.Compare(
            Db(new SchemaDefinition("app", Sequences: [new Sequence("order_id")])),
            Db(new SchemaDefinition("app", IsPartial: true)));

        diff.Schemas.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_PartialSchema_DropsExplicitlyDroppedSequence()
    {
        var diff = _sut.Compare(
            Db(new SchemaDefinition("app", Sequences: [new Sequence("order_id")])),
            Db(new SchemaDefinition("app", IsPartial: true, DroppedSequences: ["order_id"])));

        diff.Schemas.ShouldHaveSingleItem().Sequences.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
    }
}
