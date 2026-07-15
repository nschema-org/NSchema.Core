using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Columns;
using NSchema.Diff.Domain.Models.Constraints;
using NSchema.Diff.Domain.Models.Indexes;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Diff.Domain.Models.Views;
using NSchema.Diff.Reader;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Indexes;
using NSchema.Model.Tables;
using NSchema.Model.Views;

namespace NSchema.Tests.Diff;

public sealed class DiffReaderTests
{
    // -------------------------------------------------------------------------
    // Helpers — read a diff and assert over the structured document it produces.
    // -------------------------------------------------------------------------
    private static DiffDocument Read(DatabaseDiff diff) => DiffReader.Default.Read(diff);

    /// <summary>Asserts a content line exists with the given kind whose text contains the snippet.</summary>
    private static void ShouldHaveLine(DatabaseDiff diff, ChangeKind kind, string textContains)
        => Read(diff).Lines.ShouldContain(line => line.Kind == kind && line.Text.Contains(textContains));

    private static DatabaseDiff DiffOf(IReadOnlyList<SchemaDiff>? schemas = null) => new(schemas ?? []);

    private static SchemaDiff Schema(
        string name,
        ChangeKind? kind = null,
        SqlIdentifier? renamedFrom = null,
        ValueChange<string>? comment = null,
        IReadOnlyList<GrantChange>? grants = null,
        IReadOnlyList<TableDiff>? tables = null
    ) => new(new SqlIdentifier(name), kind, renamedFrom, comment, grants ?? [], tables ?? []);

    private static TableDiff Table(
        string name,
        ChangeKind kind,
        string schema = "app",
        SqlIdentifier? renamedFrom = null,
        ValueChange<string>? comment = null,
        IReadOnlyList<ColumnDiff>? columns = null,
        IReadOnlyList<GrantChange>? grants = null,
        IReadOnlyList<IndexDiff>? indexes = null,
        IReadOnlyList<PrimaryKeyDiff>? primaryKey = null,
        IReadOnlyList<ForeignKeyDiff>? foreignKeys = null,
        IReadOnlyList<UniqueConstraintDiff>? uniqueConstraints = null,
        IReadOnlyList<CheckConstraintDiff>? checks = null)
        => new(new SqlIdentifier(schema), new SqlIdentifier(name), kind, renamedFrom, comment, columns ?? [], grants ?? [], indexes ?? [],
            primaryKey ?? [], foreignKeys ?? [], uniqueConstraints ?? [], checks ?? []);

    private static ColumnDiff AddColumn(Column definition, ValueChange<string>? comment = null)
        => new(definition.Name, ChangeKind.Add, definition, null, null, null, null, null, comment);

    private static ColumnDiff RemoveColumn(Column definition)
        => new(definition.Name, ChangeKind.Remove, definition, null, null, null, null, null, null);

    private static ColumnDiff ModifyColumn(
        string name,
        SqlIdentifier? renamedFrom = null,
        ValueChange<SqlType>? type = null,
        ValueChange<bool>? nullability = null,
        ValueChange<SqlText>? @default = null,
        ValueChange<IdentityOptions>? identity = null,
        ValueChange<string>? comment = null)
        => new(new SqlIdentifier(name), ChangeKind.Modify, null, renamedFrom, type, nullability, @default, identity, comment);

    /// <summary>Wraps a single table-changing schema (null schema kind) for brevity.</summary>
    private static DatabaseDiff WithTable(TableDiff table)
        => DiffOf([Schema("app", tables: [table])]);

    /// <summary>Wraps a single view-changing schema (null schema kind) for brevity.</summary>
    private static DatabaseDiff WithView(ViewDiff view)
        => DiffOf([new SchemaDiff(new SqlIdentifier("app"), Views: [view])]);

    // -------------------------------------------------------------------------
    // Empty / summary
    // -------------------------------------------------------------------------

    [Fact]
    public void Read_EmptyDiff_IsEmptyWithZeroSummary()
    {
        var document = Read(DiffOf());

        document.IsEmpty.ShouldBeTrue();
        document.Lines.ShouldBeEmpty();
        document.Summary.ShouldBe(new DiffSummary(0, 0, 0));
    }

    [Fact]
    public void Read_PopulatesSummaryCounts()
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

        // new_schema (add); orders table + its total column (modify ×2); audit table (remove).
        Read(diff).Summary.ShouldBe(new DiffSummary(1, 2, 1));
    }

    // -------------------------------------------------------------------------
    // Schema
    // -------------------------------------------------------------------------

    [Fact]
    public void Read_SchemaAdd_EmitsAddHeader()
        => ShouldHaveLine(DiffOf([Schema("app", ChangeKind.Add)]), ChangeKind.Add, "schema app");

    [Fact]
    public void Read_SchemaRemove_EmitsRemoveHeader()
        => ShouldHaveLine(DiffOf([Schema("app", ChangeKind.Remove)]), ChangeKind.Remove, "schema app");

    [Fact]
    public void Read_SchemaRename_EmitsArrow()
        => ShouldHaveLine(DiffOf([Schema("app", ChangeKind.Modify, renamedFrom: new SqlIdentifier("legacy"))]), ChangeKind.Modify, "schema legacy → app");

    [Fact]
    public void Read_SchemaComment_AppendsNewCommentSuffix()
        => ShouldHaveLine(DiffOf([Schema("app", ChangeKind.Add, comment: new ValueChange<string>(null, "primary"))]), ChangeKind.Add, "schema app (\"primary\")");

    [Fact]
    public void Read_SchemaWithNullKind_SkipsHeaderButEmitsTables()
    {
        var lines = Read(WithTable(Table("users", ChangeKind.Add))).Lines;

        lines.ShouldNotContain(line => line.Text.Contains("schema app"));
        lines.ShouldContain(line => line.Kind == ChangeKind.Add && line.Text == "table app.users");
    }

    [Fact]
    public void Read_SchemaGrantAdd_EmitsGrantUsage()
        => ShouldHaveLine(DiffOf([Schema("app", ChangeKind.Add, grants: [new GrantChange(ChangeKind.Add, new SqlIdentifier("reader"), null)])]), ChangeKind.Add, "grant usage to reader");

    [Fact]
    public void Read_SchemaGrantRemove_EmitsRevokeUsage()
        => ShouldHaveLine(DiffOf([Schema("app", ChangeKind.Modify, grants: [new GrantChange(ChangeKind.Remove, new SqlIdentifier("reader"), null)])]), ChangeKind.Remove, "revoke usage from reader");

    // -------------------------------------------------------------------------
    // Table
    // -------------------------------------------------------------------------

    [Fact]
    public void Read_TableAdd_EmitsSchemaObjectReference()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Add)), ChangeKind.Add, "table app.users");

    [Fact]
    public void Read_TableRename_EmitsArrowWithSchemaQualifier()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, renamedFrom: new SqlIdentifier("people"))), ChangeKind.Modify, "table app.people → users");

    [Fact]
    public void Read_TableComment_AppendsChangedCommentSuffix()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, comment: new ValueChange<string>("old", "new"))), ChangeKind.Modify, "table app.users (\"old\" → \"new\")");

    [Fact]
    public void Read_AddedTable_SeparatesColumnBlockFromTrailingBlockWithSpacer()
    {
        var table = Table("users", ChangeKind.Add,
            columns: [AddColumn(new Column(new SqlIdentifier("id"), SqlType.Int))],
            indexes: [new IndexDiff(ChangeKind.Add, new SqlIdentifier("users_id_ix"), new TableIndex(new SqlIdentifier("users_id_ix"), ["id"]), null)]);

        var lines = Read(WithTable(table)).Lines;
        var columnIndex = IndexOf(lines, line => line.Text.Contains("id int not null"));
        var indexIndex = IndexOf(lines, line => line.Text.Contains("index users_id_ix"));

        columnIndex.ShouldBeGreaterThanOrEqualTo(0);
        indexIndex.ShouldBeGreaterThan(columnIndex);
        // Exactly one kindless spacer line separates the column block from the trailing index block.
        var between = lines.Skip(columnIndex + 1).Take(indexIndex - columnIndex - 1).ToList();
        between.ShouldHaveSingleItem().Kind.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Columns
    // -------------------------------------------------------------------------

    [Fact]
    public void Read_ColumnAdd_EmitsDefinitionAndCommentSuffix()
    {
        var column = AddColumn(new Column(new SqlIdentifier("id"), SqlType.Int), comment: new ValueChange<string>(null, "identifier"));

        ShouldHaveLine(WithTable(Table("users", ChangeKind.Add, columns: [column])), ChangeKind.Add, "id int not null (\"identifier\")");
    }

    [Fact]
    public void Read_ColumnAdd_NullableEmitsNull()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Add, columns: [AddColumn(new Column(new SqlIdentifier("bio"), SqlType.Text, IsNullable: true))])), ChangeKind.Add, "bio text null");

    [Fact]
    public void Read_ColumnRemove_EmitsDefinition()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, columns: [RemoveColumn(new Column(new SqlIdentifier("id"), SqlType.Int))])), ChangeKind.Remove, "id int not null");

    [Fact]
    public void Read_ColumnRename_EmitsArrow()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, columns: [ModifyColumn("email", renamedFrom: new SqlIdentifier("mail"))])), ChangeKind.Modify, "rename column: mail → email");

    [Fact]
    public void Read_ColumnTypeChange_EmitsOldToNew()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("total", type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt))])), ChangeKind.Modify, "total type: int → bigint");

    [Fact]
    public void Read_ColumnNullabilityChange_EmitsWords()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("email", nullability: new ValueChange<bool>(false, true))])), ChangeKind.Modify, "email nullable: not null → null");

    [Fact]
    public void Read_ColumnDefaultChange_EmitsNoneForNull()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("status", @default: new ValueChange<SqlText>(null, new SqlText("'active'")))])), ChangeKind.Modify, "status default: <none> → 'active'");

    [Fact]
    public void Read_ColumnIdentityChange_EmitsOptionParts()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("id", identity: new ValueChange<IdentityOptions>(null, new IdentityOptions(1, 1, 2)))])), ChangeKind.Modify, "id identity: <none> → start=1, min=1, step=2");

    [Fact]
    public void Read_ColumnIdentityChange_EmitsDefaultWhenNoParts()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("id", identity: new ValueChange<IdentityOptions>(null, new IdentityOptions(null, null, null)))])), ChangeKind.Modify, "id identity: <none> → <default>");

    [Fact]
    public void Read_ColumnCommentChange_EmitsQuotedValues()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("id", comment: new ValueChange<string>("old", "new"))])), ChangeKind.Modify, "id comment: \"old\" → \"new\"");

    [Fact]
    public void Read_ColumnWithMultipleChanges_EmitsEachOnItsOwnLine()
    {
        var column = ModifyColumn("email",
            type: new ValueChange<SqlType>(SqlType.VarChar(50), SqlType.Text),
            nullability: new ValueChange<bool>(true, false));

        var diff = WithTable(Table("users", ChangeKind.Modify, columns: [column]));

        ShouldHaveLine(diff, ChangeKind.Modify, "email type: varchar(50) → text");
        ShouldHaveLine(diff, ChangeKind.Modify, "email nullable: null → not null");
    }

    // -------------------------------------------------------------------------
    // Constraints, indexes, table grants
    // -------------------------------------------------------------------------

    [Fact]
    public void Read_PrimaryKeyConstraint_EmitsLabel()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, primaryKey: [new PrimaryKeyDiff(ChangeKind.Add, new SqlIdentifier("users_pkey"), null)])), ChangeKind.Add, "primary key users_pkey");

    [Fact]
    public void Read_ForeignKeyConstraint_EmitsLabel()
        => ShouldHaveLine(WithTable(Table("orders", ChangeKind.Modify, foreignKeys: [new ForeignKeyDiff(ChangeKind.Remove, new SqlIdentifier("orders_user_fk"), null)])), ChangeKind.Remove, "foreign key orders_user_fk");

    [Fact]
    public void Read_UniqueConstraint_EmitsLabel()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, uniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_email_uq"), null)])), ChangeKind.Add, "unique constraint users_email_uq");

    [Fact]
    public void Read_CheckConstraint_EmitsLabel()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, checks: [new CheckConstraintDiff(ChangeKind.Remove, new SqlIdentifier("users_age_chk"), null)])), ChangeKind.Remove, "check constraint users_age_chk");

    [Fact]
    public void Read_ConstraintCommentChange_EmitsCommentDiff()
    {
        var unique = new UniqueConstraintDiff(ChangeKind.Modify, new SqlIdentifier("users_email_uq"), null, new ValueChange<string>("old", "new"));

        ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, uniqueConstraints: [unique])), ChangeKind.Modify, "unique constraint users_email_uq comment: \"old\" → \"new\"");
    }

    [Fact]
    public void Read_IndexAdd_EmitsName()
    {
        var index = new IndexDiff(ChangeKind.Add, new SqlIdentifier("users_email_ux"), new TableIndex(new SqlIdentifier("users_email_ux"), ["email"], IsUnique: true), null);

        ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, indexes: [index])), ChangeKind.Add, "index users_email_ux");
    }

    [Fact]
    public void Read_IndexCommentModify_EmitsOldToNew()
    {
        var index = new IndexDiff(ChangeKind.Modify, new SqlIdentifier("users_email_ux"), null, new ValueChange<string>(null, "speed"));

        ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, indexes: [index])), ChangeKind.Modify, "index users_email_ux comment: <none> → \"speed\"");
    }

    [Fact]
    public void Read_TableGrantAdd_EmitsPrivilegeAndRole()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, grants: [new GrantChange(ChangeKind.Add, new SqlIdentifier("reader"), TablePrivilege.Insert)])), ChangeKind.Add, "grant INSERT to reader");

    [Fact]
    public void Read_TableGrantRemove_EmitsPrivilegeAndRole()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, grants: [new GrantChange(ChangeKind.Remove, new SqlIdentifier("reader"), TablePrivilege.Insert)])), ChangeKind.Remove, "revoke INSERT from reader");

    [Theory]
    // Select and its alias ReadOnly share value 1, so a single case proves the alias renders as "SELECT".
    [InlineData(TablePrivilege.Select, "SELECT")]
    [InlineData(TablePrivilege.AppendOnly, "SELECT, INSERT")]  // composite
    [InlineData(TablePrivilege.Select | TablePrivilege.Delete, "SELECT, DELETE")]
    [InlineData(TablePrivilege.All, "SELECT, INSERT, UPDATE, DELETE")]
    [InlineData(TablePrivilege.None, "no privileges")]
    public void Read_TableGrant_DecomposesPrivilegeFlags(TablePrivilege privileges, string expected)
    {
        var grant = new GrantChange(ChangeKind.Add, new SqlIdentifier("reader"), privileges);

        ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, grants: [grant])), ChangeKind.Add, $"grant {expected} to reader");
    }

    // -------------------------------------------------------------------------
    // Views
    // -------------------------------------------------------------------------

    [Fact]
    public void Read_ViewAdd_EmitsSchemaObjectReference()
        => ShouldHaveLine(WithView(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("active_users"), ChangeKind.Add, Definition: new View(new SqlIdentifier("active_users"), new SqlText("SELECT 1")))), ChangeKind.Add, "view app.active_users");

    [Fact]
    public void Read_ViewAdd_AppendsCommentSuffix()
        => ShouldHaveLine(WithView(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("active_users"), ChangeKind.Add,
                Definition: new View(new SqlIdentifier("active_users"), new SqlText("SELECT 1")), Comment: new ValueChange<string>(null, "active"))), ChangeKind.Add, "view app.active_users (\"active\")");

    [Fact]
    public void Read_ViewBodyReplace_EmitsModifyHeader()
        => ShouldHaveLine(WithView(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("daily_totals"), ChangeKind.Modify,
                Definition: new View(new SqlIdentifier("daily_totals"), new SqlText("SELECT sum(x) FROM app.sales")))), ChangeKind.Modify, "view app.daily_totals");

    [Fact]
    public void Read_ViewCommentOnlyChange_EmitsCommentDiff()
        => ShouldHaveLine(WithView(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("summary"), ChangeKind.Modify, Comment: new ValueChange<string>("old", "new"))), ChangeKind.Modify, "view app.summary comment: \"old\" → \"new\"");

    [Fact]
    public void Read_ViewRename_EmitsArrowWithSchemaQualifier()
        => ShouldHaveLine(WithView(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("report"), ChangeKind.Modify, RenamedFrom: new SqlIdentifier("legacy_report"))), ChangeKind.Modify, "view app.legacy_report → report");

    [Fact]
    public void Read_ViewToMaterializedFlip_EmitsLabelTransition()
        => ShouldHaveLine(WithView(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("totals"), ChangeKind.Modify,
                Definition: new View(new SqlIdentifier("totals"), new SqlText("SELECT 1"), IsMaterialized: true), IsMaterialized: true,
                Materialized: new ValueChange<bool>(false, true), RequiresRecreate: true)),
            ChangeKind.Modify, "view → materialized view app.totals");

    [Fact]
    public void Read_MaterializedToViewFlip_EmitsLabelTransition()
        => ShouldHaveLine(WithView(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("totals"), ChangeKind.Modify,
                Definition: new View(new SqlIdentifier("totals"), new SqlText("SELECT 1")),
                Materialized: new ValueChange<bool>(true, false), RequiresRecreate: true)),
            ChangeKind.Modify, "materialized view → view app.totals");

    [Fact]
    public void Read_ViewRemove_EmitsRemoveHeader()
        => ShouldHaveLine(WithView(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("stale_view"), ChangeKind.Remove)), ChangeKind.Remove, "view app.stale_view");

    // -------------------------------------------------------------------------
    // Document shape
    // -------------------------------------------------------------------------

    [Fact]
    public void Read_CarriesChangeKindOnContentLines_WithoutMarkersInText()
    {
        var diff = WithTable(Table("users", ChangeKind.Add, columns: [AddColumn(new Column(new SqlIdentifier("id"), SqlType.Int))]));

        var document = Read(diff);

        // The table header is a depth-0 line tagged Add; its text carries no marker glyph or indentation.
        var header = document.Lines.Single(line => line.Text.StartsWith("table "));
        header.Kind.ShouldBe(ChangeKind.Add);
        header.Depth.ShouldBe(0);
        header.Text.ShouldBe("table app.users");

        // The column is a detail beneath it: same kind, one level deeper, still marker-free.
        var column = document.Lines.Single(line => line.Text.StartsWith("id "));
        column.Kind.ShouldBe(ChangeKind.Add);
        column.Depth.ShouldBe(1);
        column.Text.ShouldNotContain("+");
    }

    [Fact]
    public void Read_SpacerLinesAreKindlessAndEmpty()
    {
        var document = Read(DiffOf([Schema("app", ChangeKind.Add)]));

        // Every blank spacer is a kindless, empty line — a formatter renders or ignores it as it sees fit.
        document.Lines.Where(line => line.Kind is null).ShouldAllBe(line => line.Text == "");
    }

    // The index of the first line matching the predicate, or -1.
    private static int IndexOf(IReadOnlyList<DiffLine> lines, Func<DiffLine, bool> predicate)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (predicate(lines[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
