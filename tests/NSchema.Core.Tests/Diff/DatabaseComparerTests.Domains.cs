using NSchema.Diff.Model;
using NSchema.Diff.Model.Domains;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Domains;
using NSchema.Model.Schemas;
using NSchema.Project.Model.Directives;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Domains
    // -------------------------------------------------------------------------

    /// <summary>Diffs two <c>app</c> schemas holding the given domains, returning the single domain diff (null when unchanged).</summary>
    private DomainDiff? DiffDomains(IReadOnlyList<DomainType> current, IReadOnlyList<DomainType> desired, ProjectDirectives? directives = null) =>
        Compare(Db(new Schema(new SqlIdentifier("app"), domains: [.. current])), Db(new Schema(new SqlIdentifier("app"), domains: [.. desired])), directives)
        .Schemas.SingleOrDefault()?.Domains.SingleOrDefault();

    [Fact]
    public void Compare_NewDomain_IsAddCarryingDefinition()
    {
        var diff = DiffDomains([], [new DomainType(new SqlIdentifier("typeid"), SqlType.Text, @default: new SqlText("''"), notNull: true)]);

        diff!.Kind.ShouldBe(ChangeKind.Add);
        diff.Definition!.DataType.ShouldBe(SqlType.Text);
        diff.Definition.NotNull.ShouldBeTrue();
    }

    [Fact]
    public void Compare_RemovedDomain_IsRemove()
        => DiffDomains([new DomainType(new SqlIdentifier("typeid"), SqlType.Text)], [])!.Kind.ShouldBe(ChangeKind.Remove);

    [Fact]
    public void Compare_UnchangedDomain_ProducesNoDiff()
        => DiffDomains([new DomainType(new SqlIdentifier("typeid"), SqlType.Text)], [new DomainType(new SqlIdentifier("typeid"), SqlType.Text)]).ShouldBeNull();

    [Fact]
    public void Compare_BaseTypeChange_RequiresRecreate()
    {
        // Postgres has no ALTER DOMAIN … TYPE, so a base-type change drops + recreates.
        var diff = DiffDomains([new DomainType(new SqlIdentifier("d"), SqlType.Text)], [new DomainType(new SqlIdentifier("d"), SqlType.VarChar(255))]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.DataType.ShouldBe(new ValueChange<SqlType>(SqlType.Text, SqlType.VarChar(255)));
        diff.RequiresRecreate.ShouldBeTrue();
        diff.Definition.ShouldNotBeNull(); // the desired domain rides along for the recreate
    }

    [Fact]
    public void Compare_DefaultChange_IsInPlace()
    {
        var diff = DiffDomains([new DomainType(new SqlIdentifier("d"), SqlType.Text, @default: new SqlText("'a'"))], [new DomainType(new SqlIdentifier("d"), SqlType.Text, @default: new SqlText("'b'"))]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.Default.ShouldBe(new ValueChange<SqlText>(new SqlText("'a'"), new SqlText("'b'")));
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_NotNullChange_IsInPlace()
    {
        var diff = DiffDomains([new DomainType(new SqlIdentifier("d"), SqlType.Text)], [new DomainType(new SqlIdentifier("d"), SqlType.Text, notNull: true)]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.NotNull.ShouldBe(new ValueChange<bool>(false, true));
    }

    [Fact]
    public void Compare_CheckAddedAndRemoved_AreInPlace()
    {
        var diff = DiffDomains(
            [new DomainType(new SqlIdentifier("d"), SqlType.Text, checks: [new CheckConstraint(new SqlIdentifier("old_chk"), new SqlText("VALUE <> ''"))])],
            [new DomainType(new SqlIdentifier("d"), SqlType.Text, checks: [new CheckConstraint(new SqlIdentifier("new_chk"), new SqlText("length(VALUE) > 0"))])]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.Checks.Select(c => (c.Kind, c.Name.Value)).ShouldBe(
            [(ChangeKind.Remove, "old_chk"), (ChangeKind.Add, "new_chk")], ignoreOrder: true);
    }

    [Fact]
    public void Compare_RenamedDomain_SetsRenamedFrom()
    {
        var diff = DiffDomains([new DomainType(new SqlIdentifier("old_d"), SqlType.Text)], [new DomainType(new SqlIdentifier("d"), SqlType.Text)],
            new ProjectDirectives(ObjectRenames: [new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Domain, App("old_d")), new SqlIdentifier("d"))]));

        diff!.RenamedFrom.ShouldBe("old_d");
        diff.RequiresRecreate.ShouldBeFalse(); // a rename is in place, not a recreate
    }

    [Fact]
    public void Compare_CommentOnlyChange_IsModify()
    {
        var diff = DiffDomains([new DomainType(new SqlIdentifier("d"), SqlType.Text) { Comment = "old" }], [new DomainType(new SqlIdentifier("d"), SqlType.Text) { Comment = "new" }]);

        diff!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Definition.ShouldBeNull();
    }
}
