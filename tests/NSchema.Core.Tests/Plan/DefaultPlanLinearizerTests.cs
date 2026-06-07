using NSchema.Diff.Model;
using NSchema.Plan;
using NSchema.Plan.Model;
using NSchema.Schema.Model;

namespace NSchema.Tests.Plan;

/// <summary>
/// Exercises <see cref="DefaultPlanLinearizer"/> in isolation: structured <see cref="DatabaseDiff"/> nodes go in,
/// the emitted <see cref="MigrationAction"/>s come out. The comparer is deliberately not involved, so these tests pin
/// the diff-node → action mapping and the priority ordering that are the linearizer's sole responsibility.
/// </summary>
public sealed class DefaultPlanLinearizerTests
{
    private readonly DefaultPlanLinearizer _linearizer = new();

    private IReadOnlyList<MigrationAction> Linearize(params SchemaDiff[] schemas) => _linearizer.Linearize(new DatabaseDiff(schemas));

    // -- diff node builders ----------------------------------------------------

    private static SchemaDiff SchemaNode(
        string name,
        ChangeKind? kind = null,
        string? renamedFrom = null,
        ValueChange<string>? comment = null,
        IReadOnlyList<GrantChange>? grants = null,
        IReadOnlyList<TableDiff>? tables = null)
        => new(name, kind, renamedFrom, comment, grants ?? [], tables ?? []);

    private static TableDiff TableNode(
        string name,
        ChangeKind kind,
        string schema = "app",
        string? renamedFrom = null,
        ValueChange<string>? comment = null,
        IReadOnlyList<ColumnDiff>? columns = null,
        IReadOnlyList<GrantChange>? grants = null,
        IReadOnlyList<IndexDiff>? indexes = null,
        IReadOnlyList<ConstraintDiff>? constraints = null,
        Table? definition = null)
        => new(schema, name, kind, renamedFrom, comment, columns ?? [], grants ?? [], indexes ?? [], constraints ?? [], definition);

    private static ColumnDiff AddedColumn(Column definition, ValueChange<string>? comment = null)
        => new(definition.Name, ChangeKind.Add, definition, null, null, null, null, null, comment);

    private static ColumnDiff RemovedColumn(Column definition)
        => new(definition.Name, ChangeKind.Remove, definition, null, null, null, null, null, null);

    private static ColumnDiff ModifiedColumn(
        string name,
        string? renamedFrom = null,
        ValueChange<SqlType>? type = null,
        ValueChange<bool>? nullability = null,
        ValueChange<string>? @default = null,
        ValueChange<IdentityOptions>? identity = null,
        ValueChange<string>? comment = null)
        => new(name, ChangeKind.Modify, null, renamedFrom, type, nullability, @default, identity, comment);

    /// <summary>Wraps a single table under a null-kind <c>app</c> schema (the common "only tables changed" case).</summary>
    private IReadOnlyList<MigrationAction> LinearizeTable(TableDiff table) => Linearize(SchemaNode("app", tables: [table]));

    private static int IndexOf<T>(IReadOnlyList<MigrationAction> plan) where T : MigrationAction
    {
        for (var i = 0; i < plan.Count; i++)
        {
            if (plan[i] is T)
            {
                return i;
            }
        }

        return -1;
    }

    // -------------------------------------------------------------------------
    // Empty
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_EmptyDiff_ProducesNoActions()
        => Linearize().ShouldBeEmpty();

    // -------------------------------------------------------------------------
    // Schema nodes
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_AddSchema_EmitsCreateSchema()
        => Linearize(SchemaNode("app", ChangeKind.Add))
            .ShouldHaveSingleItem().ShouldBeOfType<CreateSchema>().SchemaName.ShouldBe("app");

    [Fact]
    public void Linearize_RemoveSchema_EmitsDropSchema()
        => Linearize(SchemaNode("app", ChangeKind.Remove))
            .ShouldHaveSingleItem().ShouldBeOfType<DropSchema>().SchemaName.ShouldBe("app");

    [Fact]
    public void Linearize_RemoveSchema_IgnoresNestedTablesAndGrants()
    {
        // The Remove branch drops the schema wholesale; nested content must not produce its own actions.
        var schema = SchemaNode("app", ChangeKind.Remove,
            grants: [new GrantChange(ChangeKind.Add, "reader", null)],
            tables: [TableNode("users", ChangeKind.Add, definition: Table.Create("users"))]);

        Linearize(schema).ShouldHaveSingleItem().ShouldBeOfType<DropSchema>();
    }

    [Fact]
    public void Linearize_RenamedSchema_EmitsRenameSchema_NotCreateOrDrop()
    {
        var plan = Linearize(SchemaNode("application", ChangeKind.Modify, renamedFrom: "app"));

        plan.ShouldHaveSingleItem().ShouldBeOfType<RenameSchema>()
            .ShouldSatisfyAllConditions(
                r => r.OldName.ShouldBe("app"),
                r => r.NewName.ShouldBe("application"));
    }

    [Fact]
    public void Linearize_NullKindSchema_EmitsNoSchemaAction_ButEmitsTables()
    {
        var plan = LinearizeTable(TableNode("users", ChangeKind.Remove));

        plan.Any(a => a is CreateSchema or DropSchema or RenameSchema).ShouldBeFalse();
        plan.ShouldHaveSingleItem().ShouldBeOfType<DropTable>().TableName.ShouldBe("users");
    }

    // -------------------------------------------------------------------------
    // Schema attributes
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_SchemaComment_EmitsSetSchemaComment()
    {
        var plan = Linearize(SchemaNode("app", ChangeKind.Modify, comment: new ValueChange<string>("old", "new")));

        plan.OfType<SetSchemaComment>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(
                c => c.OldComment.ShouldBe("old"),
                c => c.NewComment.ShouldBe("new"));
    }

    [Fact]
    public void Linearize_SchemaCommentOnNewSchema_EmitsSetSchemaComment()
        => Linearize(SchemaNode("app", ChangeKind.Add, comment: new ValueChange<string>(null, "created")))
            .OfType<SetSchemaComment>().ShouldHaveSingleItem().NewComment.ShouldBe("created");

    [Fact]
    public void Linearize_SchemaGrantAdd_EmitsGrantSchemaUsage()
        => Linearize(SchemaNode("app", ChangeKind.Modify, grants: [new GrantChange(ChangeKind.Add, "reader", null)]))
            .OfType<GrantSchemaUsage>().ShouldHaveSingleItem().Role.ShouldBe("reader");

    [Fact]
    public void Linearize_SchemaGrantRemove_EmitsRevokeSchemaUsage()
        => Linearize(SchemaNode("app", ChangeKind.Modify, grants: [new GrantChange(ChangeKind.Remove, "reader", null)]))
            .OfType<RevokeSchemaUsage>().ShouldHaveSingleItem().Role.ShouldBe("reader");

    // -------------------------------------------------------------------------
    // Table nodes
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_AddTable_EmitsCreateTableFromDefinition()
    {
        var plan = LinearizeTable(TableNode("users", ChangeKind.Add, definition: Table.Create("users")));

        plan.OfType<CreateTable>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(
                t => t.SchemaName.ShouldBe("app"),
                t => t.Table.Name.ShouldBe("users"));
    }

    [Fact]
    public void Linearize_RemoveTable_EmitsDropTable()
        => LinearizeTable(TableNode("users", ChangeKind.Remove))
            .ShouldHaveSingleItem().ShouldBeOfType<DropTable>().TableName.ShouldBe("users");

    [Fact]
    public void Linearize_RenamedTable_EmitsRenameTable_NotCreateOrDrop()
    {
        var plan = LinearizeTable(TableNode("accounts", ChangeKind.Modify, renamedFrom: "users"));

        plan.Any(a => a is CreateTable or DropTable).ShouldBeFalse();
        plan.ShouldHaveSingleItem().ShouldBeOfType<RenameTable>()
            .ShouldSatisfyAllConditions(r => r.OldName.ShouldBe("users"), r => r.NewName.ShouldBe("accounts"));
    }

    [Fact]
    public void Linearize_TableComment_EmitsSetTableComment()
        => LinearizeTable(TableNode("users", ChangeKind.Modify, comment: new ValueChange<string>(null, "accounts")))
            .OfType<SetTableComment>().ShouldHaveSingleItem().NewComment.ShouldBe("accounts");

    [Fact]
    public void Linearize_AddTable_DoesNotEmitAddColumn_ButFoldsColumnComments()
    {
        // Columns of a new table are created inline by CREATE TABLE; only their comments arrive as separate actions.
        var table = TableNode("users", ChangeKind.Add,
            definition: Table.Create("users", columns: [Column.Create("id", SqlType.Int, comment: "pk")]),
            columns: [AddedColumn(Column.Create("id", SqlType.Int), comment: new ValueChange<string>(null, "pk"))]);

        var plan = LinearizeTable(table);

        plan.OfType<AddColumn>().ShouldBeEmpty();
        plan.OfType<CreateTable>().ShouldHaveSingleItem();
        plan.OfType<SetColumnComment>().ShouldHaveSingleItem().NewComment.ShouldBe("pk");
    }

    // -------------------------------------------------------------------------
    // Column changes (within a modified table)
    // -------------------------------------------------------------------------

    private IReadOnlyList<MigrationAction> LinearizeColumn(ColumnDiff column)
        => LinearizeTable(TableNode("users", ChangeKind.Modify, columns: [column]));

    [Fact]
    public void Linearize_AddColumn_EmitsAddColumnAndComment()
    {
        var plan = LinearizeColumn(AddedColumn(Column.Create("email", SqlType.Text), comment: new ValueChange<string>(null, "contact")));

        plan.OfType<AddColumn>().ShouldHaveSingleItem().Column.Name.ShouldBe("email");
        plan.OfType<SetColumnComment>().ShouldHaveSingleItem().NewComment.ShouldBe("contact");
    }

    [Fact]
    public void Linearize_RemoveColumn_EmitsDropColumn()
        => LinearizeColumn(RemovedColumn(Column.Create("email", SqlType.Text)))
            .OfType<DropColumn>().ShouldHaveSingleItem().ColumnName.ShouldBe("email");

    [Fact]
    public void Linearize_RenameColumn_EmitsRenameColumn()
        => LinearizeColumn(ModifiedColumn("email_address", renamedFrom: "email"))
            .OfType<RenameColumn>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(r => r.OldName.ShouldBe("email"), r => r.NewName.ShouldBe("email_address"));

    [Fact]
    public void Linearize_ColumnTypeChange_EmitsAlterColumnType()
        => LinearizeColumn(ModifiedColumn("id", type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt)))
            .OfType<AlterColumnType>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(a => a.OldType.ShouldBe(SqlType.Int), a => a.NewType.ShouldBe(SqlType.BigInt));

    [Fact]
    public void Linearize_ColumnNullabilityChange_EmitsAlterColumnNullability()
        => LinearizeColumn(ModifiedColumn("email", nullability: new ValueChange<bool>(true, false)))
            .OfType<AlterColumnNullability>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(a => a.OldNullable.ShouldBe(true), a => a.NewNullable.ShouldBe(false));

    [Fact]
    public void Linearize_ColumnDefaultChange_EmitsSetColumnDefault()
        => LinearizeColumn(ModifiedColumn("status", @default: new ValueChange<string>(null, "'active'")))
            .OfType<SetColumnDefault>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(a => a.OldDefault.ShouldBeNull(), a => a.NewDefault.ShouldBe("'active'"));

    [Fact]
    public void Linearize_ColumnIdentityChange_EmitsAlterIdentitySequence()
    {
        var identity = new ValueChange<IdentityOptions>(null, new IdentityOptions(1, 1, 1));

        LinearizeColumn(ModifiedColumn("id", identity: identity))
            .OfType<AlterIdentitySequence>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(a => a.OldOptions.ShouldBeNull(), a => a.NewOptions.ShouldBe(new IdentityOptions(1, 1, 1)));
    }

    [Fact]
    public void Linearize_ColumnComment_EmitsSetColumnComment()
        => LinearizeColumn(ModifiedColumn("id", comment: new ValueChange<string>("old", "new")))
            .OfType<SetColumnComment>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(c => c.OldComment.ShouldBe("old"), c => c.NewComment.ShouldBe("new"));

    [Fact]
    public void Linearize_ColumnWithEveryModification_EmitsAllActions()
    {
        var column = ModifiedColumn("id",
            renamedFrom: "identifier",
            type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt),
            nullability: new ValueChange<bool>(true, false),
            @default: new ValueChange<string>(null, "0"),
            identity: new ValueChange<IdentityOptions>(null, new IdentityOptions(1, 1, 1)),
            comment: new ValueChange<string>(null, "pk"));

        var actions = LinearizeColumn(column);

        actions.OfType<RenameColumn>().ShouldHaveSingleItem();
        actions.OfType<AlterColumnType>().ShouldHaveSingleItem();
        actions.OfType<AlterColumnNullability>().ShouldHaveSingleItem();
        actions.OfType<SetColumnDefault>().ShouldHaveSingleItem();
        actions.OfType<AlterIdentitySequence>().ShouldHaveSingleItem();
        actions.OfType<SetColumnComment>().ShouldHaveSingleItem();
    }

    // -------------------------------------------------------------------------
    // Constraints, indexes, grants
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_AddPrimaryKey_EmitsAddPrimaryKey()
    {
        var pk = new PrimaryKey("users_pkey", ["id"]);
        var constraint = new ConstraintDiff(ChangeKind.Add, ConstraintType.PrimaryKey, "users_pkey", pk, null);

        LinearizeTable(TableNode("users", ChangeKind.Modify, constraints: [constraint]))
            .OfType<AddPrimaryKey>().ShouldHaveSingleItem().PrimaryKey.Name.ShouldBe("users_pkey");
    }

    [Fact]
    public void Linearize_RemovePrimaryKey_EmitsDropPrimaryKey()
    {
        var constraint = new ConstraintDiff(ChangeKind.Remove, ConstraintType.PrimaryKey, "users_pkey", null, null);

        LinearizeTable(TableNode("users", ChangeKind.Modify, constraints: [constraint]))
            .OfType<DropPrimaryKey>().ShouldHaveSingleItem().PrimaryKeyName.ShouldBe("users_pkey");
    }

    [Fact]
    public void Linearize_AddForeignKey_EmitsAddForeignKey()
    {
        var fk = ForeignKey.Create("orders_user_fk", ["user_id"], "app", "users", ["id"]);
        var constraint = new ConstraintDiff(ChangeKind.Add, ConstraintType.ForeignKey, "orders_user_fk", null, fk);

        LinearizeTable(TableNode("orders", ChangeKind.Modify, constraints: [constraint]))
            .OfType<AddForeignKey>().ShouldHaveSingleItem().ForeignKey.Name.ShouldBe("orders_user_fk");
    }

    [Fact]
    public void Linearize_RemoveForeignKey_EmitsDropForeignKey()
    {
        var constraint = new ConstraintDiff(ChangeKind.Remove, ConstraintType.ForeignKey, "orders_user_fk", null, null);

        LinearizeTable(TableNode("orders", ChangeKind.Modify, constraints: [constraint]))
            .OfType<DropForeignKey>().ShouldHaveSingleItem().ForeignKeyName.ShouldBe("orders_user_fk");
    }

    [Fact]
    public void Linearize_AddIndex_EmitsCreateIndex()
    {
        var index = new IndexDiff(ChangeKind.Add, "users_email_ix", TableIndex.Create("users_email_ix", ["email"]), null);

        LinearizeTable(TableNode("users", ChangeKind.Modify, indexes: [index]))
            .OfType<CreateIndex>().ShouldHaveSingleItem().Index.Name.ShouldBe("users_email_ix");
    }

    [Fact]
    public void Linearize_RemoveIndex_EmitsDropIndex()
    {
        var index = new IndexDiff(ChangeKind.Remove, "users_email_ix", null, null);

        LinearizeTable(TableNode("users", ChangeKind.Modify, indexes: [index]))
            .OfType<DropIndex>().ShouldHaveSingleItem().IndexName.ShouldBe("users_email_ix");
    }

    [Fact]
    public void Linearize_ModifyIndexComment_EmitsSetIndexComment()
    {
        var index = new IndexDiff(ChangeKind.Modify, "users_email_ix", null, new ValueChange<string>("old", "new"));

        LinearizeTable(TableNode("users", ChangeKind.Modify, indexes: [index]))
            .OfType<SetIndexComment>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(c => c.OldComment.ShouldBe("old"), c => c.NewComment.ShouldBe("new"));
    }

    [Fact]
    public void Linearize_TableGrantAdd_EmitsGrantTablePrivileges()
    {
        var grant = new GrantChange(ChangeKind.Add, "reader", TablePrivilege.Select);

        LinearizeTable(TableNode("users", ChangeKind.Modify, grants: [grant]))
            .OfType<GrantTablePrivileges>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(g => g.Role.ShouldBe("reader"), g => g.Privileges.ShouldBe(TablePrivilege.Select));
    }

    [Fact]
    public void Linearize_TableGrantRemove_EmitsRevokeTablePrivileges()
    {
        var grant = new GrantChange(ChangeKind.Remove, "reader", TablePrivilege.Select);

        LinearizeTable(TableNode("users", ChangeKind.Modify, grants: [grant]))
            .OfType<RevokeTablePrivileges>().ShouldHaveSingleItem().Role.ShouldBe("reader");
    }

    // -------------------------------------------------------------------------
    // Ordering — the linearizer sorts every action into a safe dependency order.
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_OrdersCreateSchemaBeforeItsTables()
    {
        var plan = Linearize(SchemaNode("app", ChangeKind.Add,
            tables: [TableNode("users", ChangeKind.Add, definition: Table.Create("users"))]));

        IndexOf<CreateSchema>(plan).ShouldBeLessThan(IndexOf<CreateTable>(plan));
    }

    [Fact]
    public void Linearize_OrdersDropColumnBeforeAddColumn()
    {
        var plan = LinearizeTable(TableNode("users", ChangeKind.Modify,
            columns: [AddedColumn(Column.Create("new_col", SqlType.Text)), RemovedColumn(Column.Create("old_col", SqlType.Text))]));

        IndexOf<DropColumn>(plan).ShouldBeLessThan(IndexOf<AddColumn>(plan));
    }

    [Fact]
    public void Linearize_OrdersAddColumnBeforeAddPrimaryKey()
    {
        var plan = LinearizeTable(TableNode("users", ChangeKind.Modify,
            columns: [AddedColumn(Column.Create("id", SqlType.Int))],
            constraints: [new ConstraintDiff(ChangeKind.Add, ConstraintType.PrimaryKey, "users_pkey", new PrimaryKey("users_pkey", ["id"]), null)]));

        IndexOf<AddColumn>(plan).ShouldBeLessThan(IndexOf<AddPrimaryKey>(plan));
    }

    [Fact]
    public void Linearize_OrdersConstraintRemovalBeforeAddition_WhenReplacingAPrimaryKey()
    {
        var plan = LinearizeTable(TableNode("users", ChangeKind.Modify, constraints:
        [
            new ConstraintDiff(ChangeKind.Remove, ConstraintType.PrimaryKey, "users_pkey", null, null),
            new ConstraintDiff(ChangeKind.Add, ConstraintType.PrimaryKey, "users_pkey", new PrimaryKey("users_pkey", ["id", "tenant"]), null),
        ]));

        IndexOf<DropPrimaryKey>(plan).ShouldBeLessThan(IndexOf<AddPrimaryKey>(plan));
    }

    [Fact]
    public void Linearize_OrdersDropTableAndDropSchemaLast()
    {
        var plan = Linearize(
            SchemaNode("new_app", ChangeKind.Add, tables: [TableNode("users", ChangeKind.Add, schema: "new_app", definition: Table.Create("users"))]),
            SchemaNode("old_app", ChangeKind.Remove),
            SchemaNode("app", tables: [TableNode("stale", ChangeKind.Remove)]));

        // Destructive table/schema drops run after every constructive action.
        IndexOf<CreateSchema>(plan).ShouldBeLessThan(IndexOf<DropTable>(plan));
        IndexOf<CreateTable>(plan).ShouldBeLessThan(IndexOf<DropTable>(plan));
        IndexOf<DropTable>(plan).ShouldBeLessThan(IndexOf<DropSchema>(plan));
    }

    [Fact]
    public void Linearize_OrdersDropForeignKeyBeforeDropTable()
    {
        var plan = Linearize(SchemaNode("app", tables:
        [
            TableNode("orders", ChangeKind.Modify, constraints: [new ConstraintDiff(ChangeKind.Remove, ConstraintType.ForeignKey, "orders_user_fk", null, null)]),
            TableNode("users", ChangeKind.Remove),
        ]));

        IndexOf<DropForeignKey>(plan).ShouldBeLessThan(IndexOf<DropTable>(plan));
    }
}
