using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Constraints;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Indexes;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Domain.Models;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Table removal: partial vs full schemas and explicit drops
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_FullSchema_DropsCurrentTableNotInDesired()
    {
        var current = Db(new Schema(new SqlIdentifier("app"), Tables: [new Table(new SqlIdentifier("stale"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])]));
        var desired = Db(new Schema(new SqlIdentifier("app"), Tables: [new Table(new SqlIdentifier("fresh"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])]));

        var tables = Compare(current, desired).Schemas.Single().Tables;

        tables.Single(t => t.Name.Value.Equals("stale")).Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_PartialSchema_LeavesUnlistedCurrentTableUntouched()
    {
        var current = Db(new Schema(new SqlIdentifier("app"), Tables: [new Table(new SqlIdentifier("untracked"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])]));
        var desired = Db(new Schema(new SqlIdentifier("app"),
            Tables: [new Table(new SqlIdentifier("fresh"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])]));

        var tables = Compare(current, desired, PartialApp()).Schemas.Single().Tables;

        tables.Select(t => t.Name).ShouldBe(["fresh"]); // 'untracked' is neither dropped nor reported
    }

    [Fact]
    public void Compare_PartialSchema_StillDropsExplicitlyDroppedTable()
    {
        var current = Db(new Schema(new SqlIdentifier("app"), Tables: [new Table(new SqlIdentifier("retired"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])]));
        var desired = Db(new Schema(new SqlIdentifier("app")));
        var directives = PartialApp() with { Tables = new TableDirectives(Drops: [App("retired")]) };

        var table = Compare(current, desired, directives).Schemas.Single().Tables.ShouldHaveSingleItem();

        table.Name.ShouldBe("retired");
        table.Kind.ShouldBe(ChangeKind.Remove);
    }

    // -------------------------------------------------------------------------
    // Table-level changes
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_TableRename_SetsRenamedFrom()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("people"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            new Table(new SqlIdentifier("users"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
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
        var current = Db(new Schema(new SqlIdentifier("app"), Tables:
        [
            new Table(new SqlIdentifier("people"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
        ]));
        var desired = Db(new Schema(new SqlIdentifier("app"), Tables:
        [
            new Table(new SqlIdentifier("users"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            new Table(new SqlIdentifier("people"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
        ]));

        var ex = Should.Throw<InvalidOperationException>(() => Compare(current, desired, TableRename("people", "users")));
        ex.Message.ShouldContain("app.users");
        ex.Message.ShouldContain("people");
    }

    [Fact]
    public void Compare_TableRenamedOntoExistingName_Throws()
    {
        // Renaming 'a' to 'b' while a distinct 'b' already exists collides on the target name.
        var current = Db(new Schema(new SqlIdentifier("app"), Tables:
        [
            new Table(new SqlIdentifier("a"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            new Table(new SqlIdentifier("b"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
        ]));
        var desired = Db(new Schema(new SqlIdentifier("app"), Tables:
        [
            new Table(new SqlIdentifier("b"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
        ]));

        var ex = Should.Throw<InvalidOperationException>(() => Compare(current, desired, TableRename("a", "b")));
        ex.Message.ShouldContain("app.b");
    }

    [Fact]
    public void Compare_ColumnRenamedButOldNameStillDeclared_Throws()
    {
        var act = () => DiffTable(
            new Table(new SqlIdentifier("t"), Columns: [new Column(new SqlIdentifier("mail"), SqlType.Text)]),
            new Table(new SqlIdentifier("t"), Columns:
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
            new Table(new SqlIdentifier("people"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            new Table(new SqlIdentifier("users"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            TableRename("people", "users"));

        table.ShouldNotBeNull();
        table.Kind.ShouldBe(ChangeKind.Modify);
        table.RenamedFrom.ShouldBe("people");
    }

    [Fact]
    public void Compare_TableCommentChange_FoldsCommentValueChange()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), Comment: "old", Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            new Table(new SqlIdentifier("users"), Comment: "new", Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]));

        table!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_NewTable_FoldsForeignKeysIndexesGrantsAndIndexComment()
    {
        var desired = new Table(new SqlIdentifier("orders"),
            Columns: [new Column(new SqlIdentifier("id"), SqlType.Int), new Column(new SqlIdentifier("user_id"), SqlType.Int)],
            ForeignKeys: [new ForeignKey(new SqlIdentifier("orders_user_fk"), [new SqlIdentifier("user_id")], new SqlIdentifier("app"), new SqlIdentifier("users"), [new SqlIdentifier("id")])],
            Indexes: [new TableIndex(new SqlIdentifier("orders_user_ix"), ["user_id"], Comment: "lookup")],
            Grants: [new TableGrant(new SqlIdentifier("reader"), TablePrivilege.Select)]);

        var table = Compare(Db(new Schema(new SqlIdentifier("app"))),
            Db(new Schema(new SqlIdentifier("app"), Tables: [desired]))).Schemas.Single().Tables.Single();

        table.ForeignKeys.ShouldHaveSingleItem().ShouldBe(new ForeignKeyDiff(ChangeKind.Add, new SqlIdentifier("orders_user_fk"),
            new ForeignKey(new SqlIdentifier("orders_user_fk"), [new SqlIdentifier("user_id")], new SqlIdentifier("app"), new SqlIdentifier("users"), [new SqlIdentifier("id")])));
        table.Grants.ShouldHaveSingleItem().Privileges.ShouldBe(TablePrivilege.Select);
        // A new index carries both its definition and a folded comment change.
        table.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Add, ChangeKind.Modify]);
        table.Indexes[1].Comment.ShouldBe(new ValueChange<string>(null, "lookup"));
    }
}
