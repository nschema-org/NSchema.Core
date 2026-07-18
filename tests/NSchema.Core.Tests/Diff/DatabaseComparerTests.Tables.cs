using NSchema.Diff.Model;
using NSchema.Diff.Model.Constraints;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Indexes;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Table removal: partial vs full schemas and explicit drops
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_FullSchema_DropsCurrentTableNotInDesired()
    {
        var current = Db(new Schema { Name = new SqlIdentifier("app"), Tables = [new Table { Name = new SqlIdentifier("stale"), Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] }] });
        var desired = Db(new Schema { Name = new SqlIdentifier("app"), Tables = [new Table { Name = new SqlIdentifier("fresh"), Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] }] });

        var tables = Compare(current, desired).Schemas.Single().Tables;

        tables.Single(t => t.Name.Value.Equals("stale")).Kind.ShouldBe(ChangeKind.Remove);
    }

    // -------------------------------------------------------------------------
    // Table-level changes
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_TableRename_SetsRenamedFrom()
    {
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("people"), Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] },
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] },
            TableRename("people", "users"));

        table.ShouldNotBeNull();
        table.Kind.ShouldBe(ChangeKind.Modify);
        table.RenamedFrom.ShouldBe("people");
    }

    [Fact]
    public void Compare_AmbiguousRename_FallsBackToAddAndKeep()
    {
        // 'people' is renamed to 'users' while 'people' is also still declared. The aligner skips the rename
        // (raising an error diagnostic — see DatabaseAlignerTests), so the compare sees a plain add.
        var current = Db(new Schema
        {
            Name = new SqlIdentifier("app"),
            Tables = [
            new Table { Name = new SqlIdentifier("people"), Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] },
        ],
        });
        var desired = Db(new Schema
        {
            Name = new SqlIdentifier("app"),
            Tables = [
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] },
            new Table { Name = new SqlIdentifier("people"), Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] },
        ],
        });

        var table = Compare(current, desired, TableRename("people", "users")).Schemas.Single().Tables.ShouldHaveSingleItem();

        table.Name.ShouldBe("users");
        table.Kind.ShouldBe(ChangeKind.Add);
        table.RenamedFrom.ShouldBeNull();
    }

    [Fact]
    public void Compare_PlainRename_IsNotTreatedAsAmbiguous()
    {
        // A rename whose old name is gone and whose new name is free is unambiguous and must still work.
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("people"), Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] },
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] },
            TableRename("people", "users"));

        table.ShouldNotBeNull();
        table.Kind.ShouldBe(ChangeKind.Modify);
        table.RenamedFrom.ShouldBe("people");
    }

    [Fact]
    public void Compare_TableCommentChange_FoldsCommentValueChange()
    {
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }], Comment = "old" },
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }], Comment = "new" });

        table!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_NewTable_FoldsForeignKeysIndexesGrantsAndIndexComment()
    {
        var desired = new Table
        {
            Name = new SqlIdentifier("orders"),
            Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }, new Column { Name = new SqlIdentifier("user_id"), Type = SqlType.Int }],
            ForeignKeys = [new ForeignKey { Name = new SqlIdentifier("orders_user_fk"), ColumnNames = [new SqlIdentifier("user_id")], ReferencedSchema = new SqlIdentifier("app"), ReferencedTable = new SqlIdentifier("users"), ReferencedColumnNames = [new SqlIdentifier("id")] }],
            Indexes = [new TableIndex { Name = new SqlIdentifier("orders_user_ix"), Columns = ["user_id"], Comment = "lookup" }],
            Grants = [new TableGrant(new SqlIdentifier("reader"), TablePrivilege.Select)],
        };

        var table = Compare(Db(new Schema { Name = new SqlIdentifier("app") }),
            Db(new Schema { Name = new SqlIdentifier("app"), Tables = [desired] })).Schemas.Single().Tables.Single();

        table.ForeignKeys.ShouldHaveSingleItem().ShouldBe(new ForeignKeyDiff(ChangeKind.Add, new SqlIdentifier("orders_user_fk"),
            new ForeignKey { Name = new SqlIdentifier("orders_user_fk"), ColumnNames = [new SqlIdentifier("user_id")], ReferencedSchema = new SqlIdentifier("app"), ReferencedTable = new SqlIdentifier("users"), ReferencedColumnNames = [new SqlIdentifier("id")] }));
        table.Grants.ShouldHaveSingleItem().Privileges.ShouldBe(TablePrivilege.Select);
        // A new index carries both its definition and a folded comment change.
        table.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Add, ChangeKind.Modify]);
        table.Indexes[1].Comment.ShouldBe(new ValueChange<string>(null, "lookup"));
    }
}
