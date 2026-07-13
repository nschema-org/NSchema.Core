using NSchema.Project.Domain.Models;
using NSchema.Diff.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Tables;

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
            new Table(new SqlIdentifier("users"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int), new Column(new SqlIdentifier("email"), SqlType.Text)]),
            new Table(new SqlIdentifier("users"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]));

        var column = table!.Columns.ShouldHaveSingleItem();
        column.Name.ShouldBe("email");
        column.Kind.ShouldBe(ChangeKind.Remove);
        column.Definition.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_ColumnRename_SetsRenamedFrom()
    {
        var column = DiffColumn(new Column(new SqlIdentifier("mail"), SqlType.Text), new Column(new SqlIdentifier("email"), SqlType.Text, OldName: new SqlIdentifier("mail")));

        column!.RenamedFrom.ShouldBe("mail");
        column.Kind.ShouldBe(ChangeKind.Modify);
    }

    [Fact]
    public void Compare_ColumnTypeChange_IsReportedInIsolation()
    {
        var column = DiffColumn(new Column(new SqlIdentifier("total"), SqlType.Int), new Column(new SqlIdentifier("total"), SqlType.BigInt));

        column!.Type.ShouldBe(new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt));
        column.Nullability.ShouldBeNull();
        column.Default.ShouldBeNull();
    }

    [Fact]
    public void Compare_ColumnDefaultChange_IsReported()
    {
        var column = DiffColumn(new Column(new SqlIdentifier("status"), SqlType.Text), new Column(new SqlIdentifier("status"), SqlType.Text, DefaultExpression: "'new'"));

        column!.Default.ShouldBe(new ValueChange<string>(null, "'new'"));
    }

    [Fact]
    public void Compare_ModifiedColumn_CarriesDesiredDefinition()
    {
        // The desired column rides along on a modified column's Definition so a dialect whose in-place ALTER COLUMN
        // must restate the whole column (SQL Server) can read the final type and nullability together.
        var column = DiffColumn(
            new Column(new SqlIdentifier("total"), SqlType.Int, IsNullable: false),
            new Column(new SqlIdentifier("total"), SqlType.BigInt, IsNullable: false));

        column!.Definition.ShouldNotBeNull();
        column.Definition!.Type.ShouldBe(SqlType.BigInt);
        column.Definition.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Compare_IdentityOptionsChange_IsReported_WhenBothColumnsAreIdentity()
    {
        var current = new Column(new SqlIdentifier("id"), SqlType.Int, IsIdentity: true, IdentityOptions: new IdentityOptions(1, 1, 1));
        var desired = new Column(new SqlIdentifier("id"), SqlType.Int, IsIdentity: true, IdentityOptions: new IdentityOptions(100, 1, 1));

        var column = DiffColumn(current, desired);

        column!.Identity.ShouldBe(new ValueChange<IdentityOptions>(new IdentityOptions(1, 1, 1), new IdentityOptions(100, 1, 1)));
    }

    [Fact]
    public void Compare_IdentityEnabled_ReportsChangeFromNullToDesiredOptions()
    {
        var current = new Column(new SqlIdentifier("id"), SqlType.Int);
        var desired = new Column(new SqlIdentifier("id"), SqlType.Int, IsIdentity: true, IdentityOptions: new IdentityOptions(1, 1, 1));

        var column = DiffColumn(current, desired);

        column!.Identity.ShouldBe(new ValueChange<IdentityOptions>(null, new IdentityOptions(1, 1, 1)));
    }

    [Fact]
    public void Compare_IdentityDisabled_ReportsChangeFromCurrentOptionsToNull()
    {
        var current = new Column(new SqlIdentifier("id"), SqlType.Int, IsIdentity: true, IdentityOptions: new IdentityOptions(1, 1, 1));
        var desired = new Column(new SqlIdentifier("id"), SqlType.Int);

        var column = DiffColumn(current, desired);

        column!.Identity.ShouldBe(new ValueChange<IdentityOptions>(new IdentityOptions(1, 1, 1), null));
    }

    [Fact]
    public void Compare_UnchangedColumn_ProducesNoDiff()
        => DiffColumn(new Column(new SqlIdentifier("id"), SqlType.Int), new Column(new SqlIdentifier("id"), SqlType.Int)).ShouldBeNull();

    [Fact]
    public void Compare_GenerationExpressionAdded_IsReported()
    {
        var column = DiffColumn(
            new Column(new SqlIdentifier("area"), SqlType.Int),
            new Column(new SqlIdentifier("area"), SqlType.Int, GeneratedExpression: "w * h"));

        column!.Generated.ShouldBe(new ValueChange<string>(null, "w * h"));
    }

    [Fact]
    public void Compare_GenerationExpressionChanged_IsReported()
    {
        var column = DiffColumn(
            new Column(new SqlIdentifier("area"), SqlType.Int, GeneratedExpression: "w * h"),
            new Column(new SqlIdentifier("area"), SqlType.Int, GeneratedExpression: "w * h * 2"));

        column!.Generated.ShouldBe(new ValueChange<string>("w * h", "w * h * 2"));
    }

    [Fact]
    public void Compare_UnchangedGeneratedColumn_ProducesNoDiff()
        => DiffColumn(
            new Column(new SqlIdentifier("area"), SqlType.Int, GeneratedExpression: "w * h"),
            new Column(new SqlIdentifier("area"), SqlType.Int, GeneratedExpression: "w * h")).ShouldBeNull();
}
