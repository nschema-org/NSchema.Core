using NSchema.Diff.Model;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Tables;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Table grants
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_TableGrantRevoked_EmitsRemove()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)], grants: [new TableGrant(new SqlIdentifier("reader"), TablePrivilege.Select)]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]));

        var grant = table!.Grants.ShouldHaveSingleItem();
        grant.Kind.ShouldBe(ChangeKind.Remove);
        grant.Role.ShouldBe("reader");
    }

    [Fact]
    public void Compare_TableGrantPrivilegesChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)], grants: [new TableGrant(new SqlIdentifier("reader"), TablePrivilege.Select)]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)], grants: [new TableGrant(new SqlIdentifier("reader"), TablePrivilege.All)]));

        table!.Grants.Select(g => (g.Kind, g.Privileges)).ShouldBe(
            [(ChangeKind.Remove, TablePrivilege.Select), (ChangeKind.Add, TablePrivilege.All)]);
    }
}
