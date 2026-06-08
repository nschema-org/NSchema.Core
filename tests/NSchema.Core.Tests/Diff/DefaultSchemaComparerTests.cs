using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Tests.Diff;

/// <summary>
/// Covers the structured-diff projection the comparer now produces directly (formerly the responsibility of
/// DefaultDiffBuilder), driven from realistic schema inputs.
/// </summary>
public class DefaultSchemaComparerTests
{
    private readonly DefaultSchemaComparer _sut = new(NullLogger<DefaultSchemaComparer>.Instance);

    private static DatabaseSchema Db(params SchemaDefinition[] schemas) => DatabaseSchema.Create(schemas);

    /// <summary>Diffs two single-table <c>app</c> schemas, returning the table diff (null when unchanged).</summary>
    private TableDiff? DiffTable(Table current, Table desired) => _sut
        .Compare(Db(SchemaDefinition.Create("app", tables: [current])), Db(SchemaDefinition.Create("app", tables: [desired])))
        .Schemas.SingleOrDefault()?.Tables.SingleOrDefault();

    /// <summary>Diffs two single-column <c>app.t</c> tables, returning the column diff (null when unchanged).</summary>
    private ColumnDiff? DiffColumn(Column current, Column desired) =>
        DiffTable(Table.Create("t", columns: [current]), Table.Create("t", columns: [desired]))?.Columns.SingleOrDefault();

    [Fact]
    public void Compare_BothEmpty_ProducesEmptyDiff()
    {
        var diff = _sut.Compare(Db(), Db());

        diff.IsEmpty.ShouldBeTrue();
        diff.Schemas.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_NestsTablesUnderSchema_OrderedByName()
    {
        var current = Db(SchemaDefinition.Create("app", tables:
        [
            Table.Create("orders", columns: [Column.Create("id", SqlType.Int)]),
            Table.Create("audit_log", columns: [Column.Create("id", SqlType.Int)]),
        ]));
        var desired = Db(SchemaDefinition.Create("app", tables:
        [
            Table.Create("orders", columns: [Column.Create("id", SqlType.Int), Column.Create("shipped_at", SqlType.DateTimeOffset)]),
        ]));

        var schema = _sut.Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Name.ShouldBe("app");
        schema.Kind.ShouldBeNull(); // only its tables changed
        schema.Tables.Select(t => t.Name).ShouldBe(["audit_log", "orders"]); // ordered by name
        schema.Tables.Single(t => t.Name == "orders").Kind.ShouldBe(ChangeKind.Modify);
        schema.Tables.Single(t => t.Name == "audit_log").Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_Summary_CountsEveryChangedElementByKind()
    {
        var current = Db(SchemaDefinition.Create("app", tables:
        [
            Table.Create("orders", columns: [Column.Create("id", SqlType.Int)]),
            Table.Create("audit_log", columns: [Column.Create("id", SqlType.Int)]),
        ]));
        var desired = Db(
            SchemaDefinition.Create("app", tables:
            [
                Table.Create("orders", columns: [Column.Create("id", SqlType.Int), Column.Create("shipped_at", SqlType.DateTimeOffset)]),
            ]),
            SchemaDefinition.Create("reporting"));

        // reporting schema (Add) + shipped_at column (Add); orders table (Modify); audit_log table (Remove).
        _sut.Compare(current, desired).GetSummary().ShouldBe(new DiffSummary(Added: 2, Modified: 1, Removed: 1));
    }

    [Fact]
    public void Compare_TableChangeWithoutSchemaChange_LeavesSchemaKindNull()
    {
        var current = Db(SchemaDefinition.Create("app", tables: [Table.Create("users", columns: [Column.Create("id", SqlType.Int)])]));
        var desired = Db(SchemaDefinition.Create("app", tables:
            [Table.Create("users", columns: [Column.Create("id", SqlType.Int), Column.Create("email", SqlType.Text)])]));

        var schema = _sut.Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Kind.ShouldBeNull();
        schema.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }

    [Fact]
    public void Compare_CreateTable_AddsEveryColumnWithDefinitionAndFoldedComment()
    {
        var current = Db(SchemaDefinition.Create("app"));
        var desired = Db(SchemaDefinition.Create("app", tables:
        [
            Table.Create("users", columns:
            [
                Column.Create("id", SqlType.Int, isNullable: false),
                Column.Create("email", SqlType.Text, isNullable: false, comment: "login"),
            ]),
        ]));

        var table = _sut.Compare(current, desired).Schemas.Single().Tables.Single();

        table.Kind.ShouldBe(ChangeKind.Add);
        table.Columns.Select(c => c.Name).ShouldBe(["id", "email"]);
        table.Columns.ShouldAllBe(c => c.Kind == ChangeKind.Add && c.Definition != null);
        table.Columns.Single(c => c.Name == "email").Comment.ShouldBe(new ValueChange<string>(null, "login"));
        table.Columns.Single(c => c.Name == "id").Comment.ShouldBeNull();
    }

    [Fact]
    public void Compare_MergesMultipleChangesToOneColumnIntoASingleDiff()
    {
        var current = Db(SchemaDefinition.Create("app", tables:
            [Table.Create("users", columns: [Column.Create("email", SqlType.Text, isNullable: false, comment: "old")])]));
        var desired = Db(SchemaDefinition.Create("app", tables:
            [Table.Create("users", columns: [Column.Create("email", SqlType.Text, isNullable: true, comment: "new")])]));

        var column = _sut.Compare(current, desired).Schemas.Single().Tables.Single().Columns.ShouldHaveSingleItem();

        column.Name.ShouldBe("email");
        column.Kind.ShouldBe(ChangeKind.Modify);
        column.Nullability.ShouldBe(new ValueChange<bool>(false, true));
        column.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_GroupsIndexesConstraintsAndGrantsUnderTheirTable()
    {
        var columns = new[] { Column.Create("id", SqlType.Int), Column.Create("user_id", SqlType.Int) };
        var current = Db(SchemaDefinition.Create("app", tables: [Table.Create("orders", columns: columns)]));
        var desired = Db(SchemaDefinition.Create("app", tables:
        [
            Table.Create("orders",
                columns: columns,
                primaryKey: new PrimaryKey("orders_pkey", ["id"]),
                foreignKeys: [ForeignKey.Create("orders_user_fk", ["user_id"], "app", "users", ["id"])],
                indexes: [TableIndex.Create("orders_user_ix", ["user_id"])],
                grants: [new TableGrant("reader", TablePrivilege.Insert)]),
        ]));

        var table = _sut.Compare(current, desired).Schemas.Single().Tables.Single();

        table.Constraints.Select(c => (c.Type, c.Name)).ShouldBe(
            [(ConstraintType.PrimaryKey, "orders_pkey"), (ConstraintType.ForeignKey, "orders_user_fk")]);
        table.Indexes.ShouldHaveSingleItem().Name.ShouldBe("orders_user_ix");
        var grant = table.Grants.ShouldHaveSingleItem();
        grant.Role.ShouldBe("reader");
        grant.Privileges.ShouldBe(TablePrivilege.Insert);
    }

    [Fact]
    public void Compare_FoldsSchemaRenameCommentAndGrantsIntoSchemaDiff()
    {
        var current = Db(SchemaDefinition.Create("app_old", grants: [new SchemaGrant("writer")]));
        var desired = Db(SchemaDefinition.Create("app", oldName: "app_old", comment: "new comment", grants: [new SchemaGrant("reader")]));

        var schema = _sut.Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Kind.ShouldBe(ChangeKind.Modify);
        schema.RenamedFrom.ShouldBe("app_old");
        schema.Comment.ShouldBe(new ValueChange<string>(null, "new comment"));
        schema.Grants.ShouldBe([
            new GrantChange(ChangeKind.Remove, "writer", null),
            new GrantChange(ChangeKind.Add, "reader", null),
        ]);
    }

    // -------------------------------------------------------------------------
    // Schema-level add / remove / sort / no-op
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_IdenticalSchemas_ProduceNoDiff()
    {
        var schema = SchemaDefinition.Create("app", tables: [Table.Create("users", columns: [Column.Create("id", SqlType.Int)])]);

        _sut.Compare(Db(schema), Db(schema)).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Compare_SchemaInCurrentButNotDesired_IsRemoved()
    {
        var current = Db(SchemaDefinition.Create("app"), SchemaDefinition.Create("legacy"));
        var desired = Db(SchemaDefinition.Create("app"));

        var schema = _sut.Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Name.ShouldBe("legacy");
        schema.Kind.ShouldBe(ChangeKind.Remove);
        schema.Tables.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_NewSchema_FoldsCommentGrantsAndTablesWithDefinition()
    {
        var current = Db();
        var desired = Db(SchemaDefinition.Create("reporting",
            comment: "analytics",
            grants: [new SchemaGrant("reader")],
            tables: [Table.Create("metrics", columns: [Column.Create("id", SqlType.Int)])]));

        var schema = _sut.Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Kind.ShouldBe(ChangeKind.Add);
        schema.Comment.ShouldBe(new ValueChange<string>(null, "analytics"));
        schema.Grants.ShouldHaveSingleItem().ShouldBe(new GrantChange(ChangeKind.Add, "reader", null));
        var table = schema.Tables.ShouldHaveSingleItem();
        table.Kind.ShouldBe(ChangeKind.Add);
        table.Definition.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_OrdersResultSchemasByName()
    {
        var diff = _sut.Compare(Db(), Db(SchemaDefinition.Create("zeta"), SchemaDefinition.Create("alpha")));

        diff.Schemas.Select(s => s.Name).ShouldBe(["alpha", "zeta"]);
    }

    // -------------------------------------------------------------------------
    // Table removal: partial vs full schemas and explicit drops
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_FullSchema_DropsCurrentTableNotInDesired()
    {
        var current = Db(SchemaDefinition.Create("app", tables: [Table.Create("stale", columns: [Column.Create("id", SqlType.Int)])]));
        var desired = Db(SchemaDefinition.Create("app", tables: [Table.Create("fresh", columns: [Column.Create("id", SqlType.Int)])]));

        var tables = _sut.Compare(current, desired).Schemas.Single().Tables;

        tables.Single(t => t.Name == "stale").Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_PartialSchema_LeavesUnlistedCurrentTableUntouched()
    {
        var current = Db(SchemaDefinition.Create("app", tables: [Table.Create("untracked", columns: [Column.Create("id", SqlType.Int)])]));
        var desired = Db(SchemaDefinition.Create("app", isPartial: true,
            tables: [Table.Create("fresh", columns: [Column.Create("id", SqlType.Int)])]));

        var tables = _sut.Compare(current, desired).Schemas.Single().Tables;

        tables.Select(t => t.Name).ShouldBe(["fresh"]); // 'untracked' is neither dropped nor reported
    }

    [Fact]
    public void Compare_PartialSchema_StillDropsExplicitlyDroppedTable()
    {
        var current = Db(SchemaDefinition.Create("app", tables: [Table.Create("retired", columns: [Column.Create("id", SqlType.Int)])]));
        var desired = Db(SchemaDefinition.Create("app", isPartial: true, droppedTables: ["retired"]));

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
            Table.Create("people", columns: [Column.Create("id", SqlType.Int)]),
            Table.Create("users", oldName: "people", columns: [Column.Create("id", SqlType.Int)]));

        table.ShouldNotBeNull();
        table.Kind.ShouldBe(ChangeKind.Modify);
        table.RenamedFrom.ShouldBe("people");
    }

    [Fact]
    public void Compare_TableRenamedAndOldNameReused_RenamesAndCreatesWithoutDropping()
    {
        // 'people' is renamed to 'users', and a brand-new 'people' table reuses the freed-up name in the same diff.
        // The single current 'people' must be claimed by the rename only; the new 'people' must be an Add, not a Modify.
        var current = Db(SchemaDefinition.Create("app", tables:
        [
            Table.Create("people", columns: [Column.Create("id", SqlType.Int)]),
        ]));
        var desired = Db(SchemaDefinition.Create("app", tables:
        [
            Table.Create("users", oldName: "people", columns: [Column.Create("id", SqlType.Int)]),
            Table.Create("people", columns: [Column.Create("id", SqlType.Int)]),
        ]));

        var tables = _sut.Compare(current, desired).Schemas.ShouldHaveSingleItem().Tables;

        var renamed = tables.Single(t => t.Name == "users");
        renamed.Kind.ShouldBe(ChangeKind.Modify);
        renamed.RenamedFrom.ShouldBe("people");

        var created = tables.Single(t => t.Name == "people");
        created.Kind.ShouldBe(ChangeKind.Add);

        tables.ShouldNotContain(t => t.Kind == ChangeKind.Remove);
    }

    [Fact]
    public void Compare_ColumnRenamedAndOldNameReused_RenamesAndCreatesWithoutDropping()
    {
        var renamedThenReused = DiffTable(
            Table.Create("t", columns: [Column.Create("mail", SqlType.Text)]),
            Table.Create("t", columns:
            [
                Column.Create("email", SqlType.Text, oldName: "mail"),
                Column.Create("mail", SqlType.Text),
            ]));

        var columns = renamedThenReused.ShouldNotBeNull().Columns;
        columns.Single(c => c.Name == "email").RenamedFrom.ShouldBe("mail");
        columns.Single(c => c.Name == "mail").Kind.ShouldBe(ChangeKind.Add);
        columns.ShouldNotContain(c => c.Kind == ChangeKind.Remove);
    }

    [Fact]
    public void Compare_TableCommentChange_FoldsCommentValueChange()
    {
        var table = DiffTable(
            Table.Create("users", comment: "old", columns: [Column.Create("id", SqlType.Int)]),
            Table.Create("users", comment: "new", columns: [Column.Create("id", SqlType.Int)]));

        table!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_NewTable_FoldsForeignKeysIndexesGrantsAndIndexComment()
    {
        var desired = Table.Create("orders",
            columns: [Column.Create("id", SqlType.Int), Column.Create("user_id", SqlType.Int)],
            foreignKeys: [ForeignKey.Create("orders_user_fk", ["user_id"], "app", "users", ["id"])],
            indexes: [TableIndex.Create("orders_user_ix", ["user_id"], comment: "lookup")],
            grants: [new TableGrant("reader", TablePrivilege.Select)]);

        var table = _sut.Compare(Db(SchemaDefinition.Create("app")),
            Db(SchemaDefinition.Create("app", tables: [desired]))).Schemas.Single().Tables.Single();

        table.Constraints.ShouldHaveSingleItem().ShouldBe(new ConstraintDiff(ChangeKind.Add, ConstraintType.ForeignKey, "orders_user_fk", null,
            ForeignKey.Create("orders_user_fk", ["user_id"], "app", "users", ["id"])));
        table.Grants.ShouldHaveSingleItem().Privileges.ShouldBe(TablePrivilege.Select);
        // A new index carries both its definition and a folded comment change.
        table.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Add, ChangeKind.Modify]);
        table.Indexes[1].Comment.ShouldBe(new ValueChange<string>(null, "lookup"));
    }

    // -------------------------------------------------------------------------
    // Column-level changes
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_ColumnInCurrentButNotDesired_IsRemoved()
    {
        var table = DiffTable(
            Table.Create("users", columns: [Column.Create("id", SqlType.Int), Column.Create("email", SqlType.Text)]),
            Table.Create("users", columns: [Column.Create("id", SqlType.Int)]));

        var column = table!.Columns.ShouldHaveSingleItem();
        column.Name.ShouldBe("email");
        column.Kind.ShouldBe(ChangeKind.Remove);
        column.Definition.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_ColumnRename_SetsRenamedFrom()
    {
        var column = DiffColumn(Column.Create("mail", SqlType.Text), Column.Create("email", SqlType.Text, oldName: "mail"));

        column!.RenamedFrom.ShouldBe("mail");
        column.Kind.ShouldBe(ChangeKind.Modify);
    }

    [Fact]
    public void Compare_ColumnTypeChange_IsReportedInIsolation()
    {
        var column = DiffColumn(Column.Create("total", SqlType.Int), Column.Create("total", SqlType.BigInt));

        column!.Type.ShouldBe(new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt));
        column.Nullability.ShouldBeNull();
        column.Default.ShouldBeNull();
    }

    [Fact]
    public void Compare_ColumnDefaultChange_IsReported()
    {
        var column = DiffColumn(Column.Create("status", SqlType.Text), Column.Create("status", SqlType.Text, defaultExpression: "'new'"));

        column!.Default.ShouldBe(new ValueChange<string>(null, "'new'"));
    }

    [Fact]
    public void Compare_IdentityOptionsChange_IsReported_WhenBothColumnsAreIdentity()
    {
        var current = Column.Create("id", SqlType.Int, isIdentity: true, identityOptions: new IdentityOptions(1, 1, 1));
        var desired = Column.Create("id", SqlType.Int, isIdentity: true, identityOptions: new IdentityOptions(100, 1, 1));

        var column = DiffColumn(current, desired);

        column!.Identity.ShouldBe(new ValueChange<IdentityOptions>(new IdentityOptions(1, 1, 1), new IdentityOptions(100, 1, 1)));
    }

    [Fact]
    public void Compare_IdentityEnabled_ReportsChangeFromNullToDesiredOptions()
    {
        var current = Column.Create("id", SqlType.Int);
        var desired = Column.Create("id", SqlType.Int, isIdentity: true, identityOptions: new IdentityOptions(1, 1, 1));

        var column = DiffColumn(current, desired);

        column!.Identity.ShouldBe(new ValueChange<IdentityOptions>(null, new IdentityOptions(1, 1, 1)));
    }

    [Fact]
    public void Compare_IdentityDisabled_ReportsChangeFromCurrentOptionsToNull()
    {
        var current = Column.Create("id", SqlType.Int, isIdentity: true, identityOptions: new IdentityOptions(1, 1, 1));
        var desired = Column.Create("id", SqlType.Int);

        var column = DiffColumn(current, desired);

        column!.Identity.ShouldBe(new ValueChange<IdentityOptions>(new IdentityOptions(1, 1, 1), null));
    }

    [Fact]
    public void Compare_UnchangedColumn_ProducesNoDiff()
        => DiffColumn(Column.Create("id", SqlType.Int), Column.Create("id", SqlType.Int)).ShouldBeNull();

    // -------------------------------------------------------------------------
    // Primary keys
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_PrimaryKeyAdded_EmitsAddConstraint()
    {
        var table = DiffTable(
            Table.Create("users", columns: [Column.Create("id", SqlType.Int)]),
            Table.Create("users", primaryKey: new PrimaryKey("users_pkey", ["id"]), columns: [Column.Create("id", SqlType.Int)]));

        table!.Constraints.ShouldHaveSingleItem().ShouldBe(
            new ConstraintDiff(ChangeKind.Add, ConstraintType.PrimaryKey, "users_pkey", new PrimaryKey("users_pkey", ["id"]), null));
    }

    [Fact]
    public void Compare_PrimaryKeyDropped_EmitsRemoveConstraint()
    {
        var table = DiffTable(
            Table.Create("users", primaryKey: new PrimaryKey("users_pkey", ["id"]), columns: [Column.Create("id", SqlType.Int)]),
            Table.Create("users", columns: [Column.Create("id", SqlType.Int)]));

        table!.Constraints.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_PrimaryKeyChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            Table.Create("users", primaryKey: new PrimaryKey("users_pkey", ["id"]), columns: [Column.Create("id", SqlType.Int)]),
            Table.Create("users", primaryKey: new PrimaryKey("users_pkey", ["id", "tenant"]), columns: [Column.Create("id", SqlType.Int)]));

        table!.Constraints.Select(c => c.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    // -------------------------------------------------------------------------
    // Foreign keys
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_ForeignKeyRemoved_EmitsRemoveConstraint()
    {
        var fk = ForeignKey.Create("orders_user_fk", ["user_id"], "app", "users", ["id"]);
        var table = DiffTable(
            Table.Create("orders", columns: [Column.Create("user_id", SqlType.Int)], foreignKeys: [fk]),
            Table.Create("orders", columns: [Column.Create("user_id", SqlType.Int)]));

        var constraint = table!.Constraints.ShouldHaveSingleItem();
        constraint.Kind.ShouldBe(ChangeKind.Remove);
        constraint.Type.ShouldBe(ConstraintType.ForeignKey);
    }

    [Fact]
    public void Compare_ForeignKeyDefinitionChanged_EmitsRemoveAndAdd()
    {
        var table = DiffTable(
            Table.Create("orders", columns: [Column.Create("user_id", SqlType.Int)],
                foreignKeys: [ForeignKey.Create("orders_user_fk", ["user_id"], "app", "users", ["id"])]),
            Table.Create("orders", columns: [Column.Create("user_id", SqlType.Int)],
                foreignKeys: [ForeignKey.Create("orders_user_fk", ["user_id"], "app", "users", ["id"], onDelete: ReferentialAction.Cascade)]));

        table!.Constraints.Select(c => c.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    // -------------------------------------------------------------------------
    // Indexes
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_IndexRemoved_EmitsRemove()
    {
        var table = DiffTable(
            Table.Create("users", columns: [Column.Create("email", SqlType.Text)], indexes: [TableIndex.Create("users_email_ix", ["email"])]),
            Table.Create("users", columns: [Column.Create("email", SqlType.Text)]));

        table!.Indexes.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_IndexDefinitionChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            Table.Create("users", columns: [Column.Create("email", SqlType.Text)], indexes: [TableIndex.Create("users_email_ix", ["email"])]),
            Table.Create("users", columns: [Column.Create("email", SqlType.Text)], indexes: [TableIndex.Create("users_email_ix", ["email"], isUnique: true)]));

        table!.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_IndexCommentOnlyChange_EmitsModify()
    {
        var table = DiffTable(
            Table.Create("users", columns: [Column.Create("email", SqlType.Text)], indexes: [TableIndex.Create("users_email_ix", ["email"], comment: "old")]),
            Table.Create("users", columns: [Column.Create("email", SqlType.Text)], indexes: [TableIndex.Create("users_email_ix", ["email"], comment: "new")]));

        var index = table!.Indexes.ShouldHaveSingleItem();
        index.Kind.ShouldBe(ChangeKind.Modify);
        index.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    // -------------------------------------------------------------------------
    // Table grants
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_TableGrantRevoked_EmitsRemove()
    {
        var table = DiffTable(
            Table.Create("users", columns: [Column.Create("id", SqlType.Int)], grants: [new TableGrant("reader", TablePrivilege.Select)]),
            Table.Create("users", columns: [Column.Create("id", SqlType.Int)]));

        var grant = table!.Grants.ShouldHaveSingleItem();
        grant.Kind.ShouldBe(ChangeKind.Remove);
        grant.Role.ShouldBe("reader");
    }

    [Fact]
    public void Compare_TableGrantPrivilegesChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            Table.Create("users", columns: [Column.Create("id", SqlType.Int)], grants: [new TableGrant("reader", TablePrivilege.Select)]),
            Table.Create("users", columns: [Column.Create("id", SqlType.Int)], grants: [new TableGrant("reader", TablePrivilege.All)]));

        table!.Grants.Select(g => (g.Kind, g.Privileges)).ShouldBe(
            [(ChangeKind.Remove, TablePrivilege.Select), (ChangeKind.Add, TablePrivilege.All)]);
    }
}
