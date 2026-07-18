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
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] },
            new Table { Name = new SqlIdentifier("users"), PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("users_pkey"), ColumnNames = [new SqlIdentifier("id")] }, Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] });

        table!.PrimaryKey.ShouldHaveSingleItem().ShouldBe(
            new PrimaryKeyDiff(ChangeKind.Add, new SqlIdentifier("users_pkey"), new PrimaryKey { Name = new SqlIdentifier("users_pkey"), ColumnNames = [new SqlIdentifier("id")] }));
    }

    [Fact]
    public void Compare_PrimaryKeyDropped_EmitsRemoveConstraint()
    {
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("users"), PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("users_pkey"), ColumnNames = [new SqlIdentifier("id")] }, Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] },
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] });

        table!.PrimaryKey.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_PrimaryKeyChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("users"), PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("users_pkey"), ColumnNames = [new SqlIdentifier("id")] }, Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] },
            new Table { Name = new SqlIdentifier("users"), PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("users_pkey"), ColumnNames = [new SqlIdentifier("id"), new SqlIdentifier("tenant")] }, Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] });

        table!.PrimaryKey.Select(c => c.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_PrimaryKeyCommentOnlyChanged_EmitsModifyNotRecreate()
    {
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("users"), PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("users_pkey"), ColumnNames = [new SqlIdentifier("id")], Comment = "old" }, Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] },
            new Table { Name = new SqlIdentifier("users"), PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("users_pkey"), ColumnNames = [new SqlIdentifier("id")], Comment = "new" }, Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] });

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
        var fk = new ForeignKey { Name = new SqlIdentifier("orders_user_fk"), ColumnNames = [new SqlIdentifier("user_id")], ReferencedSchema = new SqlIdentifier("app"), ReferencedTable = new SqlIdentifier("users"), ReferencedColumnNames = [new SqlIdentifier("id")] };
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("orders"), Columns = [new Column { Name = new SqlIdentifier("user_id"), Type = SqlType.Int }], ForeignKeys = [fk] },
            new Table { Name = new SqlIdentifier("orders"), Columns = [new Column { Name = new SqlIdentifier("user_id"), Type = SqlType.Int }] });

        table!.ForeignKeys.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_ForeignKeyDefinitionChanged_EmitsRemoveAndAdd()
    {
        var table = DiffTable(
            new Table
            {
                Name = new SqlIdentifier("orders"),
                Columns = [new Column { Name = new SqlIdentifier("user_id"), Type = SqlType.Int }],
                ForeignKeys = [new ForeignKey { Name = new SqlIdentifier("orders_user_fk"), ColumnNames = [new SqlIdentifier("user_id")], ReferencedSchema = new SqlIdentifier("app"), ReferencedTable = new SqlIdentifier("users"), ReferencedColumnNames = [new SqlIdentifier("id")] }],
            },
            new Table
            {
                Name = new SqlIdentifier("orders"),
                Columns = [new Column { Name = new SqlIdentifier("user_id"), Type = SqlType.Int }],
                ForeignKeys = [new ForeignKey { Name = new SqlIdentifier("orders_user_fk"), ColumnNames = [new SqlIdentifier("user_id")], ReferencedSchema = new SqlIdentifier("app"), ReferencedTable = new SqlIdentifier("users"), ReferencedColumnNames = [new SqlIdentifier("id")], OnDelete = ReferentialAction.Cascade }],
            });

        table!.ForeignKeys.Select(c => c.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    // -------------------------------------------------------------------------
    // Unique constraints
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_UniqueConstraintAdded_EmitsAdd()
    {
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }] },
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }],
                UniqueConstraints = [new UniqueConstraint { Name = new SqlIdentifier("users_email_uq"), ColumnNames = [new SqlIdentifier("email")] }],
            });

        table!.UniqueConstraints.ShouldHaveSingleItem().ShouldBe(
            new UniqueConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_email_uq"), new UniqueConstraint { Name = new SqlIdentifier("users_email_uq"), ColumnNames = [new SqlIdentifier("email")] }));
    }

    [Fact]
    public void Compare_UniqueConstraintRemoved_EmitsRemove()
    {
        var table = DiffTable(
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }],
                UniqueConstraints = [new UniqueConstraint { Name = new SqlIdentifier("users_email_uq"), ColumnNames = [new SqlIdentifier("email")] }],
            },
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }] });

        var unique = table!.UniqueConstraints.ShouldHaveSingleItem();
        unique.Kind.ShouldBe(ChangeKind.Remove);
        unique.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_UniqueConstraintColumnsChanged_EmitsRemoveThenAdd()
    {
        DatabaseMemberCollection<Column> Columns() => [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }, new Column { Name = new SqlIdentifier("tenant"), Type = SqlType.Int }];
        var table = DiffTable(
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = Columns(),
                UniqueConstraints = [new UniqueConstraint { Name = new SqlIdentifier("users_email_uq"), ColumnNames = [new SqlIdentifier("email")] }],
            },
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = Columns(),
                UniqueConstraints = [new UniqueConstraint { Name = new SqlIdentifier("users_email_uq"), ColumnNames = [new SqlIdentifier("email"), new SqlIdentifier("tenant")] }],
            });

        table!.UniqueConstraints.Select(c => c.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_UniqueConstraintCommentOnlyChanged_EmitsModifyNotRecreate()
    {
        var table = DiffTable(
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }],
                UniqueConstraints = [new UniqueConstraint { Name = new SqlIdentifier("users_email_uq"), ColumnNames = [new SqlIdentifier("email")], Comment = "old" }],
            },
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }],
                UniqueConstraints = [new UniqueConstraint { Name = new SqlIdentifier("users_email_uq"), ColumnNames = [new SqlIdentifier("email")], Comment = "new" }],
            });

        var unique = table!.UniqueConstraints.ShouldHaveSingleItem();
        unique.Kind.ShouldBe(ChangeKind.Modify);
        unique.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_NewUniqueConstraintWithComment_FoldsCommentAsModify()
    {
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }] },
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }],
                UniqueConstraints = [new UniqueConstraint { Name = new SqlIdentifier("users_email_uq"), ColumnNames = [new SqlIdentifier("email")], Comment = "lookup" }],
            });

        table!.UniqueConstraints.Select(c => (c.Kind, c.Comment?.New))
            .ShouldBe([(ChangeKind.Add, null), (ChangeKind.Modify, "lookup")]);
    }

    [Fact]
    public void Compare_UniqueConstraintUnchanged_ProducesNoDiff()
    {
        var table = DiffTable(
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }],
                UniqueConstraints = [new UniqueConstraint { Name = new SqlIdentifier("users_email_uq"), ColumnNames = [new SqlIdentifier("email")] }],
            },
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }],
                UniqueConstraints = [new UniqueConstraint { Name = new SqlIdentifier("users_email_uq"), ColumnNames = [new SqlIdentifier("email")] }],
            });

        table.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Check constraints
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_CheckConstraintAdded_EmitsAdd()
    {
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("age"), Type = SqlType.Int }] },
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = [new Column { Name = new SqlIdentifier("age"), Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = new SqlIdentifier("users_age_chk"), Expression = new SqlText("age >= 0") }],
            });

        table!.Checks.ShouldHaveSingleItem().ShouldBe(
            new CheckConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_age_chk"), new CheckConstraint { Name = new SqlIdentifier("users_age_chk"), Expression = new SqlText("age >= 0") }));
    }

    [Fact]
    public void Compare_CheckConstraintRemoved_EmitsRemove()
    {
        var table = DiffTable(
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = [new Column { Name = new SqlIdentifier("age"), Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = new SqlIdentifier("users_age_chk"), Expression = new SqlText("age >= 0") }],
            },
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("age"), Type = SqlType.Int }] });

        var check = table!.Checks.ShouldHaveSingleItem();
        check.Kind.ShouldBe(ChangeKind.Remove);
        check.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_CheckConstraintExpressionChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = [new Column { Name = new SqlIdentifier("age"), Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = new SqlIdentifier("users_age_chk"), Expression = new SqlText("age >= 0") }],
            },
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = [new Column { Name = new SqlIdentifier("age"), Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = new SqlIdentifier("users_age_chk"), Expression = new SqlText("age > 0") }],
            });

        table!.Checks.Select(c => c.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_CheckConstraintCommentOnlyChanged_EmitsModifyNotRecreate()
    {
        var table = DiffTable(
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = [new Column { Name = new SqlIdentifier("age"), Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = new SqlIdentifier("users_age_chk"), Expression = new SqlText("age >= 0"), Comment = "old" }],
            },
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = [new Column { Name = new SqlIdentifier("age"), Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = new SqlIdentifier("users_age_chk"), Expression = new SqlText("age >= 0"), Comment = "new" }],
            });

        var check = table!.Checks.ShouldHaveSingleItem();
        check.Kind.ShouldBe(ChangeKind.Modify);
        check.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_CheckConstraintUnchanged_ProducesNoDiff()
    {
        var table = DiffTable(
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = [new Column { Name = new SqlIdentifier("age"), Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = new SqlIdentifier("users_age_chk"), Expression = new SqlText("age >= 0") }],
            },
            new Table
            {
                Name = new SqlIdentifier("users"),
                Columns = [new Column { Name = new SqlIdentifier("age"), Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = new SqlIdentifier("users_age_chk"), Expression = new SqlText("age >= 0") }],
            });

        table.ShouldBeNull();
    }

    [Fact]
    public void Compare_NewTable_FoldsUniqueAndCheckConstraintsAsAdds()
    {
        // On a new table the primary key is created inline (carried on Definition), but unique and check
        // constraints arrive as separate adds, mirroring how foreign keys fold.
        var desired = new Table
        {
            Name = new SqlIdentifier("users"),
            Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }, new Column { Name = new SqlIdentifier("age"), Type = SqlType.Int }],
            UniqueConstraints = [new UniqueConstraint { Name = new SqlIdentifier("users_email_uq"), ColumnNames = [new SqlIdentifier("email")] }],
            CheckConstraints = [new CheckConstraint { Name = new SqlIdentifier("users_age_chk"), Expression = new SqlText("age >= 0") }],
        };

        var table = Compare(Db(new Schema { Name = new SqlIdentifier("app") }),
            Db(new Schema { Name = new SqlIdentifier("app"), Tables = [desired] })).Schemas.Single().Tables.Single();

        table.Kind.ShouldBe(ChangeKind.Add);
        table.UniqueConstraints.ShouldHaveSingleItem().ShouldBe(
            new UniqueConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_email_uq"), new UniqueConstraint { Name = new SqlIdentifier("users_email_uq"), ColumnNames = [new SqlIdentifier("email")] }));
        table.Checks.ShouldHaveSingleItem().ShouldBe(
            new CheckConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_age_chk"), new CheckConstraint { Name = new SqlIdentifier("users_age_chk"), Expression = new SqlText("age >= 0") }));
    }

    // -------------------------------------------------------------------------
    // Indexes
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_IndexRemoved_EmitsRemove()
    {
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }], Indexes = [new TableIndex { Name = new SqlIdentifier("users_email_ix"), Columns = ["email"] }] },
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }] });

        table!.Indexes.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_IndexDefinitionChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }], Indexes = [new TableIndex { Name = new SqlIdentifier("users_email_ix"), Columns = ["email"] }] },
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }], Indexes = [new TableIndex { Name = new SqlIdentifier("users_email_ix"), Columns = ["email"], IsUnique = true }] });

        table!.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_IndexCommentOnlyChange_EmitsModify()
    {
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }], Indexes = [new TableIndex { Name = new SqlIdentifier("users_email_ix"), Columns = ["email"], Comment = "old" }] },
            new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }], Indexes = [new TableIndex { Name = new SqlIdentifier("users_email_ix"), Columns = ["email"], Comment = "new" }] });

        var index = table!.Indexes.ShouldHaveSingleItem();
        index.Kind.ShouldBe(ChangeKind.Modify);
        index.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_IndexMethodChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("t"), Columns = [new Column { Name = new SqlIdentifier("tags"), Type = SqlType.Text }], Indexes = [new TableIndex { Name = new SqlIdentifier("t_tags_ix"), Columns = ["tags"] }] },
            new Table { Name = new SqlIdentifier("t"), Columns = [new Column { Name = new SqlIdentifier("tags"), Type = SqlType.Text }], Indexes = [new TableIndex { Name = new SqlIdentifier("t_tags_ix"), Columns = ["tags"], Method = "gin" }] });

        table!.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_IndexIncludeChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("t"), Columns = [new Column { Name = new SqlIdentifier("a"), Type = SqlType.Int }, new Column { Name = new SqlIdentifier("b"), Type = SqlType.Int }], Indexes = [new TableIndex { Name = new SqlIdentifier("t_ix"), Columns = ["a"] }] },
            new Table { Name = new SqlIdentifier("t"), Columns = [new Column { Name = new SqlIdentifier("a"), Type = SqlType.Int }, new Column { Name = new SqlIdentifier("b"), Type = SqlType.Int }], Indexes = [new TableIndex { Name = new SqlIdentifier("t_ix"), Columns = ["a"], Include = [new SqlIdentifier("b")] }] });

        table!.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_IndexKeyOrderingChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table { Name = new SqlIdentifier("t"), Columns = [new Column { Name = new SqlIdentifier("a"), Type = SqlType.Int }], Indexes = [new TableIndex { Name = new SqlIdentifier("t_ix"), Columns = ["a"] }] },
            new Table { Name = new SqlIdentifier("t"), Columns = [new Column { Name = new SqlIdentifier("a"), Type = SqlType.Int }], Indexes = [new TableIndex { Name = new SqlIdentifier("t_ix"), Columns = [new IndexColumn(new SqlIdentifier("a"), Sort: IndexSort.Descending)] }] });

        table!.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    // -------------------------------------------------------------------------
    // Exclusion constraints
    // -------------------------------------------------------------------------

    private static ExclusionConstraint NoOverlap(string? method = "gist", string? comment = null) =>
        new ExclusionConstraint { Name = new SqlIdentifier("no_overlap"), Elements = [new ExclusionElement("=", new SqlIdentifier("room")), new ExclusionElement("&&", new SqlIdentifier("during"))], Method = method, Comment = comment };

    private static Table Bookings(params ExclusionConstraint[] exclusions) =>
        new Table { Name = new SqlIdentifier("bookings"), Columns = [new Column { Name = new SqlIdentifier("room"), Type = SqlType.Int }, new Column { Name = new SqlIdentifier("during"), Type = SqlType.Int }], ExclusionConstraints = [.. exclusions] };

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
