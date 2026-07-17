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
        var current = Db(new Schema(new SqlIdentifier("app"), tables: [new Table(new SqlIdentifier("stale"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])]));
        var desired = Db(new Schema(new SqlIdentifier("app"), tables: [new Table(new SqlIdentifier("fresh"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])]));

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
            new Table(new SqlIdentifier("people"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            TableRename("people", "users"));

        table.ShouldNotBeNull();
        table.Kind.ShouldBe(ChangeKind.Modify);
        table.RenamedFrom.ShouldBe("people");
    }

    [Fact]
    public void Compare_TableRenamedButOldNameStillDeclared_Throws()
    {
        // 'people' is renamed to 'users' while 'people' is also still declared. This is indistinguishable from
        // "keep people, add users" and cannot be ordered safely, so it must be rejected rather than guessed.
        var current = Db(new Schema(new SqlIdentifier("app"), tables:
        [
            new Table(new SqlIdentifier("people"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
        ]));
        var desired = Db(new Schema(new SqlIdentifier("app"), tables:
        [
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            new Table(new SqlIdentifier("people"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
        ]));

        var ex = Should.Throw<InvalidOperationException>(() => Compare(current, desired, TableRename("people", "users")));
        ex.Message.ShouldContain("app.users");
        ex.Message.ShouldContain("people");
    }

    [Fact]
    public void Compare_TableRenamedOntoExistingName_Throws()
    {
        // Renaming 'a' to 'b' while a distinct 'b' already exists collides on the target name.
        var current = Db(new Schema(new SqlIdentifier("app"), tables:
        [
            new Table(new SqlIdentifier("a"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            new Table(new SqlIdentifier("b"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
        ]));
        var desired = Db(new Schema(new SqlIdentifier("app"), tables:
        [
            new Table(new SqlIdentifier("b"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
        ]));

        var ex = Should.Throw<InvalidOperationException>(() => Compare(current, desired, TableRename("a", "b")));
        ex.Message.ShouldContain("app.b");
    }

    [Fact]
    public void Compare_ColumnRenamedButOldNameStillDeclared_Throws()
    {
        var act = () => DiffTable(
            new Table(new SqlIdentifier("t"), columns: [new Column(new SqlIdentifier("mail"), SqlType.Text)]),
            new Table(new SqlIdentifier("t"), columns:
            [
                new Column(new SqlIdentifier("email"), SqlType.Text),
                new Column(new SqlIdentifier("mail"), SqlType.Text),
            ]),
            ColumnRename("mail", "email"));

        var ex = act.ShouldThrow<InvalidOperationException>();
        ex.Message.ShouldContain("app.t.email");
    }

    [Fact]
    public void Compare_PlainRename_IsNotTreatedAsAmbiguous()
    {
        // A rename whose old name is gone and whose new name is free is unambiguous and must still work.
        var table = DiffTable(
            new Table(new SqlIdentifier("people"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            TableRename("people", "users"));

        table.ShouldNotBeNull();
        table.Kind.ShouldBe(ChangeKind.Modify);
        table.RenamedFrom.ShouldBe("people");
    }

    [Fact]
    public void Compare_TableCommentChange_FoldsCommentValueChange()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]) { Comment = "old" },
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]) { Comment = "new" });

        table!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_NewTable_FoldsForeignKeysIndexesGrantsAndIndexComment()
    {
        var desired = new Table(new SqlIdentifier("orders"),
            columns: [new Column(new SqlIdentifier("id"), SqlType.Int), new Column(new SqlIdentifier("user_id"), SqlType.Int)],
            foreignKeys: [new ForeignKey(new SqlIdentifier("orders_user_fk"), [new SqlIdentifier("user_id")], new SqlIdentifier("app"), new SqlIdentifier("users"), [new SqlIdentifier("id")])],
            indexes: [new TableIndex(new SqlIdentifier("orders_user_ix"), ["user_id"]) { Comment = "lookup" }],
            grants: [new TableGrant(new SqlIdentifier("reader"), TablePrivilege.Select)]);

        var table = Compare(Db(new Schema(new SqlIdentifier("app"))),
            Db(new Schema(new SqlIdentifier("app"), tables: [desired]))).Schemas.Single().Tables.Single();

        table.ForeignKeys.ShouldHaveSingleItem().ShouldBe(new ForeignKeyDiff(ChangeKind.Add, new SqlIdentifier("orders_user_fk"),
            new ForeignKey(new SqlIdentifier("orders_user_fk"), [new SqlIdentifier("user_id")], new SqlIdentifier("app"), new SqlIdentifier("users"), [new SqlIdentifier("id")])));
        table.Grants.ShouldHaveSingleItem().Privileges.ShouldBe(TablePrivilege.Select);
        // A new index carries both its definition and a folded comment change.
        table.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Add, ChangeKind.Modify]);
        table.Indexes[1].Comment.ShouldBe(new ValueChange<string>(null, "lookup"));
    }
}
