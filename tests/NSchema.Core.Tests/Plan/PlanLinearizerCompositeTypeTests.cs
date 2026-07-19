using NSchema.Diff.Model;
using NSchema.Diff.Model.CompositeTypes;
using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Tables;
using NSchema.Plan.Model;
using NSchema.Plan.Model.CompositeTypes;
using NSchema.Plan.Model.Services;
using NSchema.Plan.Model.Tables;

namespace NSchema.Tests.Plan;

/// <summary>
/// Pins how the linearizer turns composite-type diffs into actions: every change applies in place (no recreate),
/// fields add/drop/retype independently, and composite types are ordered before tables / dropped after.
/// </summary>
public sealed class PlanLinearizerCompositeTypeTests
{
    private readonly PlanLinearizer _linearizer = new();

    private IReadOnlyList<MigrationAction> Linearize(CompositeTypeDiff type) =>
        _linearizer.Linearize(new DatabaseDiff([new SchemaDiff("app", CompositeTypes: [type])]));

    [Fact]
    public void AddedCompositeType_EmitsCreateCompositeType()
        => Linearize(new CompositeTypeDiff("app", "address", ChangeKind.Add,
                Definition: new CompositeType { Name = "address", Fields = [new CompositeField("street", SqlType.Text)] }))
            .ShouldHaveSingleItem().ShouldBeOfType<CreateCompositeType>().CompositeType.Name.ShouldBe("address");

    [Fact]
    public void RemovedCompositeType_EmitsDropCompositeType()
        => Linearize(new CompositeTypeDiff("app", "address", ChangeKind.Remove))
            .ShouldHaveSingleItem().ShouldBeOfType<DropCompositeType>().TypeName.ShouldBe("address");

    [Fact]
    public void FieldChanges_EmitInPlaceFieldActions()
    {
        var plan = Linearize(new CompositeTypeDiff("app", "address", ChangeKind.Modify, Fields:
        [
            new CompositeFieldDiff(ChangeKind.Add, "zip", new CompositeField("zip", SqlType.Int)),
            new CompositeFieldDiff(ChangeKind.Remove, "old"),
            new CompositeFieldDiff(ChangeKind.Modify, "street", Type: new ValueChange<SqlType>(SqlType.Text, SqlType.VarChar(255))),
        ]));

        plan.OfType<AddCompositeField>().ShouldHaveSingleItem().Field.Name.ShouldBe("zip");
        plan.OfType<DropCompositeField>().ShouldHaveSingleItem().FieldName.ShouldBe("old");
        plan.OfType<AlterCompositeFieldType>().ShouldHaveSingleItem().NewType.ShouldBe(SqlType.VarChar(255));
    }

    [Fact]
    public void RenamedCompositeType_EmitsRenameCompositeType()
        => Linearize(new CompositeTypeDiff("app", "address", ChangeKind.Modify, RenamedFrom: "legacy_address"))
            .OfType<RenameCompositeType>().ShouldHaveSingleItem().NewName.ShouldBe("address");

    [Fact]
    public void CommentChange_EmitsSetCompositeTypeComment()
        => Linearize(new CompositeTypeDiff("app", "address", ChangeKind.Modify, Comment: new ValueChange<string>("old", "new")))
            .OfType<SetCompositeTypeComment>().ShouldHaveSingleItem().NewComment.ShouldBe("new");

    [Fact]
    public void CompositeTypeCreate_IsOrderedBeforeCreateTable()
    {
        // A column may use the composite type as its type, so the type must be created first.
        var plan = _linearizer.Linearize(new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add,
            Tables: [new TableDiff("app", "t", ChangeKind.Add, Definition: new Table { Name = "t" })],
            CompositeTypes: [new CompositeTypeDiff("app", "address", ChangeKind.Add,
                Definition: new CompositeType { Name = "address", Fields = [new CompositeField("street", SqlType.Text)] })])]));

        var createType = plan.Select((a, i) => (a, i)).Single(x => x.a is CreateCompositeType).i;
        var createTable = plan.Select((a, i) => (a, i)).Single(x => x.a is CreateTable).i;
        createType.ShouldBeLessThan(createTable);
    }
}
