using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Constraints;
using NSchema.Diff.Domain.Models.Domains;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Models;
using NSchema.Plan.Domain.Models.Domains;
using NSchema.Plan.Domain.Models.Tables;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Domains;
using NSchema.Project.Domain.Models.Tables;

namespace NSchema.Tests.Plan;

/// <summary>
/// Pins how the linearizer turns domain diffs into actions: a base-type change recreates (drop + create), every
/// other facet (default, not-null, checks) alters in place, and domains are ordered before tables / dropped after.
/// </summary>
public sealed class PlanLinearizerDomainTests
{
    private readonly PlanLinearizer _linearizer = new();

    private IReadOnlyList<MigrationAction> Linearize(DomainDiff domain) =>
        _linearizer.Linearize(new DatabaseDiff([new SchemaDiff(new SqlIdentifier("app"), Domains: [domain])]));

    [Fact]
    public void AddedDomain_EmitsCreateDomain()
        => Linearize(new DomainDiff(new SqlIdentifier("app"), new SqlIdentifier("d"), ChangeKind.Add, Definition: new DomainType(new SqlIdentifier("d"), SqlType.Text)))
            .ShouldHaveSingleItem().ShouldBeOfType<CreateDomain>().DomainType.Name.ShouldBe("d");

    [Fact]
    public void BaseTypeChange_EmitsRecreateDomain()
        => Linearize(new DomainDiff(new SqlIdentifier("app"), new SqlIdentifier("d"), ChangeKind.Modify, Definition: new DomainType(new SqlIdentifier("d"), SqlType.Int),
                DataType: new ValueChange<SqlType>(SqlType.Text, SqlType.Int)))
            .ShouldHaveSingleItem().ShouldBeOfType<RecreateDomain>();

    [Fact]
    public void DefaultAndNotNullChange_EmitInPlaceAlters()
    {
        var plan = Linearize(new DomainDiff(new SqlIdentifier("app"), new SqlIdentifier("d"), ChangeKind.Modify,
            Default: new ValueChange<SqlText>(null, new SqlText("0")),
            NotNull: new ValueChange<bool>(false, true)));

        plan.OfType<RecreateDomain>().ShouldBeEmpty();
        plan.OfType<AlterDomainDefault>().ShouldHaveSingleItem().NewDefault.ShouldBe("0");
        plan.OfType<AlterDomainNotNull>().ShouldHaveSingleItem().NotNull.ShouldBeTrue();
    }

    [Fact]
    public void CheckChanges_EmitAddAndDropDomainCheck()
    {
        var plan = Linearize(new DomainDiff(new SqlIdentifier("app"), new SqlIdentifier("d"), ChangeKind.Modify, Checks:
        [
            new CheckConstraintDiff(ChangeKind.Add, new SqlIdentifier("new_chk"), new NSchema.Project.Domain.Models.Constraints.CheckConstraint(new SqlIdentifier("new_chk"), new SqlText("VALUE > 0"))),
            new CheckConstraintDiff(ChangeKind.Remove, new SqlIdentifier("old_chk")),
        ]));

        plan.OfType<AddDomainCheck>().ShouldHaveSingleItem().Check.Name.ShouldBe("new_chk");
        plan.OfType<DropDomainCheck>().ShouldHaveSingleItem().CheckName.ShouldBe("old_chk");
    }

    [Fact]
    public void DomainCreate_IsOrderedBeforeCreateTable()
    {
        // A column may use the domain as its type, so the domain must be created first.
        var plan = _linearizer.Linearize(new DatabaseDiff([new SchemaDiff(new SqlIdentifier("app"), ChangeKind.Add,
            Tables: [new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("t"), ChangeKind.Add, Definition: new Table(new SqlIdentifier("t")))],
            Domains: [new DomainDiff(new SqlIdentifier("app"), new SqlIdentifier("d"), ChangeKind.Add, Definition: new DomainType(new SqlIdentifier("d"), SqlType.Text))])]));

        var createDomain = plan.Select((a, i) => (a, i)).Single(x => x.a is CreateDomain).i;
        var createTable = plan.Select((a, i) => (a, i)).Single(x => x.a is CreateTable).i;
        createDomain.ShouldBeLessThan(createTable);
    }
}
