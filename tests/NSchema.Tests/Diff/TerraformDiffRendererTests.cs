using Microsoft.Extensions.Options;
using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Tests.Diff;

public sealed class TerraformDiffRendererTests
{
    // -------------------------------------------------------------------------
    // Helpers — render and build diff fragments concisely.
    // -------------------------------------------------------------------------

    private static string Render(MigrationDiff diff, bool colour = false, string? indent = null)
    {
        var options = new TerraformDiffRendererOptions { IncludeColour = colour };
        if (indent is not null)
        {
            options.Indent = indent;
        }

        return new TerraformDiffRenderer(Options.Create(options)).Render(diff);
    }

    private static MigrationDiff DiffOf(
        IReadOnlyList<SchemaDiff>? schemas = null,
        IReadOnlyList<Script>? pre = null,
        IReadOnlyList<Script>? post = null
    ) => new(schemas ?? [], pre ?? [], post ?? []);

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
        IReadOnlyList<ConstraintDiff>? constraints = null)
        => new(schema, name, kind, renamedFrom, comment, columns ?? [], grants ?? [], indexes ?? [], constraints ?? []);

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
    private static MigrationDiff WithTable(TableDiff table)
        => DiffOf([Schema("app", tables: [table])]);

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
            columns: [AddColumn(Column.Create("id", SqlType.Int))],
            indexes: [new IndexDiff(ChangeKind.Add, "users_id_ix", TableIndex.Create("users_id_ix", ["id"]), null)]);

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
        var column = AddColumn(Column.Create("id", SqlType.Int), comment: new ValueChange<string>(null, "identifier"));

        Render(WithTable(Table("users", ChangeKind.Add, columns: [column])))
            .ShouldContain("+ id int not null (\"identifier\")");
    }

    [Fact]
    public void Render_ColumnAdd_NullableRendersNull()
        => Render(WithTable(Table("users", ChangeKind.Add, columns: [AddColumn(Column.Create("bio", SqlType.Text, isNullable: true))])))
            .ShouldContain("+ bio text null");

    [Fact]
    public void Render_ColumnRemove_RendersDefinition()
        => Render(WithTable(Table("users", ChangeKind.Modify, columns: [RemoveColumn(Column.Create("id", SqlType.Int))])))
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
        var pk = new ConstraintDiff(ChangeKind.Add, ConstraintType.PrimaryKey, "users_pkey", null, null);

        Render(WithTable(Table("users", ChangeKind.Modify, constraints: [pk]))).ShouldContain("+ primary key users_pkey");
    }

    [Fact]
    public void Render_ForeignKeyConstraint_RendersLabel()
    {
        var fk = new ConstraintDiff(ChangeKind.Remove, ConstraintType.ForeignKey, "orders_user_fk", null, null);

        Render(WithTable(Table("orders", ChangeKind.Modify, constraints: [fk]))).ShouldContain("- foreign key orders_user_fk");
    }

    [Fact]
    public void Render_IndexAdd_RendersName()
    {
        var index = new IndexDiff(ChangeKind.Add, "users_email_ux", TableIndex.Create("users_email_ux", ["email"], isUnique: true), null);

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
    // Scripts
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_PreAndPostScripts_RenderSeparateSections()
    {
        var diff = DiffOf(
            [Schema("app", ChangeKind.Add)],
            pre: [new Script("0001_pre", "SELECT 1", ScriptType.PreDeployment)],
            post: [new Script("0001_post", "SELECT 2", ScriptType.PostDeployment)]);

        var output = Render(diff);

        output.ShouldContain("Pre-deployment scripts:");
        output.ShouldContain("  • 0001_pre");
        output.ShouldContain("Post-deployment scripts:");
        output.ShouldContain("  • 0001_post");
    }

    [Fact]
    public void Render_NoScripts_OmitsScriptSections()
        => Render(DiffOf([Schema("app", ChangeKind.Add)])).ShouldNotContain("deployment scripts");

    // -------------------------------------------------------------------------
    // Colour / formatting options
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_WithColour_EmitsAnsiEscapeCodes()
    {
        var output = Render(DiffOf([Schema("app", ChangeKind.Add)]), colour: true);

        output.ShouldContain("\x1b[32m"); // green marker for an addition
        output.ShouldContain("schema app");
    }

    [Fact]
    public void Render_WithoutColour_EmitsPlainMarkers()
    {
        var output = Render(DiffOf([Schema("app", ChangeKind.Add)]));

        output.ShouldNotContain("\x1b["); // no ANSI escape sequences at all
        output.ShouldContain("+ schema app");
    }

    [Fact]
    public void Render_RespectsCustomIndent()
    {
        var diff = WithTable(Table("users", ChangeKind.Modify, columns: [RemoveColumn(Column.Create("id", SqlType.Int))]));

        var output = Render(diff, indent: ">>");

        output.ShouldContain(">>- id int not null");
    }
}
