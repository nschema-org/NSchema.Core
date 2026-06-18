using NSchema.Diff.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Constraints;
using NSchema.Schema.Model.Domains;
using NSchema.Schema.Model.Schemas;

namespace NSchema.Tests.Diff;

public partial class SchemaComparerTests
{
    // -------------------------------------------------------------------------
    // Domains
    // -------------------------------------------------------------------------

    /// <summary>Diffs two <c>app</c> schemas holding the given domains, returning the single domain diff (null when unchanged).</summary>
    private DomainDiff? DiffDomains(IReadOnlyList<Domain> current, IReadOnlyList<Domain> desired) => _sut
        .Compare(Db(new SchemaDefinition("app", Domains: current)), Db(new SchemaDefinition("app", Domains: desired)))
        .Schemas.SingleOrDefault()?.Domains.SingleOrDefault();

    [Fact]
    public void Compare_NewDomain_IsAddCarryingDefinition()
    {
        var diff = DiffDomains([], [new Domain("typeid", SqlType.Text, Default: "''", NotNull: true)]);

        diff!.Kind.ShouldBe(ChangeKind.Add);
        diff.Definition!.DataType.ShouldBe(SqlType.Text);
        diff.Definition.NotNull.ShouldBeTrue();
    }

    [Fact]
    public void Compare_RemovedDomain_IsRemove()
        => DiffDomains([new Domain("typeid", SqlType.Text)], [])!.Kind.ShouldBe(ChangeKind.Remove);

    [Fact]
    public void Compare_UnchangedDomain_ProducesNoDiff()
        => DiffDomains([new Domain("typeid", SqlType.Text)], [new Domain("typeid", SqlType.Text)]).ShouldBeNull();

    [Fact]
    public void Compare_BaseTypeChange_RequiresRecreate()
    {
        // Postgres has no ALTER DOMAIN … TYPE, so a base-type change drops + recreates.
        var diff = DiffDomains([new Domain("d", SqlType.Text)], [new Domain("d", SqlType.VarChar(255))]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.DataType.ShouldBe(new ValueChange<SqlType>(SqlType.Text, SqlType.VarChar(255)));
        diff.RequiresRecreate.ShouldBeTrue();
        diff.Definition.ShouldNotBeNull(); // the desired domain rides along for the recreate
    }

    [Fact]
    public void Compare_DefaultChange_IsInPlace()
    {
        var diff = DiffDomains([new Domain("d", SqlType.Text, Default: "'a'")], [new Domain("d", SqlType.Text, Default: "'b'")]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.Default.ShouldBe(new ValueChange<string>("'a'", "'b'"));
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_NotNullChange_IsInPlace()
    {
        var diff = DiffDomains([new Domain("d", SqlType.Text)], [new Domain("d", SqlType.Text, NotNull: true)]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.NotNull.ShouldBe(new ValueChange<bool>(false, true));
    }

    [Fact]
    public void Compare_CheckAddedAndRemoved_AreInPlace()
    {
        var diff = DiffDomains(
            [new Domain("d", SqlType.Text, Checks: [new CheckConstraint("old_chk", "VALUE <> ''")])],
            [new Domain("d", SqlType.Text, Checks: [new CheckConstraint("new_chk", "length(VALUE) > 0")])]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.Checks.Select(c => (c.Kind, c.Name)).ShouldBe(
            [(ChangeKind.Remove, "old_chk"), (ChangeKind.Add, "new_chk")], ignoreOrder: true);
    }

    [Fact]
    public void Compare_RenamedDomain_SetsRenamedFrom()
    {
        var diff = DiffDomains([new Domain("old_d", SqlType.Text)], [new Domain("d", SqlType.Text, OldName: "old_d")]);

        diff!.RenamedFrom.ShouldBe("old_d");
        diff.RequiresRecreate.ShouldBeFalse(); // a rename is in place, not a recreate
    }

    [Fact]
    public void Compare_CommentOnlyChange_IsModify()
    {
        var diff = DiffDomains([new Domain("d", SqlType.Text, Comment: "old")], [new Domain("d", SqlType.Text, Comment: "new")]);

        diff!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_PartialSchema_LeavesUnmanagedDomainAlone()
        => _sut.Compare(
            Db(new SchemaDefinition("app", Domains: [new Domain("d", SqlType.Text)])),
            Db(new SchemaDefinition("app", IsPartial: true)))
            .Schemas.ShouldBeEmpty();

    [Fact]
    public void Compare_PartialSchema_DropsExplicitlyDroppedDomain()
        => _sut.Compare(
            Db(new SchemaDefinition("app", Domains: [new Domain("d", SqlType.Text)])),
            Db(new SchemaDefinition("app", IsPartial: true, DroppedDomains: ["d"])))
            .Schemas.ShouldHaveSingleItem().Domains.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
}
