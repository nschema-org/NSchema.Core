using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Model;
using NSchema.Diff.Model.Columns;
using NSchema.Diff.Model.Services;
using NSchema.Diff.Model.Tables;
using NSchema.Diff.Model.Views;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Indexes;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Project.Model.Directives;
using NSchema.Project.Nsql;
using DatabaseComparer = NSchema.Diff.Model.Services.DatabaseComparer;

namespace NSchema.Tests.Diff;

/// <summary>
/// Covers the structured-diff projection the comparer now produces directly (formerly the responsibility of
/// DefaultDiffBuilder), driven from realistic schema inputs.
/// </summary>
public partial class DatabaseComparerTests
{
    private readonly DatabaseComparer _sut = new(NullLogger<DatabaseComparer>.Instance);

    private static Database Db(params Schema[] schemas) => new Database([.. schemas]);

    /// <summary>
    /// Compares two observations, optionally steered by directives (none = drift-style compare), running the
    /// full Align → Compare → Decorate pipeline the project comparer orchestrates.
    /// </summary>
    private DatabaseDiff Compare(Database current, Database desired, ProjectDirectives? directives = null)
    {
        var effective = directives ?? ProjectDirectives.Empty;
        var aligned = DatabaseAligner.Align(current, desired, effective);
        var diff = _sut.Compare(aligned.Require(), desired);
        return ChangeScriptDecorator.Decorate(diff, effective.ChangeScripts);
    }

    /// <summary>An address in the <c>app</c> schema.</summary>
    private static ObjectAddress App(string name) => new(new SqlIdentifier("app"), new SqlIdentifier(name));

    /// <summary>Diffs two single-table <c>app</c> schemas, returning the table diff (null when unchanged).</summary>
    private TableDiff? DiffTable(Table current, Table desired, ProjectDirectives? directives = null) =>
        Compare(Db(new Schema(new SqlIdentifier("app"), tables: [current])), Db(new Schema(new SqlIdentifier("app"), tables: [desired])), directives)
        .Schemas.SingleOrDefault()?.Tables.SingleOrDefault();

    /// <summary>Diffs two single-column <c>app.t</c> tables, returning the column diff (null when unchanged).</summary>
    private ColumnDiff? DiffColumn(Column current, Column desired, ProjectDirectives? directives = null) =>
        DiffTable(new Table(new SqlIdentifier("t"), columns: [current]), new Table(new SqlIdentifier("t"), columns: [desired]), directives)?.Columns.SingleOrDefault();

    /// <summary>Diffs two <c>app</c> schemas holding the given views, returning the single view diff (null when unchanged).</summary>
    private ViewDiff? DiffViews(IReadOnlyList<View> current, IReadOnlyList<View> desired, ProjectDirectives? directives = null) =>
        Compare(Db(new Schema(new SqlIdentifier("app"), views: [.. current])), Db(new Schema(new SqlIdentifier("app"), views: [.. desired])), directives)
        .Schemas.SingleOrDefault()?.Views.SingleOrDefault();

    /// <summary>Directives renaming table <c>app.&lt;from&gt;</c> to <paramref name="to"/>.</summary>
    private static ProjectDirectives TableRename(string from, string to) =>
        new(ObjectRenames: [new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App(from)), new SqlIdentifier(to))]);

    /// <summary>Directives renaming column <c>app.t.&lt;from&gt;</c> to <paramref name="to"/>.</summary>
    private static ProjectDirectives ColumnRename(string from, string to, string table = "t") =>
        new(MemberRenames: [new MemberRenameDirective(new MemberAddress(new SqlIdentifier("app"), new SqlIdentifier(table), new SqlIdentifier(from)), new SqlIdentifier(to))]);

    /// <summary>Builds a view with dependencies derived from its body, exactly as the DDL parser would.</summary>
    private static View View(string name, string body, string? comment = null) =>
        new(new SqlIdentifier(name), new SqlText(body), ViewDependencyExtractor.Extract(body, new SqlIdentifier("app"))) { Comment = comment };

    [Fact]
    public void Compare_BothEmpty_ProducesEmptyDiff()
    {
        var diff = Compare(Db(), Db());

        diff.IsEmpty.ShouldBeTrue();
        diff.Schemas.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_NestsTablesUnderSchema_OrderedByName()
    {
        var current = Db(new Schema(new SqlIdentifier("app"), tables:
        [
            new Table(new SqlIdentifier("orders"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            new Table(new SqlIdentifier("audit_log"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
        ]));
        var desired = Db(new Schema(new SqlIdentifier("app"), tables:
        [
            new Table(new SqlIdentifier("orders"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int), new Column(new SqlIdentifier("shipped_at"), SqlType.DateTimeOffset)]),
        ]));

        var schema = Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Name.ShouldBe("app");
        schema.Kind.ShouldBeNull(); // only its tables changed
        schema.Tables.Select(t => t.Name).ShouldBe(["audit_log", "orders"]); // ordered by name
        schema.Tables.Single(t => t.Name.Value.Equals("orders")).Kind.ShouldBe(ChangeKind.Modify);
        schema.Tables.Single(t => t.Name.Value.Equals("audit_log")).Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_Summary_CountsEveryChangedElementByKind()
    {
        var current = Db(new Schema(new SqlIdentifier("app"), tables:
        [
            new Table(new SqlIdentifier("orders"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
            new Table(new SqlIdentifier("audit_log"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]),
        ]));
        var desired = Db(
            new Schema(new SqlIdentifier("app"), tables:
            [
                new Table(new SqlIdentifier("orders"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int), new Column(new SqlIdentifier("shipped_at"), SqlType.DateTimeOffset)]),
            ]),
            new Schema(new SqlIdentifier("reporting")));

        // reporting schema (Add) + shipped_at column (Add); orders table (Modify); audit_log table (Remove).
        Compare(current, desired).GetSummary().ShouldBe(new DiffSummary(Added: 2, Modified: 1, Removed: 1));
    }

    [Fact]
    public void Compare_TableChangeWithoutSchemaChange_LeavesSchemaKindNull()
    {
        var current = Db(new Schema(new SqlIdentifier("app"), tables: [new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])]));
        var desired = Db(new Schema(new SqlIdentifier("app"), tables:
            [new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int), new Column(new SqlIdentifier("email"), SqlType.Text)])]));

        var schema = Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Kind.ShouldBeNull();
        schema.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }

    [Fact]
    public void Compare_CreateTable_AddsEveryColumnWithDefinitionAndFoldedComment()
    {
        var current = Db(new Schema(new SqlIdentifier("app")));
        var desired = Db(new Schema(new SqlIdentifier("app"), tables:
        [
            new Table(new SqlIdentifier("users"), columns:
            [
                new Column(new SqlIdentifier("id"), SqlType.Int, isNullable: false),
                new Column(new SqlIdentifier("email"), SqlType.Text, isNullable: false) { Comment = "login" },
            ]),
        ]));

        var table = Compare(current, desired).Schemas.Single().Tables.Single();

        table.Kind.ShouldBe(ChangeKind.Add);
        table.Columns.Select(c => c.Name).ShouldBe(["id", "email"]);
        table.Columns.ShouldAllBe(c => c.Kind == ChangeKind.Add && c.Definition != null);
        table.Columns.Single(c => c.Name.Value.Equals("email")).Comment.ShouldBe(new ValueChange<string>(null, "login"));
        table.Columns.Single(c => c.Name.Value.Equals("id")).Comment.ShouldBeNull();
    }

    [Fact]
    public void Compare_MergesMultipleChangesToOneColumnIntoASingleDiff()
    {
        var current = Db(new Schema(new SqlIdentifier("app"), tables:
            [new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text, isNullable: false) { Comment = "old" }])]));
        var desired = Db(new Schema(new SqlIdentifier("app"), tables:
            [new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("email"), SqlType.Text, isNullable: true) { Comment = "new" }])]));

        var column = Compare(current, desired).Schemas.Single().Tables.Single().Columns.ShouldHaveSingleItem();

        column.Name.ShouldBe("email");
        column.Kind.ShouldBe(ChangeKind.Modify);
        column.Nullability.ShouldBe(new ValueChange<bool>(false, true));
        column.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_GroupsIndexesConstraintsAndGrantsUnderTheirTable()
    {
        DatabaseMemberCollection<Column> Columns() => [new Column(new SqlIdentifier("id"), SqlType.Int), new Column(new SqlIdentifier("user_id"), SqlType.Int)];
        var current = Db(new Schema(new SqlIdentifier("app"), tables: [new Table(new SqlIdentifier("orders"), columns: Columns())]));
        var desired = Db(new Schema(new SqlIdentifier("app"), tables:
        [
            new Table(new SqlIdentifier("orders"),
                columns: Columns(),
                primaryKey: new PrimaryKey(new SqlIdentifier("orders_pkey"), [new SqlIdentifier("id")]),
                foreignKeys: [new ForeignKey(new SqlIdentifier("orders_user_fk"), [new SqlIdentifier("user_id")], new SqlIdentifier("app"), new SqlIdentifier("users"), [new SqlIdentifier("id")])],
                uniqueConstraints: [new UniqueConstraint(new SqlIdentifier("orders_user_uq"), [new SqlIdentifier("user_id")])],
                checkConstraints: [new CheckConstraint(new SqlIdentifier("orders_id_chk"), new SqlText("id > 0"))],
                indexes: [new TableIndex(new SqlIdentifier("orders_user_ix"), ["user_id"])],
                grants: [new TableGrant(new SqlIdentifier("reader"), TablePrivilege.Insert)]),
        ]));

        var table = Compare(current, desired).Schemas.Single().Tables.Single();

        table.PrimaryKey.Select(c => (c.Kind, c.Name.Value)).ShouldBe([(ChangeKind.Add, "orders_pkey")]);
        table.ForeignKeys.Select(c => (c.Kind, c.Name.Value)).ShouldBe([(ChangeKind.Add, "orders_user_fk")]);
        table.UniqueConstraints.Select(c => (c.Kind, c.Name.Value)).ShouldBe([(ChangeKind.Add, "orders_user_uq")]);
        table.Checks.Select(c => (c.Kind, c.Name.Value)).ShouldBe([(ChangeKind.Add, "orders_id_chk")]);
        table.Indexes.ShouldHaveSingleItem().Name.ShouldBe("orders_user_ix");
        var grant = table.Grants.ShouldHaveSingleItem();
        grant.Role.ShouldBe("reader");
        grant.Privileges.ShouldBe(TablePrivilege.Insert);
    }

    [Fact]
    public void Compare_FoldsSchemaRenameCommentAndGrantsIntoSchemaDiff()
    {
        var current = Db(new Schema(new SqlIdentifier("app_old"), grants: [new SchemaGrant(new SqlIdentifier("writer"))]));
        var desired = Db(new Schema(new SqlIdentifier("app"), grants: [new SchemaGrant(new SqlIdentifier("reader"))]) { Comment = "new comment" });
        var directives = new ProjectDirectives(
            SchemaRenames: [new SchemaRenameDirective(new SqlIdentifier("app_old"), new SqlIdentifier("app"))]);

        var schema = Compare(current, desired, directives).Schemas.ShouldHaveSingleItem();

        schema.Kind.ShouldBe(ChangeKind.Modify);
        schema.RenamedFrom.ShouldBe("app_old");
        schema.Comment.ShouldBe(new ValueChange<string>(null, "new comment"));
        schema.Grants.ShouldBe([
            new GrantChange(ChangeKind.Remove, new SqlIdentifier("writer"), null),
            new GrantChange(ChangeKind.Add, new SqlIdentifier("reader"), null),
        ]);
    }

    // -------------------------------------------------------------------------
    // Schema-level add / remove / sort / no-op
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_IdenticalSchemas_ProduceNoDiff()
    {
        var schema = new Schema(new SqlIdentifier("app"), tables: [new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])]);

        Compare(Db(schema), Db(schema)).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Compare_SchemaInCurrentButNotDesired_IsRemoved()
    {
        var current = Db(new Schema(new SqlIdentifier("app")), new Schema(new SqlIdentifier("legacy")));
        var desired = Db(new Schema(new SqlIdentifier("app")));

        var schema = Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Name.ShouldBe("legacy");
        schema.Kind.ShouldBe(ChangeKind.Remove);
        schema.Tables.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_RemovedSchemaWithTables_CarriesTheirRemovals()
    {
        // A removed schema must take its contained objects with it (rather than relying on DROP SCHEMA CASCADE), so
        // the diff carries a Remove for each nested table, ordered by name.
        var legacy = new Schema(new SqlIdentifier("legacy"), tables: [new Table(new SqlIdentifier("widgets")), new Table(new SqlIdentifier("gadgets"))]);
        var current = Db(new Schema(new SqlIdentifier("app")), legacy);
        var desired = Db(new Schema(new SqlIdentifier("app")));

        var schema = Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Kind.ShouldBe(ChangeKind.Remove);
        schema.Tables.Select(t => (t.Name.Value, t.Kind)).ShouldBe([("gadgets", ChangeKind.Remove), ("widgets", ChangeKind.Remove)]);
    }

    [Fact]
    public void Compare_NewSchema_FoldsCommentGrantsAndTablesWithDefinition()
    {
        var current = Db();
        var desired = Db(new Schema(new SqlIdentifier("reporting"),

            grants: [new SchemaGrant(new SqlIdentifier("reader"))],
            tables: [new Table(new SqlIdentifier("metrics"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])])
        { Comment = "analytics" });

        var schema = Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Kind.ShouldBe(ChangeKind.Add);
        schema.Comment.ShouldBe(new ValueChange<string>(null, "analytics"));
        schema.Grants.ShouldHaveSingleItem().ShouldBe(new GrantChange(ChangeKind.Add, new SqlIdentifier("reader"), null));
        var table = schema.Tables.ShouldHaveSingleItem();
        table.Kind.ShouldBe(ChangeKind.Add);
        table.Definition.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_OrdersResultSchemasByName()
    {
        var diff = Compare(Db(), Db(new Schema(new SqlIdentifier("zeta")), new Schema(new SqlIdentifier("alpha"))));

        diff.Schemas.Select(s => s.Name).ShouldBe(["alpha", "zeta"]);
    }

    [Fact]
    public void Compare_CaseVariantNames_MatchAsTheSameObject()
    {
        // Identifiers are case-insensitive: an introspected "Users" and a declared "users" are the same
        // table, not a drop-and-recreate pair.
        var current = Db(new Schema(new SqlIdentifier("App"), tables: [new Table(new SqlIdentifier("Users"), columns: [new Column(new SqlIdentifier("ID"), SqlType.Int)])]));
        var desired = Db(new Schema(new SqlIdentifier("app"), tables: [new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])]));

        var diff = Compare(current, desired);

        diff.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Compare_CaseVariantColumnReferences_ProduceNoDiff()
    {
        // References inside definitions (primary-key and index column lists) are identifiers too, so a
        // casing difference between the introspected and declared spelling is not a change.
        Table Build(string id, string email) => new(new SqlIdentifier("users"),
            columns: [new Column(new SqlIdentifier(id), SqlType.Int), new Column(new SqlIdentifier(email), SqlType.Text)],
            primaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier(id)]),
            indexes: [new TableIndex(new SqlIdentifier("users_email_ix"), [email])]);

        var diff = DiffTable(Build("ID", "Email"), Build("id", "email"));

        diff.ShouldBeNull();
    }

    [Fact]
    public void Compare_DirectivesUnderASchemaRename_AddressCurrentNames()
    {
        // Everything is renamed at once: schema sales→core, table users→people, column name→full_name.
        // Every directive addresses current reality (sales.users.name), and the comparer resolves the nested
        // lookups by the current names of each matched pair.
        var current = Db(new Schema(new SqlIdentifier("sales"), tables:
            [new Table(new SqlIdentifier("users"), columns: [new Column(new SqlIdentifier("name"), SqlType.Text)])]));
        var desired = Db(new Schema(new SqlIdentifier("core"), tables:
            [new Table(new SqlIdentifier("people"), columns: [new Column(new SqlIdentifier("full_name"), SqlType.Text)])]));
        var sales = new SqlIdentifier("sales");
        var directives = new ProjectDirectives(
            SchemaRenames: [new SchemaRenameDirective(sales, new SqlIdentifier("core"))],
            ObjectRenames: [new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, new ObjectAddress(sales, new SqlIdentifier("users"))), new SqlIdentifier("people"))],
            MemberRenames: [new MemberRenameDirective(new MemberAddress(sales, new SqlIdentifier("users"), new SqlIdentifier("name")), new SqlIdentifier("full_name"))]);

        var schema = Compare(current, desired, directives).Schemas.ShouldHaveSingleItem();

        schema.RenamedFrom.ShouldBe("sales");
        var table = schema.Tables.ShouldHaveSingleItem();
        table.RenamedFrom.ShouldBe("users");
        var column = table.Columns.ShouldHaveSingleItem();
        column.RenamedFrom.ShouldBe("name");
        column.Name.ShouldBe("full_name");
    }
}
