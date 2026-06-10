using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Tests.Diff;

public partial class SchemaComparerTests
{
    // -------------------------------------------------------------------------
    // Functions
    // -------------------------------------------------------------------------

    private const string Def = "RETURNS int LANGUAGE sql AS $$ SELECT 1; $$";

    /// <summary>Diffs two <c>app</c> schemas holding the given functions, returning the single function diff (null when unchanged).</summary>
    private FunctionDiff? DiffFunctions(IReadOnlyList<Function> current, IReadOnlyList<Function> desired) => _sut
        .Compare(Db(new SchemaDefinition("app", Functions: current)), Db(new SchemaDefinition("app", Functions: desired)))
        .Schemas.SingleOrDefault()?.Functions.SingleOrDefault();

    [Fact]
    public void Compare_NewFunction_IsAddCarryingDefinition()
    {
        var diff = DiffFunctions([], [new Function("f", "a int", Def)]);

        diff!.Kind.ShouldBe(ChangeKind.Add);
        diff.Definition!.Arguments.ShouldBe("a int");
        diff.RequiresRecreate.ShouldBeFalse();
    }

    [Fact]
    public void Compare_RemovedFunction_IsRemove()
        => DiffFunctions([new Function("f", "", Def)], [])!.Kind.ShouldBe(ChangeKind.Remove);

    [Fact]
    public void Compare_UnchangedFunction_ProducesNoDiff()
        => DiffFunctions([new Function("f", "a int", Def)], [new Function("f", "a int", Def)]).ShouldBeNull();

    [Fact]
    public void Compare_FunctionBodyChange_IsReplaceNotRecreate()
    {
        var diff = DiffFunctions(
            [new Function("f", "a int", "RETURNS int AS $$ SELECT 1 $$")],
            [new Function("f", "a int", "RETURNS int AS $$ SELECT 2 $$")]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.Definition.ShouldNotBeNull();
        diff.Arguments.ShouldBeNull();
        diff.RequiresRecreate.ShouldBeFalse();
    }

    [Fact]
    public void Compare_FunctionArgumentsChange_RequiresRecreate()
    {
        var diff = DiffFunctions(
            [new Function("f", "a int", Def)],
            [new Function("f", "a int, b text", Def)]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.Arguments.ShouldBe(new ValueChange<string>("a int", "a int, b text"));
        diff.Definition.ShouldNotBeNull(); // the desired definition rides along for the recreate
        diff.RequiresRecreate.ShouldBeTrue();
    }

    [Fact]
    public void Compare_FunctionCosmeticWhitespace_ProducesNoDiff()
        // Both the argument list and the definition are compared for cosmetic equivalence, so a database's
        // re-emission (collapsed whitespace, trailing terminator) does not read as drift.
        => DiffFunctions(
            [new Function("f", "a  int,  b text", "RETURNS int\n  LANGUAGE sql AS $$ SELECT 1; $$;")],
            [new Function("f", "a int, b text", "RETURNS int LANGUAGE sql AS $$ SELECT 1; $$")]).ShouldBeNull();

    [Fact]
    public void Compare_RenamedFunction_SetsRenamedFrom()
    {
        var diff = DiffFunctions(
            [new Function("old_f", "", Def)],
            [new Function("f", "", Def, OldName: "old_f")]);

        diff!.RenamedFrom.ShouldBe("old_f");
        diff.Definition.ShouldBeNull(); // nothing else changed, so it is a rename only
        diff.RequiresRecreate.ShouldBeFalse();
    }

    [Fact]
    public void Compare_RenamedFunctionWithArgumentsChange_CarriesBoth()
    {
        var diff = DiffFunctions(
            [new Function("old_f", "a int", Def)],
            [new Function("f", "a int, b text", Def, OldName: "old_f")]);

        diff!.RenamedFrom.ShouldBe("old_f");
        diff.RequiresRecreate.ShouldBeTrue();
    }

    [Fact]
    public void Compare_FunctionCommentOnlyChange_IsModifyWithoutDefinition()
    {
        var diff = DiffFunctions(
            [new Function("f", "", Def, Comment: "old")],
            [new Function("f", "", Def, Comment: "new")]);

        diff!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_FunctionReplacedByProcedure_IsRemoveAndAdd_NotModify()
    {
        // Functions and procedures are separate kinds: a same-name swap is a drop of one and an add of the
        // other, never a modify. (The shared-name-space rule rejects declaring both simultaneously upstream.)
        var diff = _sut.Compare(
            Db(new SchemaDefinition("app", Functions: [new Function("r", "", Def)])),
            Db(new SchemaDefinition("app", Procedures: [new Procedure("r", "", "LANGUAGE sql AS $$ SELECT 1 $$")])));

        var schema = diff.Schemas.ShouldHaveSingleItem();
        schema.Functions.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
        schema.Procedures.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Add);
    }

    [Fact]
    public void Compare_PartialSchema_LeavesUnmanagedFunctionAlone()
        => _sut.Compare(
            Db(new SchemaDefinition("app", Functions: [new Function("f", "", Def)])),
            Db(new SchemaDefinition("app", IsPartial: true)))
            .Schemas.ShouldBeEmpty();

    [Fact]
    public void Compare_PartialSchema_DropsExplicitlyDroppedFunction()
        => _sut.Compare(
            Db(new SchemaDefinition("app", Functions: [new Function("f", "", Def)])),
            Db(new SchemaDefinition("app", IsPartial: true, DroppedFunctions: ["f"])))
            .Schemas.ShouldHaveSingleItem().Functions.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
}
