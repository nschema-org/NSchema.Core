using NSchema.Diff.Domain.Models.Columns;
using NSchema.Diff.Domain.Models.Constraints;
using NSchema.Diff.Domain.Models.Enums;
using NSchema.Diff.Domain.Models.Indexes;
using NSchema.Diff.Domain.Models.Routines;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models.Sequences;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Diff.Domain.Models.Triggers;
using NSchema.Diff.Domain.Models.Views;
using NSchema.Diff.Domain.Models;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Models;
using NSchema.Plan.Domain.Models.Columns;
using NSchema.Plan.Domain.Models.Constraints;
using NSchema.Plan.Domain.Models.Enums;
using NSchema.Plan.Domain.Models.Indexes;
using NSchema.Plan.Domain.Models.Routines;
using NSchema.Plan.Domain.Models.Schemas;
using NSchema.Plan.Domain.Models.Sequences;
using NSchema.Plan.Domain.Models.Tables;
using NSchema.Plan.Domain.Models.Triggers;
using NSchema.Plan.Domain.Models.Views;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Constraints;
using NSchema.Project.Domain.Models.Enums;
using NSchema.Project.Domain.Models.Indexes;
using NSchema.Project.Domain.Models.Routines;
using NSchema.Project.Domain.Models.Sequences;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Views;

namespace NSchema.Tests.Plan;

/// <summary>
/// Exercises <see cref="PlanLinearizer"/> in isolation: structured <see cref="DatabaseDiff"/> nodes go in,
/// the emitted <see cref="MigrationAction"/>s come out. The comparer is deliberately not involved, so these tests pin
/// the diff-node → action mapping and the priority ordering that are the linearizer's sole responsibility.
/// </summary>
public sealed class PlanLinearizerTests
{
    private readonly PlanLinearizer _linearizer = new();

    private IReadOnlyList<MigrationAction> Linearize(params SchemaDiff[] schemas) => _linearizer.Linearize(new DatabaseDiff(schemas));

    // -- diff node builders ----------------------------------------------------

    private static SchemaDiff SchemaNode(
        string name,
        ChangeKind? kind = null,
        string? renamedFrom = null,
        ValueChange<string>? comment = null,
        IReadOnlyList<GrantChange>? grants = null,
        IReadOnlyList<TableDiff>? tables = null,
        IReadOnlyList<ViewDiff>? views = null,
        IReadOnlyList<EnumDiff>? enums = null,
        IReadOnlyList<SequenceDiff>? sequences = null,
        IReadOnlyList<RoutineDiff>? routines = null
    )
        => new(name, kind, renamedFrom, comment, grants ?? [], tables ?? [], views ?? [], enums ?? [], sequences ?? [],
            routines ?? []);

    private static TableDiff TableNode(
        string name,
        ChangeKind kind,
        string schema = "app",
        string? renamedFrom = null,
        ValueChange<string>? comment = null,
        IReadOnlyList<ColumnDiff>? columns = null,
        IReadOnlyList<GrantChange>? grants = null,
        IReadOnlyList<IndexDiff>? indexes = null,
        IReadOnlyList<PrimaryKeyDiff>? primaryKey = null,
        IReadOnlyList<ForeignKeyDiff>? foreignKeys = null,
        IReadOnlyList<UniqueConstraintDiff>? uniqueConstraints = null,
        IReadOnlyList<CheckConstraintDiff>? checks = null,
        IReadOnlyList<ExclusionConstraintDiff>? exclusionConstraints = null,
        IReadOnlyList<TriggerDiff>? triggers = null,
        Table? definition = null)
        => new(schema, name, kind, renamedFrom, comment, columns ?? [], grants ?? [], indexes ?? [],
            primaryKey ?? [], foreignKeys ?? [], uniqueConstraints ?? [], checks ?? [], exclusionConstraints ?? [], triggers ?? [], definition);

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
        ValueChange<string>? comment = null,
        ValueChange<string>? generated = null,
        Column? definition = null)
        => new(name, ChangeKind.Modify, definition, renamedFrom, type, nullability, @default, identity, comment, generated);

    private static ViewDiff AddView(string name, string schema = "app", params (string Schema, string Name)[] dependsOn)
    {
        var deps = dependsOn.Select(d => new ViewDependency(d.Schema, d.Name)).ToList();
        var view = new View(name, $"SELECT * FROM source_of_{name}", DependsOn: deps);
        return new ViewDiff(schema, name, ChangeKind.Add, Definition: view, DependsOn: deps);
    }

    private static ViewDiff RemoveView(string name, string schema = "app", params (string Schema, string Name)[] dependsOn)
        => new(schema, name, ChangeKind.Remove, DependsOn: dependsOn.Select(d => new ViewDependency(d.Schema, d.Name)).ToList());

    private static TableDiff AddTable(string name, string schema = "app")
        => new(schema, name, ChangeKind.Add, Definition: new Table(name));

    private static int IndexOfCreateView(IReadOnlyList<MigrationAction> plan, string name)
        => plan.ToList().FindIndex(a => a is CreateView v && v.View.Name == name);

    private static int IndexOfDropView(IReadOnlyList<MigrationAction> plan, string name)
        => plan.ToList().FindIndex(a => a is DropView v && v.ViewName == name);

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
    public void Linearize_RemoveSchema_DropsNestedObjectsBeforeTheSchema()
    {
        // A removed schema drops its contained objects first (rather than relying on a provider-specific
        // DROP SCHEMA CASCADE), then the schema itself. The type-sort orders the table drop ahead of the schema drop.
        var schema = SchemaNode("app", ChangeKind.Remove,
            tables: [TableNode("users", ChangeKind.Remove)]);

        var plan = Linearize(schema);

        plan.Count.ShouldBe(2);
        plan[0].ShouldBeOfType<DropTable>().TableName.ShouldBe("users");
        plan[1].ShouldBeOfType<DropSchema>().SchemaName.ShouldBe("app");
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
        var plan = LinearizeTable(TableNode("users", ChangeKind.Add, definition: new Table("users")));

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
    public void Linearize_RenamedTable_DropsTargetOldName_AndPrecedeRename()
    {
        // Drops and revokes sort before RenameTable, so they execute while the table still carries its old name.
        var table = TableNode("accounts", ChangeKind.Modify, renamedFrom: "users",
            primaryKey: [new PrimaryKeyDiff(ChangeKind.Remove, "users_pkey")],
            foreignKeys: [new ForeignKeyDiff(ChangeKind.Remove, "users_org_fk")],
            uniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Remove, "users_email_uq")],
            checks: [new CheckConstraintDiff(ChangeKind.Remove, "users_age_chk")],
            exclusionConstraints: [new ExclusionConstraintDiff(ChangeKind.Remove, "no_overlap")],
            indexes: [new IndexDiff(ChangeKind.Remove, "users_email_ix")],
            triggers: [new TriggerDiff(ChangeKind.Remove, "users_audit_trg")],
            grants: [new GrantChange(ChangeKind.Remove, "reader", TablePrivilege.Select)]);

        var plan = LinearizeTable(table);

        plan.OfType<DropPrimaryKey>().ShouldHaveSingleItem().TableName.ShouldBe("users");
        plan.OfType<DropForeignKey>().ShouldHaveSingleItem().TableName.ShouldBe("users");
        plan.OfType<DropUniqueConstraint>().ShouldHaveSingleItem().TableName.ShouldBe("users");
        plan.OfType<DropCheckConstraint>().ShouldHaveSingleItem().TableName.ShouldBe("users");
        plan.OfType<DropExclusionConstraint>().ShouldHaveSingleItem().TableName.ShouldBe("users");
        plan.OfType<DropIndex>().ShouldHaveSingleItem().TableName.ShouldBe("users");
        plan.OfType<DropTrigger>().ShouldHaveSingleItem().TableName.ShouldBe("users");
        plan.OfType<RevokeTablePrivileges>().ShouldHaveSingleItem().TableName.ShouldBe("users");
        plan[^1].ShouldBeOfType<RenameTable>();
    }

    [Fact]
    public void Linearize_RenamedSchema_RenamePrecedesChildDrops()
    {
        // Child diff nodes carry the new schema name, so the schema rename must run before their drops for the
        // schema-qualified names to resolve.
        var table = TableNode("orders", ChangeKind.Modify, schema: "sales",
            foreignKeys: [new ForeignKeyDiff(ChangeKind.Remove, "orders_user_fk")],
            triggers: [new TriggerDiff(ChangeKind.Remove, "orders_audit_trg")]);
        var plan = Linearize(SchemaNode("sales", ChangeKind.Modify, renamedFrom: "shop",
            grants: [new GrantChange(ChangeKind.Remove, "reader")],
            tables: [table]));

        IndexOf<RenameSchema>(plan).ShouldBe(0);
        plan.OfType<DropForeignKey>().ShouldHaveSingleItem().SchemaName.ShouldBe("sales");
        plan.OfType<DropTrigger>().ShouldHaveSingleItem().SchemaName.ShouldBe("sales");
        plan.OfType<RevokeSchemaUsage>().ShouldHaveSingleItem().SchemaName.ShouldBe("sales");
    }

    [Fact]
    public void Linearize_RenamedTable_AddsTargetNewName()
    {
        var table = TableNode("accounts", ChangeKind.Modify, renamedFrom: "users",
            uniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Add, "accounts_email_uq", new UniqueConstraint("accounts_email_uq", ["email"]))],
            indexes: [new IndexDiff(ChangeKind.Add, "accounts_email_ix", new TableIndex("accounts_email_ix", ["email"]))]);

        var plan = LinearizeTable(table);

        plan.OfType<AddUniqueConstraint>().ShouldHaveSingleItem().TableName.ShouldBe("accounts");
        plan.OfType<CreateIndex>().ShouldHaveSingleItem().TableName.ShouldBe("accounts");
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
            definition: new Table("users", Columns: [new Column("id", SqlType.Int, Comment: "pk")]),
            columns: [AddedColumn(new Column("id", SqlType.Int), comment: new ValueChange<string>(null, "pk"))]);

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
        var plan = LinearizeColumn(AddedColumn(new Column("email", SqlType.Text), comment: new ValueChange<string>(null, "contact")));

        plan.OfType<AddColumn>().ShouldHaveSingleItem().Column.Name.ShouldBe("email");
        plan.OfType<SetColumnComment>().ShouldHaveSingleItem().NewComment.ShouldBe("contact");
    }

    [Fact]
    public void Linearize_RemoveColumn_EmitsDropColumn()
        => LinearizeColumn(RemovedColumn(new Column("email", SqlType.Text)))
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
    public void Linearize_ColumnTypeChange_CarriesFinalNullabilityFromDefinition()
        // A dialect whose ALTER COLUMN restates the whole column (SQL Server) needs the unchanged nullability; it
        // rides along on the action from the desired column's Definition.
        => LinearizeColumn(ModifiedColumn("id", type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt),
                definition: new Column("id", SqlType.BigInt, IsNullable: false)))
            .OfType<AlterColumnType>().ShouldHaveSingleItem()
            .IsNullable.ShouldBe(false);

    [Fact]
    public void Linearize_ColumnNullabilityChange_CarriesFinalTypeFromDefinition()
        => LinearizeColumn(ModifiedColumn("email", nullability: new ValueChange<bool>(true, false),
                definition: new Column("email", SqlType.VarChar(255), IsNullable: false)))
            .OfType<AlterColumnNullability>().ShouldHaveSingleItem()
            .ColumnType.ShouldBe(SqlType.VarChar(255));

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
    public void Linearize_ColumnGenerationChange_EmitsSetColumnGenerated()
        => LinearizeColumn(ModifiedColumn("area", generated: new ValueChange<string>(null, "w * h")))
            .OfType<SetColumnGenerated>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(a => a.OldExpression.ShouldBeNull(), a => a.NewExpression.ShouldBe("w * h"));

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
        var constraint = new PrimaryKeyDiff(ChangeKind.Add, "users_pkey", pk);

        LinearizeTable(TableNode("users", ChangeKind.Modify, primaryKey: [constraint]))
            .OfType<AddPrimaryKey>().ShouldHaveSingleItem().PrimaryKey.Name.ShouldBe("users_pkey");
    }

    [Fact]
    public void Linearize_RemovePrimaryKey_EmitsDropPrimaryKey()
    {
        var constraint = new PrimaryKeyDiff(ChangeKind.Remove, "users_pkey", null);

        LinearizeTable(TableNode("users", ChangeKind.Modify, primaryKey: [constraint]))
            .OfType<DropPrimaryKey>().ShouldHaveSingleItem().PrimaryKeyName.ShouldBe("users_pkey");
    }

    [Fact]
    public void Linearize_AddForeignKey_EmitsAddForeignKey()
    {
        var fk = new ForeignKey("orders_user_fk", ["user_id"], "app", "users", ["id"]);
        var constraint = new ForeignKeyDiff(ChangeKind.Add, "orders_user_fk", fk);

        LinearizeTable(TableNode("orders", ChangeKind.Modify, foreignKeys: [constraint]))
            .OfType<AddForeignKey>().ShouldHaveSingleItem().ForeignKey.Name.ShouldBe("orders_user_fk");
    }

    [Fact]
    public void Linearize_RemoveForeignKey_EmitsDropForeignKey()
    {
        var constraint = new ForeignKeyDiff(ChangeKind.Remove, "orders_user_fk", null);

        LinearizeTable(TableNode("orders", ChangeKind.Modify, foreignKeys: [constraint]))
            .OfType<DropForeignKey>().ShouldHaveSingleItem().ForeignKeyName.ShouldBe("orders_user_fk");
    }

    [Fact]
    public void Linearize_AddExclusionConstraint_EmitsAddExclusionConstraint()
    {
        var exclusion = new ExclusionConstraint("no_overlap", [new ExclusionElement("during", "&&")], "gist");
        var constraint = new ExclusionConstraintDiff(ChangeKind.Add, "no_overlap", exclusion);

        LinearizeTable(TableNode("bookings", ChangeKind.Modify, exclusionConstraints: [constraint]))
            .OfType<AddExclusionConstraint>().ShouldHaveSingleItem().ExclusionConstraint.Name.ShouldBe("no_overlap");
    }

    [Fact]
    public void Linearize_RemoveExclusionConstraint_EmitsDropExclusionConstraint()
    {
        var constraint = new ExclusionConstraintDiff(ChangeKind.Remove, "no_overlap", null);

        LinearizeTable(TableNode("bookings", ChangeKind.Modify, exclusionConstraints: [constraint]))
            .OfType<DropExclusionConstraint>().ShouldHaveSingleItem().ConstraintName.ShouldBe("no_overlap");
    }

    [Fact]
    public void Linearize_AddUniqueConstraint_EmitsAddUniqueConstraint()
    {
        var unique = new UniqueConstraint("users_email_uq", ["email"]);
        var constraint = new UniqueConstraintDiff(ChangeKind.Add, "users_email_uq", unique);

        LinearizeTable(TableNode("users", ChangeKind.Modify, uniqueConstraints: [constraint]))
            .OfType<AddUniqueConstraint>().ShouldHaveSingleItem().UniqueConstraint.Name.ShouldBe("users_email_uq");
    }

    [Fact]
    public void Linearize_RemoveUniqueConstraint_EmitsDropUniqueConstraint()
    {
        var constraint = new UniqueConstraintDiff(ChangeKind.Remove, "users_email_uq", null);

        LinearizeTable(TableNode("users", ChangeKind.Modify, uniqueConstraints: [constraint]))
            .OfType<DropUniqueConstraint>().ShouldHaveSingleItem().ConstraintName.ShouldBe("users_email_uq");
    }

    [Fact]
    public void Linearize_AddCheckConstraint_EmitsAddCheckConstraint()
    {
        var check = new CheckConstraint("users_age_chk", "age >= 0");
        var constraint = new CheckConstraintDiff(ChangeKind.Add, "users_age_chk", check);

        LinearizeTable(TableNode("users", ChangeKind.Modify, checks: [constraint]))
            .OfType<AddCheckConstraint>().ShouldHaveSingleItem().CheckConstraint.Name.ShouldBe("users_age_chk");
    }

    [Fact]
    public void Linearize_RemoveCheckConstraint_EmitsDropCheckConstraint()
    {
        var constraint = new CheckConstraintDiff(ChangeKind.Remove, "users_age_chk", null);

        LinearizeTable(TableNode("users", ChangeKind.Modify, checks: [constraint]))
            .OfType<DropCheckConstraint>().ShouldHaveSingleItem().ConstraintName.ShouldBe("users_age_chk");
    }

    [Fact]
    public void Linearize_UniqueConstraintCommentChange_EmitsSetConstraintComment()
    {
        var constraint = new UniqueConstraintDiff(ChangeKind.Modify, "users_email_uq", null, new ValueChange<string>("old", "new"));

        var action = LinearizeTable(TableNode("users", ChangeKind.Modify, uniqueConstraints: [constraint]))
            .OfType<SetConstraintComment>().ShouldHaveSingleItem();
        action.ConstraintName.ShouldBe("users_email_uq");
        action.OldComment.ShouldBe("old");
        action.NewComment.ShouldBe("new");
    }

    [Fact]
    public void Linearize_PrimaryKeyCommentChange_EmitsSetConstraintComment()
    {
        var constraint = new PrimaryKeyDiff(ChangeKind.Modify, "users_pkey", null, new ValueChange<string>(null, "surrogate key"));

        LinearizeTable(TableNode("users", ChangeKind.Modify, primaryKey: [constraint]))
            .OfType<SetConstraintComment>().ShouldHaveSingleItem().NewComment.ShouldBe("surrogate key");
    }

    [Fact]
    public void Linearize_AddIndex_EmitsCreateIndex()
    {
        var index = new IndexDiff(ChangeKind.Add, "users_email_ix", new TableIndex("users_email_ix", ["email"]), null);

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
            tables: [TableNode("users", ChangeKind.Add, definition: new Table("users"))]));

        IndexOf<CreateSchema>(plan).ShouldBeLessThan(IndexOf<CreateTable>(plan));
    }

    [Fact]
    public void Linearize_OrdersDropColumnBeforeAddColumn()
    {
        var plan = LinearizeTable(TableNode("users", ChangeKind.Modify,
            columns: [AddedColumn(new Column("new_col", SqlType.Text)), RemovedColumn(new Column("old_col", SqlType.Text))]));

        IndexOf<DropColumn>(plan).ShouldBeLessThan(IndexOf<AddColumn>(plan));
    }

    [Fact]
    public void Linearize_OrdersAddColumnBeforeAddPrimaryKey()
    {
        var plan = LinearizeTable(TableNode("users", ChangeKind.Modify,
            columns: [AddedColumn(new Column("id", SqlType.Int))],
            primaryKey: [new PrimaryKeyDiff(ChangeKind.Add, "users_pkey", new PrimaryKey("users_pkey", ["id"]))]));

        IndexOf<AddColumn>(plan).ShouldBeLessThan(IndexOf<AddPrimaryKey>(plan));
    }

    [Fact]
    public void Linearize_OrdersConstraintRemovalBeforeAddition_WhenReplacingAPrimaryKey()
    {
        var plan = LinearizeTable(TableNode("users", ChangeKind.Modify, primaryKey:
        [
            new PrimaryKeyDiff(ChangeKind.Remove, "users_pkey", null),
            new PrimaryKeyDiff(ChangeKind.Add, "users_pkey", new PrimaryKey("users_pkey", ["id", "tenant"])),
        ]));

        IndexOf<DropPrimaryKey>(plan).ShouldBeLessThan(IndexOf<AddPrimaryKey>(plan));
    }

    [Fact]
    public void Linearize_OrdersDropTableAndDropSchemaLast()
    {
        var plan = Linearize(
            SchemaNode("new_app", ChangeKind.Add, tables: [TableNode("users", ChangeKind.Add, schema: "new_app", definition: new Table("users"))]),
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
            TableNode("orders", ChangeKind.Modify, foreignKeys: [new ForeignKeyDiff(ChangeKind.Remove, "orders_user_fk", null)]),
            TableNode("users", ChangeKind.Remove),
        ]));

        IndexOf<DropForeignKey>(plan).ShouldBeLessThan(IndexOf<DropTable>(plan));
    }

    [Fact]
    public void Linearize_OrdersAddUniqueConstraintBeforeAddForeignKey()
    {
        // A foreign key may target a unique constraint, so the constraint must be created first.
        var plan = LinearizeTable(TableNode("orders", ChangeKind.Modify,
            uniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Add, "orders_code_uq", new UniqueConstraint("orders_code_uq", ["code"]))],
            foreignKeys: [new ForeignKeyDiff(ChangeKind.Add, "orders_user_fk", new ForeignKey("orders_user_fk", ["user_id"], "app", "users", ["id"]))]));

        IndexOf<AddUniqueConstraint>(plan).ShouldBeLessThan(IndexOf<AddForeignKey>(plan));
    }

    [Fact]
    public void Linearize_OrdersDropForeignKeyBeforeDropUniqueConstraint()
    {
        // The mirror of the add ordering: a referencing foreign key is dropped before the constraint it targets.
        var plan = LinearizeTable(TableNode("orders", ChangeKind.Modify,
            foreignKeys: [new ForeignKeyDiff(ChangeKind.Remove, "orders_user_fk", null)],
            uniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Remove, "orders_code_uq", null)]));

        IndexOf<DropForeignKey>(plan).ShouldBeLessThan(IndexOf<DropUniqueConstraint>(plan));
    }

    // -------------------------------------------------------------------------
    // Views — the dependency-aware ordering layered on the fixed type order
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_CreatesViewAfterTheViewItReads_DespiteName()
    {
        // "a_top" reads "z_base"; alphabetically a_top sorts first, but it must be created second.
        var plan = Linearize(SchemaNode("app", views: [AddView("a_top", "app", ("app", "z_base")), AddView("z_base")]));

        IndexOfCreateView(plan, "z_base").ShouldBeLessThan(IndexOfCreateView(plan, "a_top"));
    }

    [Fact]
    public void Linearize_CreatesViewsInTransitiveDependencyOrder()
    {
        // c -> b -> a
        var plan = Linearize(SchemaNode("app", views:
            [AddView("c", "app", ("app", "b")), AddView("b", "app", ("app", "a")), AddView("a")]));

        IndexOfCreateView(plan, "a").ShouldBeLessThan(IndexOfCreateView(plan, "b"));
        IndexOfCreateView(plan, "b").ShouldBeLessThan(IndexOfCreateView(plan, "c"));
    }

    [Fact]
    public void Linearize_DropsDependentViewBeforeItsDependency()
    {
        // a_top reads z_base; dropping must remove a_top first (the reverse of create order).
        var plan = Linearize(SchemaNode("app", views: [RemoveView("a_top", "app", ("app", "z_base")), RemoveView("z_base")]));

        IndexOfDropView(plan, "a_top").ShouldBeLessThan(IndexOfDropView(plan, "z_base"));
    }

    [Fact]
    public void Linearize_OrdersViewDependenciesAcrossSchemas()
    {
        // A view in "reporting" reads a view in "core"; the core view must be created first.
        var plan = Linearize(
            SchemaNode("reporting", views: [AddView("summary", "reporting", ("core", "base"))]),
            SchemaNode("core", views: [AddView("base", "core")]));

        IndexOfCreateView(plan, "base").ShouldBeLessThan(IndexOfCreateView(plan, "summary"));
    }

    [Fact]
    public void Linearize_OrdersCreateViewAfterCreateTable()
    {
        var plan = Linearize(SchemaNode("app", tables: [AddTable("t")], views: [AddView("v", "app", ("app", "t"))]));

        IndexOf<CreateTable>(plan).ShouldBeLessThan(IndexOfCreateView(plan, "v"));
    }

    [Fact]
    public void Linearize_OrdersDropViewBeforeDropTable()
    {
        var plan = Linearize(SchemaNode("app",
            tables: [TableNode("t", ChangeKind.Remove)],
            views: [RemoveView("v", "app", ("app", "t"))]));

        IndexOfDropView(plan, "v").ShouldBeLessThan(IndexOf<DropTable>(plan));
    }

    [Fact]
    public void Linearize_EmitsRenameViewForRenamedView()
    {
        var plan = Linearize(SchemaNode("app", views: [new ViewDiff("app", "active", ChangeKind.Modify, RenamedFrom: "legacy")]));

        var rename = plan.OfType<RenameView>().ShouldHaveSingleItem();
        rename.OldName.ShouldBe("legacy");
        rename.NewName.ShouldBe("active");
        plan.OfType<CreateView>().ShouldBeEmpty(); // a rename-only change is not a replace
    }

    [Fact]
    public void Linearize_EmitsSetViewCommentForCommentChange()
    {
        var plan = Linearize(SchemaNode("app", views:
            [new ViewDiff("app", "active", ChangeKind.Modify, Comment: new ValueChange<string>("old", "new"))]));

        var comment = plan.OfType<SetViewComment>().ShouldHaveSingleItem();
        comment.ViewName.ShouldBe("active");
        comment.OldComment.ShouldBe("old");
        comment.NewComment.ShouldBe("new");
    }

    [Fact]
    public void Linearize_IndependentViews_KeepStableOrder()
    {
        var plan = Linearize(SchemaNode("app", views: [AddView("x"), AddView("y"), AddView("z")]));

        IndexOfCreateView(plan, "x").ShouldBeLessThan(IndexOfCreateView(plan, "y"));
        IndexOfCreateView(plan, "y").ShouldBeLessThan(IndexOfCreateView(plan, "z"));
    }

    // -------------------------------------------------------------------------
    // Enums
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_AddEnum_EmitsCreateEnumFromDefinition()
    {
        var plan = Linearize(SchemaNode("app", enums:
            [new EnumDiff("app", "status", ChangeKind.Add, Definition: new EnumType("status", ["a", "b"]))]));

        plan.ShouldHaveSingleItem().ShouldBeOfType<CreateEnum>().Enum.Values.ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Linearize_RemoveEnum_EmitsDropEnum()
        => Linearize(SchemaNode("app", enums: [new EnumDiff("app", "status", ChangeKind.Remove)]))
            .ShouldHaveSingleItem().ShouldBeOfType<DropEnum>().EnumName.ShouldBe("status");

    [Fact]
    public void Linearize_RenamedEnum_EmitsRenameEnum_NotCreateOrDrop()
    {
        var plan = Linearize(SchemaNode("app", enums:
            [new EnumDiff("app", "status", ChangeKind.Modify, RenamedFrom: "state")]));

        plan.ShouldHaveSingleItem().ShouldBeOfType<RenameEnum>()
            .ShouldSatisfyAllConditions(r => r.OldName.ShouldBe("state"), r => r.NewName.ShouldBe("status"));
    }

    [Fact]
    public void Linearize_EnumValueAdditions_EmitOneActionEach_InListOrder()
    {
        var plan = Linearize(SchemaNode("app", enums:
        [
            new EnumDiff("app", "status", ChangeKind.Modify, AddedValues:
            [
                new EnumValueAddition("a", Before: "c"),
                new EnumValueAddition("b", After: "a"),
            ]),
        ]));

        var additions = plan.OfType<AddEnumValue>().ToList();
        additions.Select(a => (a.Value, a.Before, a.After)).ShouldBe(
            [("a", "c", null), ("b", null, "a")]);
    }

    [Fact]
    public void Linearize_EnumComment_EmitsSetEnumComment()
        => Linearize(SchemaNode("app", enums:
            [new EnumDiff("app", "status", ChangeKind.Modify, Comment: new ValueChange<string>("old", "new"))]))
            .ShouldHaveSingleItem().ShouldBeOfType<SetEnumComment>()
            .ShouldSatisfyAllConditions(c => c.OldComment.ShouldBe("old"), c => c.NewComment.ShouldBe("new"));

    [Fact]
    public void Linearize_EnumRequiringRecreate_EmitsNoValueActions_AndDoesNotThrow()
    {
        // A removal/reorder cannot be planned; the linearizer stays silent and the always-on
        // EnumValueRemovalDiffPolicy fails the run at the workflow level instead.
        var plan = Linearize(SchemaNode("app", enums:
        [
            new EnumDiff("app", "status", ChangeKind.Modify,
                Values: new ValueChange<IReadOnlyList<string>>(["a", "b"], ["a"])),
        ]));

        plan.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Sequences
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_AddSequence_EmitsCreateSequenceFromDefinition()
    {
        var plan = Linearize(SchemaNode("app", sequences:
            [new SequenceDiff("app", "order_id", ChangeKind.Add, Definition: new Sequence("order_id", new SequenceOptions(StartWith: 100)))]));

        plan.ShouldHaveSingleItem().ShouldBeOfType<CreateSequence>().Sequence.Options.StartWith.ShouldBe(100);
    }

    [Fact]
    public void Linearize_RemoveSequence_EmitsDropSequence()
        => Linearize(SchemaNode("app", sequences: [new SequenceDiff("app", "order_id", ChangeKind.Remove)]))
            .ShouldHaveSingleItem().ShouldBeOfType<DropSequence>().SequenceName.ShouldBe("order_id");

    [Fact]
    public void Linearize_RenamedSequence_EmitsRenameSequence()
        => Linearize(SchemaNode("app", sequences:
            [new SequenceDiff("app", "invoice_id", ChangeKind.Modify, RenamedFrom: "bill_id")]))
            .ShouldHaveSingleItem().ShouldBeOfType<RenameSequence>()
            .ShouldSatisfyAllConditions(r => r.OldName.ShouldBe("bill_id"), r => r.NewName.ShouldBe("invoice_id"));

    [Fact]
    public void Linearize_SequenceOptionsChange_EmitsAlterSequence()
    {
        var options = new ValueChange<SequenceOptions>(
            new SequenceOptions(StartWith: 1), new SequenceOptions(StartWith: 100));

        Linearize(SchemaNode("app", sequences: [new SequenceDiff("app", "order_id", ChangeKind.Modify, Options: options)]))
            .ShouldHaveSingleItem().ShouldBeOfType<AlterSequence>()
            .ShouldSatisfyAllConditions(
                a => a.OldOptions.StartWith.ShouldBe(1),
                a => a.NewOptions.StartWith.ShouldBe(100));
    }

    [Fact]
    public void Linearize_SequenceComment_EmitsSetSequenceComment()
        => Linearize(SchemaNode("app", sequences:
            [new SequenceDiff("app", "order_id", ChangeKind.Modify, Comment: new ValueChange<string>(null, "order numbers"))]))
            .ShouldHaveSingleItem().ShouldBeOfType<SetSequenceComment>().NewComment.ShouldBe("order numbers");

    // -------------------------------------------------------------------------
    // Enum/sequence ordering relative to tables
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_OrdersCreateEnumAndSequenceBeforeCreateTable()
    {
        // A column may use the enum type and a default may call the sequence, so both exist first.
        var plan = Linearize(SchemaNode("app", ChangeKind.Add,
            tables: [TableNode("users", ChangeKind.Add, definition: new Table("users"))],
            enums: [new EnumDiff("app", "status", ChangeKind.Add, Definition: new EnumType("status", ["a"]))],
            sequences: [new SequenceDiff("app", "order_id", ChangeKind.Add, Definition: new Sequence("order_id"))]));

        IndexOf<CreateSchema>(plan).ShouldBeLessThan(IndexOf<CreateEnum>(plan));
        IndexOf<CreateEnum>(plan).ShouldBeLessThan(IndexOf<CreateTable>(plan));
        IndexOf<CreateSequence>(plan).ShouldBeLessThan(IndexOf<CreateTable>(plan));
    }

    [Fact]
    public void Linearize_OrdersAddEnumValueBeforeColumnChanges()
    {
        // A column being retyped to the enum (or defaulted to a new value) needs the value to exist first.
        var plan = Linearize(SchemaNode("app",
            tables:
            [
                TableNode("users", ChangeKind.Modify, columns:
                [
                    ModifiedColumn("status",
                        type: new ValueChange<SqlType>(SqlType.Text, SqlType.Custom("status")),
                        @default: new ValueChange<string>(null, "'a'")),
                ]),
            ],
            enums: [new EnumDiff("app", "status", ChangeKind.Modify, AddedValues: [new EnumValueAddition("a")])]));

        IndexOf<AddEnumValue>(plan).ShouldBeLessThan(IndexOf<AlterColumnType>(plan));
        IndexOf<AddEnumValue>(plan).ShouldBeLessThan(IndexOf<SetColumnDefault>(plan));
    }

    [Fact]
    public void Linearize_OrdersEnumAndSequenceDropsAfterDropTable_BeforeDropSchema()
    {
        var plan = Linearize(
            SchemaNode("app",
                tables: [TableNode("users", ChangeKind.Remove)],
                enums: [new EnumDiff("app", "status", ChangeKind.Remove)],
                sequences: [new SequenceDiff("app", "order_id", ChangeKind.Remove)]),
            SchemaNode("scratch", ChangeKind.Remove));

        IndexOf<DropTable>(plan).ShouldBeLessThan(IndexOf<DropEnum>(plan));
        IndexOf<DropTable>(plan).ShouldBeLessThan(IndexOf<DropSequence>(plan));
        IndexOf<DropEnum>(plan).ShouldBeLessThan(IndexOf<DropSchema>(plan));
        IndexOf<DropSequence>(plan).ShouldBeLessThan(IndexOf<DropSchema>(plan));
    }

    [Fact]
    public void Linearize_OrdersRenameEnumBeforeCreateTable()
    {
        // A new table's columns reference the enum by its new name, so the rename must land first.
        var plan = Linearize(SchemaNode("app",
            tables: [TableNode("users", ChangeKind.Add, definition: new Table("users"))],
            enums: [new EnumDiff("app", "status", ChangeKind.Modify, RenamedFrom: "state")]));

        IndexOf<RenameEnum>(plan).ShouldBeLessThan(IndexOf<CreateTable>(plan));
    }

    // -------------------------------------------------------------------------
    // Functions and procedures
    // -------------------------------------------------------------------------

    private static readonly Routine Fn = new("f", RoutineKind.Function, "a int", "RETURNS int LANGUAGE sql AS $$ SELECT 1; $$");
    private static readonly Routine Proc = new("p", RoutineKind.Procedure, "", "LANGUAGE sql AS $$ DELETE FROM app.t; $$");

    [Fact]
    public void Linearize_AddFunction_EmitsCreateRoutineFromDefinition()
        => Linearize(SchemaNode("app", routines: [new RoutineDiff("app", "f", ChangeKind.Add, RoutineKind.Function, Definition: Fn)]))
            .ShouldHaveSingleItem().ShouldBeOfType<CreateRoutine>().Routine.Arguments.ShouldBe("a int");

    [Fact]
    public void Linearize_RemoveRoutine_EmitsDropRoutine()
        => Linearize(SchemaNode("app", routines: [new RoutineDiff("app", "f", ChangeKind.Remove, RoutineKind.Function)]))
            .ShouldHaveSingleItem().ShouldBeOfType<DropRoutine>().RoutineName.ShouldBe("f");

    [Fact]
    public void Linearize_RoutineBodyChange_EmitsCreateRoutine_NotRecreate()
    {
        // A definition-only change replaces in place (CREATE OR REPLACE semantics, like a view body change).
        var plan = Linearize(SchemaNode("app", routines:
            [new RoutineDiff("app", "f", ChangeKind.Modify, RoutineKind.Function, Definition: Fn)]));

        plan.ShouldHaveSingleItem().ShouldBeOfType<CreateRoutine>();
        plan.OfType<RecreateRoutine>().ShouldBeEmpty();
    }

    [Fact]
    public void Linearize_RoutineSignatureChange_EmitsRecreateRoutine()
        => Linearize(SchemaNode("app", routines:
            [new RoutineDiff("app", "f", ChangeKind.Modify, RoutineKind.Function, Definition: Fn,
                Arguments: new ValueChange<string>("a int", "a int, b text"))]))
            .ShouldHaveSingleItem().ShouldBeOfType<RecreateRoutine>();

    [Fact]
    public void Linearize_RenamedRoutine_EmitsRenameRoutine()
        => Linearize(SchemaNode("app", routines:
            [new RoutineDiff("app", "f", ChangeKind.Modify, RoutineKind.Function, RenamedFrom: "old_f")]))
            .ShouldHaveSingleItem().ShouldBeOfType<RenameRoutine>()
            .ShouldSatisfyAllConditions(r => r.OldName.ShouldBe("old_f"), r => r.NewName.ShouldBe("f"));

    [Fact]
    public void Linearize_RenameWithSignatureChange_RenamesBeforeRecreating()
    {
        // The recreate targets the final name, so the rename must land first.
        var plan = Linearize(SchemaNode("app", routines:
            [new RoutineDiff("app", "f", ChangeKind.Modify, RoutineKind.Function, RenamedFrom: "old_f", Definition: Fn,
                Arguments: new ValueChange<string>("a int", "a int, b text"))]));

        IndexOf<RenameRoutine>(plan).ShouldBeLessThan(IndexOf<RecreateRoutine>(plan));
    }

    [Fact]
    public void Linearize_RoutineComment_EmitsSetRoutineComment()
        => Linearize(SchemaNode("app", routines:
            [new RoutineDiff("app", "f", ChangeKind.Modify, RoutineKind.Function, Comment: new ValueChange<string>("old", "new"))]))
            .ShouldHaveSingleItem().ShouldBeOfType<SetRoutineComment>()
            .ShouldSatisfyAllConditions(c => c.OldComment.ShouldBe("old"), c => c.NewComment.ShouldBe("new"));

    [Fact]
    public void Linearize_ProcedureLifecycle_EmitsRoutineActions()
    {
        var plan = Linearize(SchemaNode("app", routines:
        [
            new RoutineDiff("app", "p", ChangeKind.Add, RoutineKind.Procedure, Definition: Proc),
            new RoutineDiff("app", "q", ChangeKind.Modify, RoutineKind.Procedure, RenamedFrom: "old_q",
                Definition: Proc, Arguments: new ValueChange<string>("", "before date")),
            new RoutineDiff("app", "stale", ChangeKind.Remove, RoutineKind.Procedure),
        ]));

        plan.OfType<CreateRoutine>().ShouldHaveSingleItem();
        plan.OfType<RenameRoutine>().ShouldHaveSingleItem();
        plan.OfType<RecreateRoutine>().ShouldHaveSingleItem();
        plan.OfType<DropRoutine>().ShouldHaveSingleItem().RoutineName.ShouldBe("stale");
    }

    [Fact]
    public void Linearize_OrdersRoutineCreatesBeforeCreateTable_AndAfterEnums()
    {
        // Column DEFAULTs and CHECKs may call routines, and routine args may use enum types.
        var plan = Linearize(SchemaNode("app", ChangeKind.Add,
            tables: [TableNode("users", ChangeKind.Add, definition: new Table("users"))],
            enums: [new EnumDiff("app", "status", ChangeKind.Add, Definition: new EnumType("status", ["a"]))],
            routines:
            [
                new RoutineDiff("app", "f", ChangeKind.Add, RoutineKind.Function, Definition: Fn),
                new RoutineDiff("app", "p", ChangeKind.Add, RoutineKind.Procedure, Definition: Proc),
            ]));

        IndexOf<CreateEnum>(plan).ShouldBeLessThan(IndexOf<CreateRoutine>(plan));
        IndexOf<CreateRoutine>(plan).ShouldBeLessThan(IndexOf<CreateTable>(plan));
    }

    [Fact]
    public void Linearize_OrdersRoutineDropsAfterDropTable_BeforeDropEnum()
    {
        var plan = Linearize(SchemaNode("app",
            tables: [TableNode("users", ChangeKind.Remove)],
            enums: [new EnumDiff("app", "status", ChangeKind.Remove)],
            routines: [new RoutineDiff("app", "f", ChangeKind.Remove, RoutineKind.Function)]));

        IndexOf<DropTable>(plan).ShouldBeLessThan(IndexOf<DropRoutine>(plan));
        IndexOf<DropRoutine>(plan).ShouldBeLessThan(IndexOf<DropEnum>(plan));
    }

    [Fact]
    public void Linearize_OrdersViewsAroundRoutines()
    {
        // A view may call a routine: views are created after routines and dropped before them.
        var plan = Linearize(SchemaNode("app",
            views: [AddView("v"), RemoveView("stale_v")],
            routines:
            [
                new RoutineDiff("app", "f", ChangeKind.Add, RoutineKind.Function, Definition: Fn),
                new RoutineDiff("app", "stale_f", ChangeKind.Remove, RoutineKind.Function),
            ]));

        IndexOf<CreateRoutine>(plan).ShouldBeLessThan(IndexOfCreateView(plan, "v"));
        IndexOfDropView(plan, "stale_v").ShouldBeLessThan(IndexOf<DropRoutine>(plan));
    }
}
