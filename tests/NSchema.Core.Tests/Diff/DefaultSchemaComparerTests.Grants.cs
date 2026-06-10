using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Tests.Diff;

public partial class DefaultSchemaComparerTests
{
    // -------------------------------------------------------------------------
    // Table grants
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_TableGrantRevoked_EmitsRemove()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("id", SqlType.Int)], Grants: [new TableGrant("reader", TablePrivilege.Select)]),
            new Table("users", Columns: [new Column("id", SqlType.Int)]));

        var grant = table!.Grants.ShouldHaveSingleItem();
        grant.Kind.ShouldBe(ChangeKind.Remove);
        grant.Role.ShouldBe("reader");
    }

    [Fact]
    public void Compare_TableGrantPrivilegesChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("id", SqlType.Int)], Grants: [new TableGrant("reader", TablePrivilege.Select)]),
            new Table("users", Columns: [new Column("id", SqlType.Int)], Grants: [new TableGrant("reader", TablePrivilege.All)]));

        table!.Grants.Select(g => (g.Kind, g.Privileges)).ShouldBe(
            [(ChangeKind.Remove, TablePrivilege.Select), (ChangeKind.Add, TablePrivilege.All)]);
    }
}
