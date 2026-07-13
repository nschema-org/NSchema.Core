using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Routines;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Routines;
using NSchema.Project.Domain.Models.Schemas;

namespace NSchema.Tests.Diff;

public partial class SchemaComparerTests
{
    // -------------------------------------------------------------------------
    // Routines (functions and procedures — one type with a kind discriminator)
    // -------------------------------------------------------------------------

    private const string Def = "RETURNS int LANGUAGE sql AS $$ SELECT 1; $$";
    private const string ProcDef = "LANGUAGE sql AS $$ DELETE FROM app.t; $$";

    private static Routine Fn(string name, string args, string def, SqlIdentifier? oldName = null, string? comment = null) =>
        new(new SqlIdentifier(name), RoutineKind.Function, args, def, oldName, comment);

    private static Routine Proc(string name, string args, string def, SqlIdentifier? oldName = null, string? comment = null) =>
        new(new SqlIdentifier(name), RoutineKind.Procedure, args, def, oldName, comment);

    /// <summary>Diffs two <c>app</c> schemas holding the given routines, returning the single routine diff (null when unchanged).</summary>
    private RoutineDiff? DiffRoutines(IReadOnlyList<Routine> current, IReadOnlyList<Routine> desired) => _sut
        .Compare(Db(new SchemaDefinition(new SqlIdentifier("app"), Routines: current)), Db(new SchemaDefinition(new SqlIdentifier("app"), Routines: desired)))
        .Schemas.SingleOrDefault()?.Routines.SingleOrDefault();

    [Fact]
    public void Compare_NewFunction_IsAddCarryingDefinition()
    {
        var diff = DiffRoutines([], [Fn("f", "a int", Def)]);

        diff!.Kind.ShouldBe(ChangeKind.Add);
        diff.RoutineKind.ShouldBe(RoutineKind.Function);
        diff.Definition!.Arguments.ShouldBe("a int");
        diff.RequiresRecreate.ShouldBeFalse();
    }

    [Fact]
    public void Compare_NewProcedure_CarriesProcedureKind()
        => DiffRoutines([], [Proc("p", "before date", ProcDef)])!.RoutineKind.ShouldBe(RoutineKind.Procedure);

    [Fact]
    public void Compare_RemovedRoutine_IsRemoveCarryingKind()
    {
        var diff = DiffRoutines([Proc("p", "", ProcDef)], []);
        diff!.Kind.ShouldBe(ChangeKind.Remove);
        diff.RoutineKind.ShouldBe(RoutineKind.Procedure);
    }

    [Fact]
    public void Compare_UnchangedRoutine_ProducesNoDiff()
        => DiffRoutines([Fn("f", "a int", Def)], [Fn("f", "a int", Def)]).ShouldBeNull();

    [Fact]
    public void Compare_BodyChange_IsReplaceNotRecreate()
    {
        var diff = DiffRoutines(
            [Fn("f", "a int", "RETURNS int AS $$ SELECT 1 $$")],
            [Fn("f", "a int", "RETURNS int AS $$ SELECT 2 $$")]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.Definition.ShouldNotBeNull();
        diff.Arguments.ShouldBeNull();
        diff.RequiresRecreate.ShouldBeFalse();
    }

    [Fact]
    public void Compare_ArgumentsChange_RequiresRecreate()
    {
        var diff = DiffRoutines([Fn("f", "a int", Def)], [Fn("f", "a int, b text", Def)]);

        diff!.Arguments.ShouldBe(new ValueChange<string>("a int", "a int, b text"));
        diff.Definition.ShouldNotBeNull(); // the desired definition rides along for the recreate
        diff.RequiresRecreate.ShouldBeTrue();
    }

    [Fact]
    public void Compare_CosmeticWhitespace_ProducesNoDiff()
        // Both the argument list and the definition are compared for cosmetic equivalence.
        => DiffRoutines(
            [Fn("f", "a  int,  b text", "RETURNS int\n  LANGUAGE sql AS $$ SELECT 1; $$;")],
            [Fn("f", "a int, b text", "RETURNS int LANGUAGE sql AS $$ SELECT 1; $$")]).ShouldBeNull();

    [Fact]
    public void Compare_Renamed_SetsRenamedFrom()
    {
        var diff = DiffRoutines([Fn("old_f", "", Def)], [Fn("f", "", Def, oldName: new SqlIdentifier("old_f"))]);

        diff!.RenamedFrom.ShouldBe("old_f");
        diff.Definition.ShouldBeNull(); // nothing else changed, so it is a rename only
        diff.RequiresRecreate.ShouldBeFalse();
    }

    [Fact]
    public void Compare_CommentOnlyChange_IsModifyWithoutDefinition()
    {
        var diff = DiffRoutines([Proc("p", "", ProcDef, comment: "old")], [Proc("p", "", ProcDef, comment: "new")]);

        diff!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_FunctionSwappedToProcedureSameName_IsRecreate()
    {
        // Functions and procedures share one name space, so a same-name swap matches by name and is a kind
        // change — applied as a drop + recreate (there is no in-place conversion).
        var diff = DiffRoutines([Fn("r", "", Def)], [Proc("r", "", ProcDef)]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.RoutineKind.ShouldBe(RoutineKind.Procedure);
        diff.RequiresRecreate.ShouldBeTrue();
        diff.Definition.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_PartialSchema_LeavesUnmanagedRoutineAlone()
        => _sut.Compare(
            Db(new SchemaDefinition(new SqlIdentifier("app"), Routines: [Fn("f", "", Def)])),
            Db(new SchemaDefinition(new SqlIdentifier("app"), IsPartial: true)))
            .Schemas.ShouldBeEmpty();

    [Fact]
    public void Compare_PartialSchema_DropsExplicitlyDroppedRoutine()
        => _sut.Compare(
            Db(new SchemaDefinition(new SqlIdentifier("app"), Routines: [Fn("f", "", Def)])),
            Db(new SchemaDefinition(new SqlIdentifier("app"), IsPartial: true, DroppedRoutines: [new SqlIdentifier("f")])))
            .Schemas.ShouldHaveSingleItem().Routines.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
}
