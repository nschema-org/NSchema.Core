using NSchema.Diff.Model;
using NSchema.Diff.Model.Constraints;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Indexes;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Primary keys
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_PrimaryKeyAdded_EmitsAddConstraint()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            new Table(new SqlIdentifier("users"), primaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id")]), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]));

        table!.PrimaryKey.ShouldHaveSingleItem().ShouldBe(
            new PrimaryKeyDiff(ChangeKind.Add, new SqlIdentifier("users_pkey"), new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id")])));
    }

    [Fact]
    public void Compare_PrimaryKeyDropped_EmitsRemoveConstraint()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), primaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id")]), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]));

        table!.PrimaryKey.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_PrimaryKeyChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), primaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id")]), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            new Table(new SqlIdentifier("users"), primaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id"), new SqlIdentifier("tenant")]), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]));

        table!.PrimaryKey.Select(c => c.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_PrimaryKeyCommentOnlyChanged_EmitsModifyNotRecreate()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), primaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id")]) { Comment = "old" }, columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            new Table(new SqlIdentifier("users"), primaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id")]) { Comment = "new" }, columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]));

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
        var fk = new ForeignKey(new SqlIdentifier("orders_user_fk"), [new SqlIdentifier("user_id")], new SqlIdentifier("app"), new SqlIdentifier("users"), [new SqlIdentifier("id")]);
        var table = DiffTable(
            new Table(new SqlIdentifier("orders"), columns: [new Column(new SqlIdentifier("user_id"), SqlType.Int)], foreignKeys: [fk]),
            new Table(new SqlIdentifier("orders"), columns: [new Column(new SqlIdentifier("user_id"), SqlType.Int)]));

        table!.ForeignKeys.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_ForeignKeyDefinitionChanged_EmitsRemoveAndAdd()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("orders"), columns: [new Column(new SqlIdentifier("user_id"), SqlType.Int)],
                foreignKeys: [new ForeignKey(new SqlIdentifier("orders_user_fk"), [new SqlIdentifier("user_id")], new SqlIdentifier("app"), new SqlIdentifier("users"), [new SqlIdentifier("id")])]),
            new Table(new SqlIdentifier("orders"), columns: [new Column(new SqlIdentifier("user_id"), SqlType.Int)],
                foreignKeys: [new ForeignKey(new SqlIdentifier("orders_user_fk"), [new SqlIdentifier("user_id")], new SqlIdentifier("app"), new SqlIdentifier("users"), [new SqlIdentifier("id")], onDelete: ReferentialAction.Cascade)]));

        table!.ForeignKeys.Select(c => c.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    // -------------------------------------------------------------------------
    // Unique constraints
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_UniqueConstraintAdded_EmitsAdd()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)],
                uniqueConstraints: [new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")])]));

        table!.UniqueConstraints.ShouldHaveSingleItem().ShouldBe(
            new UniqueConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_email_uq"), new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")])));
    }

    [Fact]
    public void Compare_UniqueConstraintRemoved_EmitsRemove()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)],
                uniqueConstraints: [new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")])]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)]));

        var unique = table!.UniqueConstraints.ShouldHaveSingleItem();
        unique.Kind.ShouldBe(ChangeKind.Remove);
        unique.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_UniqueConstraintColumnsChanged_EmitsRemoveThenAdd()
    {
        Column[] Columns() => [new Column(new SqlIdentifier("email"), SqlType.Text), new Column(new SqlIdentifier("tenant"), SqlType.Int)];
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: Columns(),
                uniqueConstraints: [new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")])]),
            new Table(new SqlIdentifier("users"), columns: Columns(),
                uniqueConstraints: [new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email"), new SqlIdentifier("tenant")])]));

        table!.UniqueConstraints.Select(c => c.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_UniqueConstraintCommentOnlyChanged_EmitsModifyNotRecreate()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)],
                uniqueConstraints: [new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")]) { Comment = "old" }]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)],
                uniqueConstraints: [new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")]) { Comment = "new" }]));

        var unique = table!.UniqueConstraints.ShouldHaveSingleItem();
        unique.Kind.ShouldBe(ChangeKind.Modify);
        unique.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_NewUniqueConstraintWithComment_FoldsCommentAsModify()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)],
                uniqueConstraints: [new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")]) { Comment = "lookup" }]));

        table!.UniqueConstraints.Select(c => (c.Kind, c.Comment?.New))
            .ShouldBe([(ChangeKind.Add, null), (ChangeKind.Modify, "lookup")]);
    }

    [Fact]
    public void Compare_UniqueConstraintUnchanged_ProducesNoDiff()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)],
                uniqueConstraints: [new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")])]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)],
                uniqueConstraints: [new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")])]));

        table.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Check constraints
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_CheckConstraintAdded_EmitsAdd()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("age"), SqlType.Int)]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("age"), SqlType.Int)],
                checkConstraints: [new CheckConstraint(new SqlIdentifier("users_age_chk"), new SqlText("age >= 0"))]));

        table!.Checks.ShouldHaveSingleItem().ShouldBe(
            new CheckConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_age_chk"), new CheckConstraint(new SqlIdentifier("users_age_chk"), new SqlText("age >= 0"))));
    }

    [Fact]
    public void Compare_CheckConstraintRemoved_EmitsRemove()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("age"), SqlType.Int)],
                checkConstraints: [new CheckConstraint(new SqlIdentifier("users_age_chk"), new SqlText("age >= 0"))]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("age"), SqlType.Int)]));

        var check = table!.Checks.ShouldHaveSingleItem();
        check.Kind.ShouldBe(ChangeKind.Remove);
        check.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_CheckConstraintExpressionChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("age"), SqlType.Int)],
                checkConstraints: [new CheckConstraint(new SqlIdentifier("users_age_chk"), new SqlText("age >= 0"))]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("age"), SqlType.Int)],
                checkConstraints: [new CheckConstraint(new SqlIdentifier("users_age_chk"), new SqlText("age > 0"))]));

        table!.Checks.Select(c => c.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_CheckConstraintCommentOnlyChanged_EmitsModifyNotRecreate()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("age"), SqlType.Int)],
                checkConstraints: [new CheckConstraint(new SqlIdentifier("users_age_chk"), new SqlText("age >= 0")) { Comment = "old" }]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("age"), SqlType.Int)],
                checkConstraints: [new CheckConstraint(new SqlIdentifier("users_age_chk"), new SqlText("age >= 0")) { Comment = "new" }]));

        var check = table!.Checks.ShouldHaveSingleItem();
        check.Kind.ShouldBe(ChangeKind.Modify);
        check.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_CheckConstraintUnchanged_ProducesNoDiff()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("age"), SqlType.Int)],
                checkConstraints: [new CheckConstraint(new SqlIdentifier("users_age_chk"), new SqlText("age >= 0"))]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("age"), SqlType.Int)],
                checkConstraints: [new CheckConstraint(new SqlIdentifier("users_age_chk"), new SqlText("age >= 0"))]));

        table.ShouldBeNull();
    }

    [Fact]
    public void Compare_NewTable_FoldsUniqueAndCheckConstraintsAsAdds()
    {
        // On a new table the primary key is created inline (carried on Definition), but unique and check
        // constraints arrive as separate adds, mirroring how foreign keys fold.
        var desired = new Table(new SqlIdentifier("users"),
            columns: [new Column(new SqlIdentifier("email"), SqlType.Text), new Column(new SqlIdentifier("age"), SqlType.Int)],
            uniqueConstraints: [new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")])],
            checkConstraints: [new CheckConstraint(new SqlIdentifier("users_age_chk"), new SqlText("age >= 0"))]);

        var table = Compare(Db(new Schema(new SqlIdentifier("app"))),
            Db(new Schema(new SqlIdentifier("app"), tables: [desired]))).Schemas.Single().Tables.Single();

        table.Kind.ShouldBe(ChangeKind.Add);
        table.UniqueConstraints.ShouldHaveSingleItem().ShouldBe(
            new UniqueConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_email_uq"), new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")])));
        table.Checks.ShouldHaveSingleItem().ShouldBe(
            new CheckConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_age_chk"), new CheckConstraint(new SqlIdentifier("users_age_chk"), new SqlText("age >= 0"))));
    }

    // -------------------------------------------------------------------------
    // Indexes
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_IndexRemoved_EmitsRemove()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)], indexes: [new TableIndex(new SqlIdentifier("users_email_ix"), ["email"])]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)]));

        table!.Indexes.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_IndexDefinitionChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)], indexes: [new TableIndex(new SqlIdentifier("users_email_ix"), ["email"])]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)], indexes: [new TableIndex(new SqlIdentifier("users_email_ix"), ["email"], isUnique: true)]));

        table!.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_IndexCommentOnlyChange_EmitsModify()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)], indexes: [new TableIndex(new SqlIdentifier("users_email_ix"), ["email"]) { Comment = "old" }]),
            new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text)], indexes: [new TableIndex(new SqlIdentifier("users_email_ix"), ["email"]) { Comment = "new" }]));

        var index = table!.Indexes.ShouldHaveSingleItem();
        index.Kind.ShouldBe(ChangeKind.Modify);
        index.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_IndexMethodChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("t"), columns: [new Column(new SqlIdentifier("tags"), SqlType.Text)], indexes: [new TableIndex(new SqlIdentifier("t_tags_ix"), ["tags"])]),
            new Table(new SqlIdentifier("t"), columns: [new Column(new SqlIdentifier("tags"), SqlType.Text)], indexes: [new TableIndex(new SqlIdentifier("t_tags_ix"), ["tags"], method: "gin")]));

        table!.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_IndexIncludeChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("t"), columns: [new Column(new SqlIdentifier("a"), SqlType.Int), new Column(new SqlIdentifier("b"), SqlType.Int)], indexes: [new TableIndex(new SqlIdentifier("t_ix"), ["a"])]),
            new Table(new SqlIdentifier("t"), columns: [new Column(new SqlIdentifier("a"), SqlType.Int), new Column(new SqlIdentifier("b"), SqlType.Int)], indexes: [new TableIndex(new SqlIdentifier("t_ix"), ["a"], include: [new SqlIdentifier("b")])]));

        table!.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_IndexKeyOrderingChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table(new SqlIdentifier("t"), columns: [new Column(new SqlIdentifier("a"), SqlType.Int)], indexes: [new TableIndex(new SqlIdentifier("t_ix"), ["a"])]),
            new Table(new SqlIdentifier("t"), columns: [new Column(new SqlIdentifier("a"), SqlType.Int)], indexes: [new TableIndex(new SqlIdentifier("t_ix"), [new IndexColumn(new SqlIdentifier("a"), Sort: IndexSort.Descending)])]));

        table!.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    // -------------------------------------------------------------------------
    // Exclusion constraints
    // -------------------------------------------------------------------------

    private static ExclusionConstraint NoOverlap(string? method = "gist", string? comment = null) =>
        new(new SqlIdentifier("no_overlap"), [new ExclusionElement("=", new SqlIdentifier("room")), new ExclusionElement("&&", new SqlIdentifier("during"))], method) { Comment = comment };

    private static Table Bookings(params ExclusionConstraint[] exclusions) =>
        new(new SqlIdentifier("bookings"), columns: [new Column(new SqlIdentifier("room"), SqlType.Int), new Column(new SqlIdentifier("during"), SqlType.Int)], exclusionConstraints: exclusions);

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
