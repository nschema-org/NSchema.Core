using NSchema.Diff.Model;
using NSchema.Schema.Model.Procedures;
using NSchema.Schema.Model.Schemas;

namespace NSchema.Tests.Diff;

public partial class SchemaComparerTests
{
    // -------------------------------------------------------------------------
    // Procedures
    // -------------------------------------------------------------------------

    private const string ProcDef = "LANGUAGE sql AS $$ DELETE FROM app.t; $$";

    /// <summary>Diffs two <c>app</c> schemas holding the given procedures, returning the single procedure diff (null when unchanged).</summary>
    private ProcedureDiff? DiffProcedures(IReadOnlyList<Procedure> current, IReadOnlyList<Procedure> desired) => _sut
        .Compare(Db(new SchemaDefinition("app", Procedures: current)), Db(new SchemaDefinition("app", Procedures: desired)))
        .Schemas.SingleOrDefault()?.Procedures.SingleOrDefault();

    [Fact]
    public void Compare_NewProcedure_IsAddCarryingDefinition()
    {
        var diff = DiffProcedures([], [new Procedure("p", "before date", ProcDef)]);

        diff!.Kind.ShouldBe(ChangeKind.Add);
        diff.Definition!.Arguments.ShouldBe("before date");
    }

    [Fact]
    public void Compare_RemovedProcedure_IsRemove()
        => DiffProcedures([new Procedure("p", "", ProcDef)], [])!.Kind.ShouldBe(ChangeKind.Remove);

    [Fact]
    public void Compare_UnchangedProcedure_ProducesNoDiff()
        => DiffProcedures([new Procedure("p", "", ProcDef)], [new Procedure("p", "", ProcDef)]).ShouldBeNull();

    [Fact]
    public void Compare_ProcedureBodyChange_IsReplaceNotRecreate()
    {
        var diff = DiffProcedures(
            [new Procedure("p", "", "LANGUAGE sql AS $$ DELETE FROM app.t $$")],
            [new Procedure("p", "", "LANGUAGE sql AS $$ TRUNCATE app.t $$")]);

        diff!.Arguments.ShouldBeNull();
        diff.RequiresRecreate.ShouldBeFalse();
        diff.Definition.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_ProcedureArgumentsChange_RequiresRecreate()
    {
        var diff = DiffProcedures(
            [new Procedure("p", "", ProcDef)],
            [new Procedure("p", "before date", ProcDef)]);

        diff!.RequiresRecreate.ShouldBeTrue();
        diff.Arguments.ShouldBe(new ValueChange<string>("", "before date"));
    }

    [Fact]
    public void Compare_RenamedProcedure_SetsRenamedFrom()
        => DiffProcedures(
            [new Procedure("old_p", "", ProcDef)],
            [new Procedure("p", "", ProcDef, OldName: "old_p")])!
            .RenamedFrom.ShouldBe("old_p");

    [Fact]
    public void Compare_ProcedureCommentOnlyChange_IsModifyWithoutDefinition()
    {
        var diff = DiffProcedures(
            [new Procedure("p", "", ProcDef, Comment: "old")],
            [new Procedure("p", "", ProcDef, Comment: "new")]);

        diff!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_PartialSchema_LeavesUnmanagedProcedureAlone()
        => _sut.Compare(
            Db(new SchemaDefinition("app", Procedures: [new Procedure("p", "", ProcDef)])),
            Db(new SchemaDefinition("app", IsPartial: true)))
            .Schemas.ShouldBeEmpty();

    [Fact]
    public void Compare_PartialSchema_DropsExplicitlyDroppedProcedure()
        => _sut.Compare(
            Db(new SchemaDefinition("app", Procedures: [new Procedure("p", "", ProcDef)])),
            Db(new SchemaDefinition("app", IsPartial: true, DroppedProcedures: ["p"])))
            .Schemas.ShouldHaveSingleItem().Procedures.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
}
