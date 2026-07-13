using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Domains;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Constraints;
using NSchema.Project.Domain.Models.Domains;
using NSchema.Project.Domain.Models.Schemas;

namespace NSchema.Tests.Diff;

public partial class SchemaComparerTests
{
    // -------------------------------------------------------------------------
    // Domains
    // -------------------------------------------------------------------------

    /// <summary>Diffs two <c>app</c> schemas holding the given domains, returning the single domain diff (null when unchanged).</summary>
    private DomainDiff? DiffDomains(IReadOnlyList<DomainDefinition> current, IReadOnlyList<DomainDefinition> desired) => _sut
        .Compare(Db(new SchemaDefinition(new SqlIdentifier("app"), Domains: current)), Db(new SchemaDefinition(new SqlIdentifier("app"), Domains: desired)))
        .Schemas.SingleOrDefault()?.Domains.SingleOrDefault();

    [Fact]
    public void Compare_NewDomain_IsAddCarryingDefinition()
    {
        var diff = DiffDomains([], [new DomainDefinition(new SqlIdentifier("typeid"), SqlType.Text, Default: "''", NotNull: true)]);

        diff!.Kind.ShouldBe(ChangeKind.Add);
        diff.Definition!.DataType.ShouldBe(SqlType.Text);
        diff.Definition.NotNull.ShouldBeTrue();
    }

    [Fact]
    public void Compare_RemovedDomain_IsRemove()
        => DiffDomains([new DomainDefinition(new SqlIdentifier("typeid"), SqlType.Text)], [])!.Kind.ShouldBe(ChangeKind.Remove);

    [Fact]
    public void Compare_UnchangedDomain_ProducesNoDiff()
        => DiffDomains([new DomainDefinition(new SqlIdentifier("typeid"), SqlType.Text)], [new DomainDefinition(new SqlIdentifier("typeid"), SqlType.Text)]).ShouldBeNull();

    [Fact]
    public void Compare_BaseTypeChange_RequiresRecreate()
    {
        // Postgres has no ALTER DOMAIN … TYPE, so a base-type change drops + recreates.
        var diff = DiffDomains([new DomainDefinition(new SqlIdentifier("d"), SqlType.Text)], [new DomainDefinition(new SqlIdentifier("d"), SqlType.VarChar(255))]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.DataType.ShouldBe(new ValueChange<SqlType>(SqlType.Text, SqlType.VarChar(255)));
        diff.RequiresRecreate.ShouldBeTrue();
        diff.Definition.ShouldNotBeNull(); // the desired domain rides along for the recreate
    }

    [Fact]
    public void Compare_DefaultChange_IsInPlace()
    {
        var diff = DiffDomains([new DomainDefinition(new SqlIdentifier("d"), SqlType.Text, Default: "'a'")], [new DomainDefinition(new SqlIdentifier("d"), SqlType.Text, Default: "'b'")]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.Default.ShouldBe(new ValueChange<string>("'a'", "'b'"));
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_NotNullChange_IsInPlace()
    {
        var diff = DiffDomains([new DomainDefinition(new SqlIdentifier("d"), SqlType.Text)], [new DomainDefinition(new SqlIdentifier("d"), SqlType.Text, NotNull: true)]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.NotNull.ShouldBe(new ValueChange<bool>(false, true));
    }

    [Fact]
    public void Compare_CheckAddedAndRemoved_AreInPlace()
    {
        var diff = DiffDomains(
            [new DomainDefinition(new SqlIdentifier("d"), SqlType.Text, Checks: [new CheckConstraint(new SqlIdentifier("old_chk"), "VALUE <> ''")])],
            [new DomainDefinition(new SqlIdentifier("d"), SqlType.Text, Checks: [new CheckConstraint(new SqlIdentifier("new_chk"), "length(VALUE) > 0")])]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.Checks.Select(c => (c.Kind, c.Name.Value)).ShouldBe(
            [(ChangeKind.Remove, "old_chk"), (ChangeKind.Add, "new_chk")], ignoreOrder: true);
    }

    [Fact]
    public void Compare_RenamedDomain_SetsRenamedFrom()
    {
        var diff = DiffDomains([new DomainDefinition(new SqlIdentifier("old_d"), SqlType.Text)], [new DomainDefinition(new SqlIdentifier("d"), SqlType.Text, OldName: new SqlIdentifier("old_d"))]);

        diff!.RenamedFrom.ShouldBe("old_d");
        diff.RequiresRecreate.ShouldBeFalse(); // a rename is in place, not a recreate
    }

    [Fact]
    public void Compare_CommentOnlyChange_IsModify()
    {
        var diff = DiffDomains([new DomainDefinition(new SqlIdentifier("d"), SqlType.Text, Comment: "old")], [new DomainDefinition(new SqlIdentifier("d"), SqlType.Text, Comment: "new")]);

        diff!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_PartialSchema_LeavesUnmanagedDomainAlone()
        => _sut.Compare(
            Db(new SchemaDefinition(new SqlIdentifier("app"), Domains: [new DomainDefinition(new SqlIdentifier("d"), SqlType.Text)])),
            Db(new SchemaDefinition(new SqlIdentifier("app"), IsPartial: true)))
            .Schemas.ShouldBeEmpty();

    [Fact]
    public void Compare_PartialSchema_DropsExplicitlyDroppedDomain()
        => _sut.Compare(
            Db(new SchemaDefinition(new SqlIdentifier("app"), Domains: [new DomainDefinition(new SqlIdentifier("d"), SqlType.Text)])),
            Db(new SchemaDefinition(new SqlIdentifier("app"), IsPartial: true, DroppedDomains: [new SqlIdentifier("d")])))
            .Schemas.ShouldHaveSingleItem().Domains.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
}
