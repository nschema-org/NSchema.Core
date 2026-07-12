using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Enums;
using NSchema.Project.Domain.Models.Enums;
using NSchema.Project.Domain.Models.Schemas;

namespace NSchema.Tests.Diff;

public partial class SchemaComparerTests
{
    // -------------------------------------------------------------------------
    // Enums
    // -------------------------------------------------------------------------

    /// <summary>Diffs two <c>app</c> schemas holding the given enums, returning the single enum diff (null when unchanged).</summary>
    private EnumDiff? DiffEnums(IReadOnlyList<EnumType> current, IReadOnlyList<EnumType> desired) => _sut
        .Compare(Db(new SchemaDefinition("app", Enums: current)), Db(new SchemaDefinition("app", Enums: desired)))
        .Schemas.SingleOrDefault()?.Enums.SingleOrDefault();

    [Fact]
    public void Compare_NewEnum_IsAddCarryingDefinition()
    {
        var diff = DiffEnums([], [new EnumType("status", ["a", "b"])]);

        diff!.Kind.ShouldBe(ChangeKind.Add);
        diff.Definition!.Values.ShouldBe(["a", "b"]);
        diff.AddedValues.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_RemovedEnum_IsRemove()
    {
        var diff = DiffEnums([new EnumType("status", ["a"])], []);

        diff!.Kind.ShouldBe(ChangeKind.Remove);
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_UnchangedEnum_ProducesNoDiff()
        => DiffEnums([new EnumType("status", ["a", "b"])], [new EnumType("status", ["a", "b"])]).ShouldBeNull();

    [Fact]
    public void Compare_RenamedEnum_SetsRenamedFrom()
    {
        var diff = DiffEnums(
            [new EnumType("state", ["a"])],
            [new EnumType("status", ["a"], OldName: "state")]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.RenamedFrom.ShouldBe("state");
        diff.Name.ShouldBe("status");
        diff.Values.ShouldBeNull(); // values unchanged, so it is a rename only
    }

    [Fact]
    public void Compare_EnumCommentOnlyChange_IsModify()
    {
        var diff = DiffEnums(
            [new EnumType("status", ["a"], Comment: "old")],
            [new EnumType("status", ["a"], Comment: "new")]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Values.ShouldBeNull();
    }

    [Fact]
    public void Compare_EnumAppendedValue_AnchorsAfterThePreviousValue()
    {
        var diff = DiffEnums(
            [new EnumType("status", ["a", "b"])],
            [new EnumType("status", ["a", "b", "c"])]);

        diff!.AddedValues.ShouldHaveSingleItem().ShouldBe(new EnumValueAddition("c", After: "b"));
        diff.RequiresRecreate.ShouldBeFalse();
        diff.Values!.Old.ShouldBe(["a", "b"]);
        diff.Values.New.ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public void Compare_EnumHeadInsertion_AnchorsBeforeTheFirstExistingValue()
    {
        var diff = DiffEnums(
            [new EnumType("status", ["c"])],
            [new EnumType("status", ["a", "b", "c"])]);

        // a goes before the only existing value; b then chains after a, which exists once a is added.
        diff!.AddedValues.ShouldBe([
            new EnumValueAddition("a", Before: "c"),
            new EnumValueAddition("b", After: "a"),
        ]);
    }

    [Fact]
    public void Compare_EnumMiddleInsertion_AnchorsAfterThePrecedingValue()
    {
        var diff = DiffEnums(
            [new EnumType("status", ["a", "c"])],
            [new EnumType("status", ["a", "b", "c"])]);

        diff!.AddedValues.ShouldHaveSingleItem().ShouldBe(new EnumValueAddition("b", After: "a"));
    }

    [Fact]
    public void Compare_EnumConsecutiveInsertions_ChainTheirAnchors()
    {
        var diff = DiffEnums(
            [new EnumType("status", ["a"])],
            [new EnumType("status", ["a", "b", "c"])]);

        diff!.AddedValues.ShouldBe([
            new EnumValueAddition("b", After: "a"),
            new EnumValueAddition("c", After: "b"),
        ]);
    }

    [Fact]
    public void Compare_EnumWithEmptyCurrent_AppendsWithoutAnchors()
    {
        var diff = DiffEnums(
            [new EnumType("status")],
            [new EnumType("status", ["a", "b"])]);

        diff!.AddedValues.ShouldBe([
            new EnumValueAddition("a"),
            new EnumValueAddition("b", After: "a"),
        ]);
    }

    [Fact]
    public void Compare_EnumValueRemoval_RequiresRecreate()
    {
        var diff = DiffEnums(
            [new EnumType("status", ["a", "b", "c"])],
            [new EnumType("status", ["a", "c"])]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.AddedValues.ShouldBeEmpty();
        diff.RequiresRecreate.ShouldBeTrue();
        // The old and new lists are still recorded so drift can display the change.
        diff.Values!.Old.ShouldBe(["a", "b", "c"]);
        diff.Values.New.ShouldBe(["a", "c"]);
    }

    [Fact]
    public void Compare_EnumValueReorder_RequiresRecreate()
    {
        var diff = DiffEnums(
            [new EnumType("status", ["a", "b"])],
            [new EnumType("status", ["b", "a"])]);

        diff!.AddedValues.ShouldBeEmpty();
        diff.RequiresRecreate.ShouldBeTrue();
    }

    [Fact]
    public void Compare_EnumValues_AreCaseSensitive()
        => DiffEnums([new EnumType("status", ["Active"])], [new EnumType("status", ["active"])])!
            .RequiresRecreate.ShouldBeTrue();

    [Fact]
    public void Compare_PartialSchema_LeavesUnmanagedEnumAlone()
    {
        var diff = _sut.Compare(
            Db(new SchemaDefinition("app", Enums: [new EnumType("status", ["a"])])),
            Db(new SchemaDefinition("app", IsPartial: true)));

        diff.Schemas.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_PartialSchema_DropsExplicitlyDroppedEnum()
    {
        var diff = _sut.Compare(
            Db(new SchemaDefinition("app", Enums: [new EnumType("status", ["a"])])),
            Db(new SchemaDefinition("app", IsPartial: true, DroppedEnums: ["status"])));

        diff.Schemas.ShouldHaveSingleItem().Enums.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
    }
}
