using NSchema.Diff.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Constraints;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Tables;

namespace NSchema.Tests.Diff;

public partial class SchemaComparerTests
{
    // -------------------------------------------------------------------------
    // Primary keys
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_PrimaryKeyAdded_EmitsAddConstraint()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("id", SqlType.Int)]),
            new Table("users", PrimaryKey: new PrimaryKey("users_pkey", ["id"]), Columns: [new Column("id", SqlType.Int)]));

        table!.PrimaryKey.ShouldHaveSingleItem().ShouldBe(
            new PrimaryKeyDiff(ChangeKind.Add, "users_pkey", new PrimaryKey("users_pkey", ["id"])));
    }

    [Fact]
    public void Compare_PrimaryKeyDropped_EmitsRemoveConstraint()
    {
        var table = DiffTable(
            new Table("users", PrimaryKey: new PrimaryKey("users_pkey", ["id"]), Columns: [new Column("id", SqlType.Int)]),
            new Table("users", Columns: [new Column("id", SqlType.Int)]));

        table!.PrimaryKey.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_PrimaryKeyChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table("users", PrimaryKey: new PrimaryKey("users_pkey", ["id"]), Columns: [new Column("id", SqlType.Int)]),
            new Table("users", PrimaryKey: new PrimaryKey("users_pkey", ["id", "tenant"]), Columns: [new Column("id", SqlType.Int)]));

        table!.PrimaryKey.Select(c => c.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_PrimaryKeyCommentOnlyChanged_EmitsModifyNotRecreate()
    {
        var table = DiffTable(
            new Table("users", PrimaryKey: new PrimaryKey("users_pkey", ["id"], Comment: "old"), Columns: [new Column("id", SqlType.Int)]),
            new Table("users", PrimaryKey: new PrimaryKey("users_pkey", ["id"], Comment: "new"), Columns: [new Column("id", SqlType.Int)]));

        var pk = table!.PrimaryKey.ShouldHaveSingleItem();
        pk.Kind.ShouldBe(ChangeKind.Modify);
        pk.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    // -------------------------------------------------------------------------
    // Foreign keys
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_ForeignKeyRemoved_EmitsRemoveConstraint()
    {
        var fk = new ForeignKey("orders_user_fk", ["user_id"], "app", "users", ["id"]);
        var table = DiffTable(
            new Table("orders", Columns: [new Column("user_id", SqlType.Int)], ForeignKeys: [fk]),
            new Table("orders", Columns: [new Column("user_id", SqlType.Int)]));

        table!.ForeignKeys.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_ForeignKeyDefinitionChanged_EmitsRemoveAndAdd()
    {
        var table = DiffTable(
            new Table("orders", Columns: [new Column("user_id", SqlType.Int)],
                ForeignKeys: [new ForeignKey("orders_user_fk", ["user_id"], "app", "users", ["id"])]),
            new Table("orders", Columns: [new Column("user_id", SqlType.Int)],
                ForeignKeys: [new ForeignKey("orders_user_fk", ["user_id"], "app", "users", ["id"], OnDelete: ReferentialAction.Cascade)]));

        table!.ForeignKeys.Select(c => c.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    // -------------------------------------------------------------------------
    // Unique constraints
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_UniqueConstraintAdded_EmitsAdd()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("email", SqlType.Text)]),
            new Table("users", Columns: [new Column("email", SqlType.Text)],
                UniqueConstraints: [new UniqueConstraint("users_email_uq", ["email"])]));

        table!.UniqueConstraints.ShouldHaveSingleItem().ShouldBe(
            new UniqueConstraintDiff(ChangeKind.Add, "users_email_uq", new UniqueConstraint("users_email_uq", ["email"])));
    }

    [Fact]
    public void Compare_UniqueConstraintRemoved_EmitsRemove()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("email", SqlType.Text)],
                UniqueConstraints: [new UniqueConstraint("users_email_uq", ["email"])]),
            new Table("users", Columns: [new Column("email", SqlType.Text)]));

        var unique = table!.UniqueConstraints.ShouldHaveSingleItem();
        unique.Kind.ShouldBe(ChangeKind.Remove);
        unique.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_UniqueConstraintColumnsChanged_EmitsRemoveThenAdd()
    {
        var columns = new[] { new Column("email", SqlType.Text), new Column("tenant", SqlType.Int) };
        var table = DiffTable(
            new Table("users", Columns: columns,
                UniqueConstraints: [new UniqueConstraint("users_email_uq", ["email"])]),
            new Table("users", Columns: columns,
                UniqueConstraints: [new UniqueConstraint("users_email_uq", ["email", "tenant"])]));

        table!.UniqueConstraints.Select(c => c.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_UniqueConstraintCommentOnlyChanged_EmitsModifyNotRecreate()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("email", SqlType.Text)],
                UniqueConstraints: [new UniqueConstraint("users_email_uq", ["email"], Comment: "old")]),
            new Table("users", Columns: [new Column("email", SqlType.Text)],
                UniqueConstraints: [new UniqueConstraint("users_email_uq", ["email"], Comment: "new")]));

        var unique = table!.UniqueConstraints.ShouldHaveSingleItem();
        unique.Kind.ShouldBe(ChangeKind.Modify);
        unique.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_NewUniqueConstraintWithComment_FoldsCommentAsModify()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("email", SqlType.Text)]),
            new Table("users", Columns: [new Column("email", SqlType.Text)],
                UniqueConstraints: [new UniqueConstraint("users_email_uq", ["email"], Comment: "lookup")]));

        table!.UniqueConstraints.Select(c => (c.Kind, c.Comment?.New))
            .ShouldBe([(ChangeKind.Add, null), (ChangeKind.Modify, "lookup")]);
    }

    [Fact]
    public void Compare_UniqueConstraintUnchanged_ProducesNoDiff()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("email", SqlType.Text)],
                UniqueConstraints: [new UniqueConstraint("users_email_uq", ["email"])]),
            new Table("users", Columns: [new Column("email", SqlType.Text)],
                UniqueConstraints: [new UniqueConstraint("users_email_uq", ["email"])]));

        table.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Check constraints
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_CheckConstraintAdded_EmitsAdd()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("age", SqlType.Int)]),
            new Table("users", Columns: [new Column("age", SqlType.Int)],
                CheckConstraints: [new CheckConstraint("users_age_chk", "age >= 0")]));

        table!.Checks.ShouldHaveSingleItem().ShouldBe(
            new CheckConstraintDiff(ChangeKind.Add, "users_age_chk", new CheckConstraint("users_age_chk", "age >= 0")));
    }

    [Fact]
    public void Compare_CheckConstraintRemoved_EmitsRemove()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("age", SqlType.Int)],
                CheckConstraints: [new CheckConstraint("users_age_chk", "age >= 0")]),
            new Table("users", Columns: [new Column("age", SqlType.Int)]));

        var check = table!.Checks.ShouldHaveSingleItem();
        check.Kind.ShouldBe(ChangeKind.Remove);
        check.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_CheckConstraintExpressionChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("age", SqlType.Int)],
                CheckConstraints: [new CheckConstraint("users_age_chk", "age >= 0")]),
            new Table("users", Columns: [new Column("age", SqlType.Int)],
                CheckConstraints: [new CheckConstraint("users_age_chk", "age > 0")]));

        table!.Checks.Select(c => c.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_CheckConstraintCommentOnlyChanged_EmitsModifyNotRecreate()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("age", SqlType.Int)],
                CheckConstraints: [new CheckConstraint("users_age_chk", "age >= 0", Comment: "old")]),
            new Table("users", Columns: [new Column("age", SqlType.Int)],
                CheckConstraints: [new CheckConstraint("users_age_chk", "age >= 0", Comment: "new")]));

        var check = table!.Checks.ShouldHaveSingleItem();
        check.Kind.ShouldBe(ChangeKind.Modify);
        check.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_CheckConstraintUnchanged_ProducesNoDiff()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("age", SqlType.Int)],
                CheckConstraints: [new CheckConstraint("users_age_chk", "age >= 0")]),
            new Table("users", Columns: [new Column("age", SqlType.Int)],
                CheckConstraints: [new CheckConstraint("users_age_chk", "age >= 0")]));

        table.ShouldBeNull();
    }

    [Fact]
    public void Compare_NewTable_FoldsUniqueAndCheckConstraintsAsAdds()
    {
        // On a new table the primary key is created inline (carried on Definition), but unique and check
        // constraints arrive as separate adds, mirroring how foreign keys fold.
        var desired = new Table("users",
            Columns: [new Column("email", SqlType.Text), new Column("age", SqlType.Int)],
            UniqueConstraints: [new UniqueConstraint("users_email_uq", ["email"])],
            CheckConstraints: [new CheckConstraint("users_age_chk", "age >= 0")]);

        var table = _sut.Compare(Db(new SchemaDefinition("app")),
            Db(new SchemaDefinition("app", Tables: [desired]))).Schemas.Single().Tables.Single();

        table.Kind.ShouldBe(ChangeKind.Add);
        table.UniqueConstraints.ShouldHaveSingleItem().ShouldBe(
            new UniqueConstraintDiff(ChangeKind.Add, "users_email_uq", new UniqueConstraint("users_email_uq", ["email"])));
        table.Checks.ShouldHaveSingleItem().ShouldBe(
            new CheckConstraintDiff(ChangeKind.Add, "users_age_chk", new CheckConstraint("users_age_chk", "age >= 0")));
    }

    // -------------------------------------------------------------------------
    // Indexes
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_IndexRemoved_EmitsRemove()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("email", SqlType.Text)], Indexes: [new TableIndex("users_email_ix", ["email"])]),
            new Table("users", Columns: [new Column("email", SqlType.Text)]));

        table!.Indexes.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_IndexDefinitionChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("email", SqlType.Text)], Indexes: [new TableIndex("users_email_ix", ["email"])]),
            new Table("users", Columns: [new Column("email", SqlType.Text)], Indexes: [new TableIndex("users_email_ix", ["email"], IsUnique: true)]));

        table!.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_IndexCommentOnlyChange_EmitsModify()
    {
        var table = DiffTable(
            new Table("users", Columns: [new Column("email", SqlType.Text)], Indexes: [new TableIndex("users_email_ix", ["email"], Comment: "old")]),
            new Table("users", Columns: [new Column("email", SqlType.Text)], Indexes: [new TableIndex("users_email_ix", ["email"], Comment: "new")]));

        var index = table!.Indexes.ShouldHaveSingleItem();
        index.Kind.ShouldBe(ChangeKind.Modify);
        index.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_IndexMethodChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table("t", Columns: [new Column("tags", SqlType.Text)], Indexes: [new TableIndex("t_tags_ix", ["tags"])]),
            new Table("t", Columns: [new Column("tags", SqlType.Text)], Indexes: [new TableIndex("t_tags_ix", ["tags"], Method: "gin")]));

        table!.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_IndexIncludeChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table("t", Columns: [new Column("a", SqlType.Int), new Column("b", SqlType.Int)], Indexes: [new TableIndex("t_ix", ["a"])]),
            new Table("t", Columns: [new Column("a", SqlType.Int), new Column("b", SqlType.Int)], Indexes: [new TableIndex("t_ix", ["a"], Include: ["b"])]));

        table!.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_IndexKeyOrderingChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table("t", Columns: [new Column("a", SqlType.Int)], Indexes: [new TableIndex("t_ix", ["a"])]),
            new Table("t", Columns: [new Column("a", SqlType.Int)], Indexes: [new TableIndex("t_ix", [new IndexColumn("a", Sort: IndexSort.Descending)])]));

        table!.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    // -------------------------------------------------------------------------
    // Exclusion constraints
    // -------------------------------------------------------------------------

    private static ExclusionConstraint NoOverlap(string? method = "gist", string? comment = null) =>
        new("no_overlap", [new ExclusionElement("room", "="), new ExclusionElement("during", "&&")], method, Comment: comment);

    private static Table Bookings(params ExclusionConstraint[] exclusions) =>
        new("bookings", Columns: [new Column("room", SqlType.Int), new Column("during", SqlType.Int)], ExclusionConstraints: exclusions);

    [Fact]
    public void Compare_ExclusionAdded_EmitsAdd()
    {
        var table = DiffTable(Bookings(), Bookings(NoOverlap()));

        var exclusion = table!.ExclusionConstraints.ShouldHaveSingleItem();
        exclusion.Kind.ShouldBe(ChangeKind.Add);
        exclusion.Definition!.Method.ShouldBe("gist");
    }

    [Fact]
    public void Compare_ExclusionDropped_EmitsRemove()
        => DiffTable(Bookings(NoOverlap()), Bookings())!
            .ExclusionConstraints.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);

    [Fact]
    public void Compare_ExclusionUnchanged_ProducesNoDiff()
        => DiffTable(Bookings(NoOverlap()), Bookings(NoOverlap())).ShouldBeNull();

    [Fact]
    public void Compare_ExclusionDefinitionChanged_EmitsRemoveThenAdd()
        => DiffTable(Bookings(NoOverlap(method: "gist")), Bookings(NoOverlap(method: "spgist")))!
            .ExclusionConstraints.Select(e => e.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);

    [Fact]
    public void Compare_ExclusionCommentOnlyChange_EmitsModify()
    {
        var exclusion = DiffTable(Bookings(NoOverlap(comment: "old")), Bookings(NoOverlap(comment: "new")))!
            .ExclusionConstraints.ShouldHaveSingleItem();
        exclusion.Kind.ShouldBe(ChangeKind.Modify);
        exclusion.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }
}
