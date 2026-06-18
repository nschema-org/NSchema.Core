using NSchema.Diff.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Tables;

namespace NSchema.Tests.Diff;

public partial class SchemaComparerTests
{
    // -------------------------------------------------------------------------
    // Table removal: partial vs full schemas and explicit drops
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_FullSchema_DropsCurrentTableNotInDesired()
    {
        var current = Db(new SchemaDefinition("app", Tables: [new Table("stale", Columns: [new Column("id", SqlType.Int)])]));
        var desired = Db(new SchemaDefinition("app", Tables: [new Table("fresh", Columns: [new Column("id", SqlType.Int)])]));

        var tables = _sut.Compare(current, desired).Schemas.Single().Tables;

        tables.Single(t => t.Name == "stale").Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_PartialSchema_LeavesUnlistedCurrentTableUntouched()
    {
        var current = Db(new SchemaDefinition("app", Tables: [new Table("untracked", Columns: [new Column("id", SqlType.Int)])]));
        var desired = Db(new SchemaDefinition("app", IsPartial: true,
            Tables: [new Table("fresh", Columns: [new Column("id", SqlType.Int)])]));

        var tables = _sut.Compare(current, desired).Schemas.Single().Tables;

        tables.Select(t => t.Name).ShouldBe(["fresh"]); // 'untracked' is neither dropped nor reported
    }

    [Fact]
    public void Compare_PartialSchema_StillDropsExplicitlyDroppedTable()
    {
        var current = Db(new SchemaDefinition("app", Tables: [new Table("retired", Columns: [new Column("id", SqlType.Int)])]));
        var desired = Db(new SchemaDefinition("app", IsPartial: true, DroppedTables: ["retired"]));

        var table = _sut.Compare(current, desired).Schemas.Single().Tables.ShouldHaveSingleItem();

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
            new Table("people", Columns: [new Column("id", SqlType.Int)]),
            new Table("users", OldName: "people", Columns: [new Column("id", SqlType.Int)]));

        table.ShouldNotBeNull();
        table.Kind.ShouldBe(ChangeKind.Modify);
        table.RenamedFrom.ShouldBe("people");
    }

    [Fact]
    public void Compare_TableRenamedButOldNameStillDeclared_Throws()
    {
        // 'people' is renamed to 'users' while 'people' is also still declared. This is indistinguishable from
        // "keep people, add users" and cannot be ordered safely, so it must be rejected rather than guessed.
        var current = Db(new SchemaDefinition("app", Tables:
        [
            new Table("people", Columns: [new Column("id", SqlType.Int)]),
        ]));
        var desired = Db(new SchemaDefinition("app", Tables:
        [
            new Table("users", OldName: "people", Columns: [new Column("id", SqlType.Int)]),
            new Table("people", Columns: [new Column("id", SqlType.Int)]),
        ]));

        var ex = Should.Throw<InvalidOperationException>(() => _sut.Compare(current, desired));
        ex.Message.ShouldContain("app.users");
        ex.Message.ShouldContain("people");
    }

    [Fact]
    public void Compare_TableRenamedOntoExistingName_Throws()
    {
        // Renaming 'a' to 'b' while a distinct 'b' already exists collides on the target name.
        var current = Db(new SchemaDefinition("app", Tables:
        [
            new Table("a", Columns: [new Column("id", SqlType.Int)]),
            new Table("b", Columns: [new Column("id", SqlType.Int)]),
        ]));
        var desired = Db(new SchemaDefinition("app", Tables:
        [
            new Table("b", OldName: "a", Columns: [new Column("id", SqlType.Int)]),
        ]));

        var ex = Should.Throw<InvalidOperationException>(() => _sut.Compare(current, desired));
        ex.Message.ShouldContain("app.b");
    }

    [Fact]
    public void Compare_ColumnRenamedButOldNameStillDeclared_Throws()
    {
        var act = () => DiffTable(
            new Table("t", Columns: [new Column("mail", SqlType.Text)]),
            new Table("t", Columns:
            [
                new Column("email", SqlType.Text, OldName: "mail"),
                new Column("mail", SqlType.Text),
            ]));

        var ex = act.ShouldThrow<InvalidOperationException>();
        ex.Message.ShouldContain("app.t.email");
    }

    [Fact]
    public void Compare_PlainRename_IsNotTreatedAsAmbiguous()
    {
        // A rename whose old name is gone and whose new name is free is unambiguous and must still work.
        var table = DiffTable(
            new Table("people", Columns: [new Column("id", SqlType.Int)]),
            new Table("users", OldName: "people", Columns: [new Column("id", SqlType.Int)]));

        table.ShouldNotBeNull();
        table.Kind.ShouldBe(ChangeKind.Modify);
        table.RenamedFrom.ShouldBe("people");
    }

    [Fact]
    public void Compare_TableCommentChange_FoldsCommentValueChange()
    {
        var table = DiffTable(
            new Table("users", Comment: "old", Columns: [new Column("id", SqlType.Int)]),
            new Table("users", Comment: "new", Columns: [new Column("id", SqlType.Int)]));

        table!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_NewTable_FoldsForeignKeysIndexesGrantsAndIndexComment()
    {
        var desired = new Table("orders",
            Columns: [new Column("id", SqlType.Int), new Column("user_id", SqlType.Int)],
            ForeignKeys: [new ForeignKey("orders_user_fk", ["user_id"], "app", "users", ["id"])],
            Indexes: [new TableIndex("orders_user_ix", ["user_id"], Comment: "lookup")],
            Grants: [new TableGrant("reader", TablePrivilege.Select)]);

        var table = _sut.Compare(Db(new SchemaDefinition("app")),
            Db(new SchemaDefinition("app", Tables: [desired]))).Schemas.Single().Tables.Single();

        table.ForeignKeys.ShouldHaveSingleItem().ShouldBe(new ForeignKeyDiff(ChangeKind.Add, "orders_user_fk",
            new ForeignKey("orders_user_fk", ["user_id"], "app", "users", ["id"])));
        table.Grants.ShouldHaveSingleItem().Privileges.ShouldBe(TablePrivilege.Select);
        // A new index carries both its definition and a folded comment change.
        table.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Add, ChangeKind.Modify]);
        table.Indexes[1].Comment.ShouldBe(new ValueChange<string>(null, "lookup"));
    }
}
