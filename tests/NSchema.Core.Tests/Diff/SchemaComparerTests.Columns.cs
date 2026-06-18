using NSchema.Diff.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Tables;

namespace NSchema.Tests.Diff;

public partial class SchemaComparerTests
{
    // -------------------------------------------------------------------------
    // Column-level changes
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_ColumnInCurrentButNotDesired_IsRemoved()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("id", SqlType.Int), new Column("email", SqlType.Text)]),
            new Table("users", Columns: [new Column("id", SqlType.Int)]));

        var column = table!.Columns.ShouldHaveSingleItem();
        column.Name.ShouldBe("email");
        column.Kind.ShouldBe(ChangeKind.Remove);
        column.Definition.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_ColumnRename_SetsRenamedFrom()
    {
        var column = DiffColumn(new Column("mail", SqlType.Text), new Column("email", SqlType.Text, OldName: "mail"));

        column!.RenamedFrom.ShouldBe("mail");
        column.Kind.ShouldBe(ChangeKind.Modify);
    }

    [Fact]
    public void Compare_ColumnTypeChange_IsReportedInIsolation()
    {
        var column = DiffColumn(new Column("total", SqlType.Int), new Column("total", SqlType.BigInt));

        column!.Type.ShouldBe(new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt));
        column.Nullability.ShouldBeNull();
        column.Default.ShouldBeNull();
    }

    [Fact]
    public void Compare_ColumnDefaultChange_IsReported()
    {
        var column = DiffColumn(new Column("status", SqlType.Text), new Column("status", SqlType.Text, DefaultExpression: "'new'"));

        column!.Default.ShouldBe(new ValueChange<string>(null, "'new'"));
    }

    [Fact]
    public void Compare_IdentityOptionsChange_IsReported_WhenBothColumnsAreIdentity()
    {
        var current = new Column("id", SqlType.Int, IsIdentity: true, IdentityOptions: new IdentityOptions(1, 1, 1));
        var desired = new Column("id", SqlType.Int, IsIdentity: true, IdentityOptions: new IdentityOptions(100, 1, 1));

        var column = DiffColumn(current, desired);

        column!.Identity.ShouldBe(new ValueChange<IdentityOptions>(new IdentityOptions(1, 1, 1), new IdentityOptions(100, 1, 1)));
    }

    [Fact]
    public void Compare_IdentityEnabled_ReportsChangeFromNullToDesiredOptions()
    {
        var current = new Column("id", SqlType.Int);
        var desired = new Column("id", SqlType.Int, IsIdentity: true, IdentityOptions: new IdentityOptions(1, 1, 1));

        var column = DiffColumn(current, desired);

        column!.Identity.ShouldBe(new ValueChange<IdentityOptions>(null, new IdentityOptions(1, 1, 1)));
    }

    [Fact]
    public void Compare_IdentityDisabled_ReportsChangeFromCurrentOptionsToNull()
    {
        var current = new Column("id", SqlType.Int, IsIdentity: true, IdentityOptions: new IdentityOptions(1, 1, 1));
        var desired = new Column("id", SqlType.Int);

        var column = DiffColumn(current, desired);

        column!.Identity.ShouldBe(new ValueChange<IdentityOptions>(new IdentityOptions(1, 1, 1), null));
    }

    [Fact]
    public void Compare_UnchangedColumn_ProducesNoDiff()
        => DiffColumn(new Column("id", SqlType.Int), new Column("id", SqlType.Int)).ShouldBeNull();

    [Fact]
    public void Compare_GenerationExpressionAdded_IsReported()
    {
        var column = DiffColumn(
            new Column("area", SqlType.Int),
            new Column("area", SqlType.Int, GeneratedExpression: "w * h"));

        column!.Generated.ShouldBe(new ValueChange<string>(null, "w * h"));
    }

    [Fact]
    public void Compare_GenerationExpressionChanged_IsReported()
    {
        var column = DiffColumn(
            new Column("area", SqlType.Int, GeneratedExpression: "w * h"),
            new Column("area", SqlType.Int, GeneratedExpression: "w * h * 2"));

        column!.Generated.ShouldBe(new ValueChange<string>("w * h", "w * h * 2"));
    }

    [Fact]
    public void Compare_UnchangedGeneratedColumn_ProducesNoDiff()
        => DiffColumn(
            new Column("area", SqlType.Int, GeneratedExpression: "w * h"),
            new Column("area", SqlType.Int, GeneratedExpression: "w * h")).ShouldBeNull();
}
