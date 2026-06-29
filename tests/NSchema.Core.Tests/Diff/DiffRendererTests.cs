using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Views;

namespace NSchema.Tests.Diff;

public sealed class DiffRendererTests
{
    // -------------------------------------------------------------------------
    // Helpers — render and build diff fragments concisely.
    // -------------------------------------------------------------------------

    private static string Render(DatabaseDiff diff, string? indent = null)
    {
        var options = new DiffRendererOptions();
        if (indent is not null)
        {
            options.Indent = indent;
        }

        return new DiffRenderer(options).Render(diff);
    }

    private static DatabaseDiff DiffOf(IReadOnlyList<SchemaDiff>? schemas = null) => new(schemas ?? []);

    private static SchemaDiff Schema(
        string name,
        ChangeKind? kind = null,
        string? renamedFrom = null,
        ValueChange<string>? comment = null,
        IReadOnlyList<GrantChange>? grants = null,
        IReadOnlyList<TableDiff>? tables = null
    ) => new(name, kind, renamedFrom, comment, grants ?? [], tables ?? []);

    private static TableDiff Table(
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
        IReadOnlyList<CheckConstraintDiff>? checks = null)
        => new(schema, name, kind, renamedFrom, comment, columns ?? [], grants ?? [], indexes ?? [],
            primaryKey ?? [], foreignKeys ?? [], uniqueConstraints ?? [], checks ?? []);

    private static ColumnDiff AddColumn(Column definition, ValueChange<string>? comment = null)
        => new(definition.Name, ChangeKind.Add, definition, null, null, null, null, null, comment);

    private static ColumnDiff RemoveColumn(Column definition)
        => new(definition.Name, ChangeKind.Remove, definition, null, null, null, null, null, null);

    private static ColumnDiff ModifyColumn(
        string name,
        string? renamedFrom = null,
        ValueChange<SqlType>? type = null,
        ValueChange<bool>? nullability = null,
        ValueChange<string>? @default = null,
        ValueChange<IdentityOptions>? identity = null,
        ValueChange<string>? comment = null)
        => new(name, ChangeKind.Modify, null, renamedFrom, type, nullability, @default, identity, comment);

    /// <summary>Wraps a single table-changing schema (null schema kind) for brevity.</summary>
    private static DatabaseDiff WithTable(TableDiff table)
        => DiffOf([Schema("app", tables: [table])]);

    /// <summary>Wraps a single view-changing schema (null schema kind) for brevity.</summary>
    private static DatabaseDiff WithView(ViewDiff view)
        => DiffOf([new SchemaDiff("app", Views: [view])]);

    // -------------------------------------------------------------------------
    // Empty / summary
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_EmptyDiff_ReturnsNoChanges()
        => Render(DiffOf()).ShouldBe("No changes detected.");

    [Fact]
    public void Render_IncludesPlanSummaryLine()
    {
        var diff = DiffOf(
        [
            Schema("new_schema", ChangeKind.Add),
            Schema("app", tables:
            [
                Table("orders", ChangeKind.Modify, columns: [ModifyColumn("total", type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt))]),
                Table("audit", ChangeKind.Remove),
            ]),
        ]);

        // new_schema (add) + total column (add? no — type change = modify); orders (modify), audit (remove).
        Render(diff).ShouldContain("Plan: 1 to add, 2 to change, 1 to destroy.");
    }

    // -------------------------------------------------------------------------
    // Schema
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_SchemaAdd_RendersHeaderWithMarker()
        => Render(DiffOf([Schema("app", ChangeKind.Add)])).ShouldContain("+ schema app");

    [Fact]
    public void Render_SchemaRemove_RendersRemoveMarker()
        => Render(DiffOf([Schema("app", ChangeKind.Remove)])).ShouldContain("- schema app");

    [Fact]
    public void Render_SchemaRename_RendersArrow()
        => Render(DiffOf([Schema("app", ChangeKind.Modify, renamedFrom: "legacy")]))
            .ShouldContain("schema legacy → app");

    [Fact]
    public void Render_SchemaComment_AppendsNewCommentSuffix()
        => Render(DiffOf([Schema("app", ChangeKind.Add, comment: new ValueChange<string>(null, "primary"))]))
            .ShouldContain("schema app (\"primary\")");

    [Fact]
    public void Render_SchemaWithNullKind_SkipsHeaderButRendersTables()
    {
        var output = Render(WithTable(Table("users", ChangeKind.Add)));

        output.ShouldNotContain("schema app");
        output.ShouldContain("+ table app.users");
    }

    [Fact]
    public void Render_SchemaGrantAdd_RendersGrantUsage()
    {
        var diff = DiffOf([Schema("app", ChangeKind.Add, grants: [new GrantChange(ChangeKind.Add, "reader", null)])]);

        Render(diff).ShouldContain("+ grant usage to reader");
    }

    [Fact]
    public void Render_SchemaGrantRemove_RendersRevokeUsage()
    {
        var diff = DiffOf([Schema("app", ChangeKind.Modify, grants: [new GrantChange(ChangeKind.Remove, "reader", null)])]);

        Render(diff).ShouldContain("- revoke usage from reader");
    }

    // -------------------------------------------------------------------------
    // Table
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_TableAdd_RendersSchemaQualifiedName()
        => Render(WithTable(Table("users", ChangeKind.Add))).ShouldContain("+ table app.users");

    [Fact]
    public void Render_TableRename_RendersArrowWithSchemaQualifier()
        => Render(WithTable(Table("users", ChangeKind.Modify, renamedFrom: "people")))
            .ShouldContain("table app.people → users");

    [Fact]
    public void Render_TableComment_AppendsChangedCommentSuffix()
        => Render(WithTable(Table("users", ChangeKind.Modify, comment: new ValueChange<string>("old", "new"))))
            .ShouldContain("table app.users (\"old\" → \"new\")");

    [Fact]
    public void Render_AddedTable_SeparatesColumnBlockFromTrailingBlockWithBlankLine()
    {
        var table = Table("users", ChangeKind.Add,
            columns: [AddColumn(new Column("id", SqlType.Int))],
            indexes: [new IndexDiff(ChangeKind.Add, "users_id_ix", new TableIndex("users_id_ix", ["id"]), null)]);

        var lines = Render(WithTable(table)).Split('\n');
        var columnLine = Array.FindIndex(lines, l => l.Contains("+ id int not null"));
        var indexLine = Array.FindIndex(lines, l => l.Contains("index users_id_ix"));

        columnLine.ShouldBeGreaterThan(-1);
        indexLine.ShouldBeGreaterThan(columnLine);
        // Exactly one blank line between the column block and the trailing index block.
        lines[(columnLine + 1)..indexLine].ShouldBe([""]);
    }

    // -------------------------------------------------------------------------
    // Columns
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_ColumnAdd_RendersDefinitionAndCommentSuffix()
    {
        var column = AddColumn(new Column("id", SqlType.Int), comment: new ValueChange<string>(null, "identifier"));

        Render(WithTable(Table("users", ChangeKind.Add, columns: [column])))
            .ShouldContain("+ id int not null (\"identifier\")");
    }

    [Fact]
    public void Render_ColumnAdd_NullableRendersNull()
        => Render(WithTable(Table("users", ChangeKind.Add, columns: [AddColumn(new Column("bio", SqlType.Text, IsNullable: true))])))
            .ShouldContain("+ bio text null");

    [Fact]
    public void Render_ColumnRemove_RendersDefinition()
        => Render(WithTable(Table("users", ChangeKind.Modify, columns: [RemoveColumn(new Column("id", SqlType.Int))])))
            .ShouldContain("- id int not null");

    [Fact]
    public void Render_ColumnRename_RendersArrow()
        => Render(WithTable(Table("users", ChangeKind.Modify, columns: [ModifyColumn("email", renamedFrom: "mail")])))
            .ShouldContain("~ rename column: mail → email");

    [Fact]
    public void Render_ColumnTypeChange_RendersOldToNew()
        => Render(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("total", type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt))])))
            .ShouldContain("~ total type: int → bigint");

    [Fact]
    public void Render_ColumnNullabilityChange_RendersWords()
        => Render(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("email", nullability: new ValueChange<bool>(false, true))])))
            .ShouldContain("~ email nullable: not null → null");

    [Fact]
    public void Render_ColumnDefaultChange_RendersNoneForNull()
        => Render(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("status", @default: new ValueChange<string>(null, "'active'"))])))
            .ShouldContain("~ status default: <none> → 'active'");

    [Fact]
    public void Render_ColumnIdentityChange_RendersOptionParts()
        => Render(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("id", identity: new ValueChange<IdentityOptions>(null, new IdentityOptions(1, 1, 2)))])))
            .ShouldContain("~ id identity: <none> → start=1, min=1, step=2");

    [Fact]
    public void Render_ColumnIdentityChange_RendersDefaultWhenNoParts()
        => Render(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("id", identity: new ValueChange<IdentityOptions>(null, new IdentityOptions(null, null, null)))])))
            .ShouldContain("~ id identity: <none> → <default>");

    [Fact]
    public void Render_ColumnCommentChange_RendersQuotedValues()
        => Render(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("id", comment: new ValueChange<string>("old", "new"))])))
            .ShouldContain("~ id comment: \"old\" → \"new\"");

    [Fact]
    public void Render_ColumnWithMultipleChanges_RendersEachOnItsOwnLine()
    {
        var column = ModifyColumn("email",
            type: new ValueChange<SqlType>(SqlType.VarChar(50), SqlType.Text),
            nullability: new ValueChange<bool>(true, false));

        var output = Render(WithTable(Table("users", ChangeKind.Modify, columns: [column])));

        output.ShouldContain("~ email type: varchar(50) → text");
        output.ShouldContain("~ email nullable: null → not null");
    }

    // -------------------------------------------------------------------------
    // Constraints, indexes, table grants
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_PrimaryKeyConstraint_RendersLabel()
    {
        var pk = new PrimaryKeyDiff(ChangeKind.Add, "users_pkey", null);

        Render(WithTable(Table("users", ChangeKind.Modify, primaryKey: [pk]))).ShouldContain("+ primary key users_pkey");
    }

    [Fact]
    public void Render_ForeignKeyConstraint_RendersLabel()
    {
        var fk = new ForeignKeyDiff(ChangeKind.Remove, "orders_user_fk", null);

        Render(WithTable(Table("orders", ChangeKind.Modify, foreignKeys: [fk]))).ShouldContain("- foreign key orders_user_fk");
    }

    [Fact]
    public void Render_UniqueConstraint_RendersLabel()
    {
        var unique = new UniqueConstraintDiff(ChangeKind.Add, "users_email_uq", null);

        Render(WithTable(Table("users", ChangeKind.Modify, uniqueConstraints: [unique]))).ShouldContain("+ unique constraint users_email_uq");
    }

    [Fact]
    public void Render_CheckConstraint_RendersLabel()
    {
        var check = new CheckConstraintDiff(ChangeKind.Remove, "users_age_chk", null);

        Render(WithTable(Table("users", ChangeKind.Modify, checks: [check]))).ShouldContain("- check constraint users_age_chk");
    }

    [Fact]
    public void Render_ConstraintCommentChange_RendersCommentDiff()
    {
        var unique = new UniqueConstraintDiff(ChangeKind.Modify, "users_email_uq", null, new ValueChange<string>("old", "new"));

        Render(WithTable(Table("users", ChangeKind.Modify, uniqueConstraints: [unique])))
            .ShouldContain("unique constraint users_email_uq comment: \"old\" → \"new\"");
    }

    [Fact]
    public void Render_IndexAdd_RendersName()
    {
        var index = new IndexDiff(ChangeKind.Add, "users_email_ux", new TableIndex("users_email_ux", ["email"], IsUnique: true), null);

        Render(WithTable(Table("users", ChangeKind.Modify, indexes: [index]))).ShouldContain("+ index users_email_ux");
    }

    [Fact]
    public void Render_IndexCommentModify_RendersOldToNew()
    {
        var index = new IndexDiff(ChangeKind.Modify, "users_email_ux", null, new ValueChange<string>(null, "speed"));

        Render(WithTable(Table("users", ChangeKind.Modify, indexes: [index])))
            .ShouldContain("~ index users_email_ux comment: <none> → \"speed\"");
    }

    [Fact]
    public void Render_TableGrantAdd_RendersPrivilegeAndRole()
    {
        var grant = new GrantChange(ChangeKind.Add, "reader", TablePrivilege.Insert);

        Render(WithTable(Table("users", ChangeKind.Modify, grants: [grant]))).ShouldContain("+ grant INSERT to reader");
    }

    [Fact]
    public void Render_TableGrantRemove_RendersPrivilegeAndRole()
    {
        var grant = new GrantChange(ChangeKind.Remove, "reader", TablePrivilege.Insert);

        Render(WithTable(Table("users", ChangeKind.Modify, grants: [grant]))).ShouldContain("- revoke INSERT from reader");
    }

    [Theory]
    // Select and its alias ReadOnly share value 1, so a single case proves the alias renders as "SELECT".
    [InlineData(TablePrivilege.Select, "SELECT")]
    [InlineData(TablePrivilege.AppendOnly, "SELECT, INSERT")]  // composite
    [InlineData(TablePrivilege.Select | TablePrivilege.Delete, "SELECT, DELETE")]
    [InlineData(TablePrivilege.All, "SELECT, INSERT, UPDATE, DELETE")]
    [InlineData(TablePrivilege.None, "no privileges")]
    public void Render_TableGrant_DecomposesPrivilegeFlags(TablePrivilege privileges, string expected)
    {
        var grant = new GrantChange(ChangeKind.Add, "reader", privileges);

        Render(WithTable(Table("users", ChangeKind.Modify, grants: [grant])))
            .ShouldContain($"+ grant {expected} to reader");
    }

    // -------------------------------------------------------------------------
    // Views
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_ViewAdd_RendersSchemaQualifiedName()
        => Render(WithView(new ViewDiff("app", "active_users", ChangeKind.Add, Definition: new View("active_users", "SELECT 1"))))
            .ShouldContain("+ view app.active_users");

    [Fact]
    public void Render_ViewAdd_AppendsCommentSuffix()
        => Render(WithView(new ViewDiff("app", "active_users", ChangeKind.Add,
                Definition: new View("active_users", "SELECT 1"), Comment: new ValueChange<string>(null, "active"))))
            .ShouldContain("+ view app.active_users (\"active\")");

    [Fact]
    public void Render_ViewBodyReplace_RendersModifyHeader()
        => Render(WithView(new ViewDiff("app", "daily_totals", ChangeKind.Modify,
                Definition: new View("daily_totals", "SELECT sum(x) FROM app.sales"))))
            .ShouldContain("~ view app.daily_totals");

    [Fact]
    public void Render_ViewCommentOnlyChange_RendersCommentDiff()
        => Render(WithView(new ViewDiff("app", "summary", ChangeKind.Modify, Comment: new ValueChange<string>("old", "new"))))
            .ShouldContain("~ view app.summary comment: \"old\" → \"new\"");

    [Fact]
    public void Render_ViewRename_RendersArrowWithSchemaQualifier()
        => Render(WithView(new ViewDiff("app", "report", ChangeKind.Modify, RenamedFrom: "legacy_report")))
            .ShouldContain("view app.legacy_report → report");

    [Fact]
    public void Render_ViewRemove_RendersRemoveMarker()
        => Render(WithView(new ViewDiff("app", "stale_view", ChangeKind.Remove)))
            .ShouldContain("- view app.stale_view");

    // -------------------------------------------------------------------------
    // Formatting
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_EmitsPlainMarkers()
    {
        var output = Render(DiffOf([Schema("app", ChangeKind.Add)]));

        output.ShouldNotContain("\x1b["); // plain text only — no ANSI escape sequences
        output.ShouldContain("+ schema app");
    }

    [Fact]
    public void Render_RespectsCustomIndent()
    {
        var diff = WithTable(Table("users", ChangeKind.Modify, columns: [RemoveColumn(new Column("id", SqlType.Int))]));

        var output = Render(diff, indent: ">>");

        output.ShouldContain(">>- id int not null");
    }
}
