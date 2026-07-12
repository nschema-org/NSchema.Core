using NSchema.Diff.Domain.Models.Columns;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Diff.Domain.Models.Views;
using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Models;
using NSchema.Project.Ddl;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Constraints;
using NSchema.Project.Domain.Models.Indexes;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Views;

namespace NSchema.Tests.Diff;

/// <summary>
/// Covers the structured-diff projection the comparer now produces directly (formerly the responsibility of
/// DefaultDiffBuilder), driven from realistic schema inputs.
/// </summary>
public partial class SchemaComparerTests
{
    private readonly SchemaComparer _sut = new(NullLogger<SchemaComparer>.Instance);

    private static DatabaseSchema Db(params SchemaDefinition[] schemas) => new DatabaseSchema(schemas);

    /// <summary>Diffs two single-table <c>app</c> schemas, returning the table diff (null when unchanged).</summary>
    private TableDiff? DiffTable(Table current, Table desired) => _sut
        .Compare(Db(new SchemaDefinition("app", Tables: [current])), Db(new SchemaDefinition("app", Tables: [desired])))
        .Schemas.SingleOrDefault()?.Tables.SingleOrDefault();

    /// <summary>Diffs two single-column <c>app.t</c> tables, returning the column diff (null when unchanged).</summary>
    private ColumnDiff? DiffColumn(Column current, Column desired) =>
        DiffTable(new Table("t", Columns: [current]), new Table("t", Columns: [desired]))?.Columns.SingleOrDefault();

    /// <summary>Diffs two <c>app</c> schemas holding the given views, returning the single view diff (null when unchanged).</summary>
    private ViewDiff? DiffViews(IReadOnlyList<View> current, IReadOnlyList<View> desired) => _sut
        .Compare(Db(new SchemaDefinition("app", Views: current)), Db(new SchemaDefinition("app", Views: desired)))
        .Schemas.SingleOrDefault()?.Views.SingleOrDefault();

    /// <summary>Builds a view with dependencies derived from its body, exactly as the DDL parser would.</summary>
    private static View View(string name, string body, string? comment = null, string? oldName = null) =>
        new(name, body, oldName, comment, ViewDependencyExtractor.Extract(body, "app"));

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
        var current = Db(new SchemaDefinition("app", Tables:
        [
            new Table("orders", Columns: [new Column("id", SqlType.Int)]),
            new Table("audit_log", Columns: [new Column("id", SqlType.Int)]),
        ]));
        var desired = Db(new SchemaDefinition("app", Tables:
        [
            new Table("orders", Columns: [new Column("id", SqlType.Int), new Column("shipped_at", SqlType.DateTimeOffset)]),
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
        var current = Db(new SchemaDefinition("app", Tables:
        [
            new Table("orders", Columns: [new Column("id", SqlType.Int)]),
            new Table("audit_log", Columns: [new Column("id", SqlType.Int)]),
        ]));
        var desired = Db(
            new SchemaDefinition("app", Tables:
            [
                new Table("orders", Columns: [new Column("id", SqlType.Int), new Column("shipped_at", SqlType.DateTimeOffset)]),
            ]),
            new SchemaDefinition("reporting"));

        // reporting schema (Add) + shipped_at column (Add); orders table (Modify); audit_log table (Remove).
        _sut.Compare(current, desired).GetSummary().ShouldBe(new DiffSummary(Added: 2, Modified: 1, Removed: 1));
    }

    [Fact]
    public void Compare_TableChangeWithoutSchemaChange_LeavesSchemaKindNull()
    {
        var current = Db(new SchemaDefinition("app", Tables: [new Table("users", Columns: [new Column("id", SqlType.Int)])]));
        var desired = Db(new SchemaDefinition("app", Tables:
            [new Table("users", Columns: [new Column("id", SqlType.Int), new Column("email", SqlType.Text)])]));

        var schema = _sut.Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Kind.ShouldBeNull();
        schema.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }

    [Fact]
    public void Compare_CreateTable_AddsEveryColumnWithDefinitionAndFoldedComment()
    {
        var current = Db(new SchemaDefinition("app"));
        var desired = Db(new SchemaDefinition("app", Tables:
        [
            new Table("users", Columns:
            [
                new Column("id", SqlType.Int, IsNullable: false),
                new Column("email", SqlType.Text, IsNullable: false, Comment: "login"),
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
        var current = Db(new SchemaDefinition("app", Tables:
            [new Table("users", Columns: [new Column("email", SqlType.Text, IsNullable: false, Comment: "old")])]));
        var desired = Db(new SchemaDefinition("app", Tables:
            [new Table("users", Columns: [new Column("email", SqlType.Text, IsNullable: true, Comment: "new")])]));

        var column = _sut.Compare(current, desired).Schemas.Single().Tables.Single().Columns.ShouldHaveSingleItem();

        column.Name.ShouldBe("email");
        column.Kind.ShouldBe(ChangeKind.Modify);
        column.Nullability.ShouldBe(new ValueChange<bool>(false, true));
        column.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_GroupsIndexesConstraintsAndGrantsUnderTheirTable()
    {
        var columns = new[] { new Column("id", SqlType.Int), new Column("user_id", SqlType.Int) };
        var current = Db(new SchemaDefinition("app", Tables: [new Table("orders", Columns: columns)]));
        var desired = Db(new SchemaDefinition("app", Tables:
        [
            new Table("orders",
                Columns: columns,
                PrimaryKey: new PrimaryKey("orders_pkey", ["id"]),
                ForeignKeys: [new ForeignKey("orders_user_fk", ["user_id"], "app", "users", ["id"])],
                UniqueConstraints: [new UniqueConstraint("orders_user_uq", ["user_id"])],
                CheckConstraints: [new CheckConstraint("orders_id_chk", "id > 0")],
                Indexes: [new TableIndex("orders_user_ix", ["user_id"])],
                Grants: [new TableGrant("reader", TablePrivilege.Insert)]),
        ]));

        var table = _sut.Compare(current, desired).Schemas.Single().Tables.Single();

        table.PrimaryKey.Select(c => (c.Kind, c.Name)).ShouldBe([(ChangeKind.Add, "orders_pkey")]);
        table.ForeignKeys.Select(c => (c.Kind, c.Name)).ShouldBe([(ChangeKind.Add, "orders_user_fk")]);
        table.UniqueConstraints.Select(c => (c.Kind, c.Name)).ShouldBe([(ChangeKind.Add, "orders_user_uq")]);
        table.Checks.Select(c => (c.Kind, c.Name)).ShouldBe([(ChangeKind.Add, "orders_id_chk")]);
        table.Indexes.ShouldHaveSingleItem().Name.ShouldBe("orders_user_ix");
        var grant = table.Grants.ShouldHaveSingleItem();
        grant.Role.ShouldBe("reader");
        grant.Privileges.ShouldBe(TablePrivilege.Insert);
    }

    [Fact]
    public void Compare_FoldsSchemaRenameCommentAndGrantsIntoSchemaDiff()
    {
        var current = Db(new SchemaDefinition("app_old", Grants: [new SchemaGrant("writer")]));
        var desired = Db(new SchemaDefinition("app", OldName: "app_old", Comment: "new comment", Grants: [new SchemaGrant("reader")]));

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
        var schema = new SchemaDefinition("app", Tables: [new Table("users", Columns: [new Column("id", SqlType.Int)])]);

        _sut.Compare(Db(schema), Db(schema)).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Compare_SchemaInCurrentButNotDesired_IsRemoved()
    {
        var current = Db(new SchemaDefinition("app"), new SchemaDefinition("legacy"));
        var desired = Db(new SchemaDefinition("app"));

        var schema = _sut.Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Name.ShouldBe("legacy");
        schema.Kind.ShouldBe(ChangeKind.Remove);
        schema.Tables.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_RemovedSchemaWithTables_CarriesTheirRemovals()
    {
        // A removed schema must take its contained objects with it (rather than relying on DROP SCHEMA CASCADE), so
        // the diff carries a Remove for each nested table, ordered by name.
        var legacy = new SchemaDefinition("legacy", Tables: [new Table("widgets"), new Table("gadgets")]);
        var current = Db(new SchemaDefinition("app"), legacy);
        var desired = Db(new SchemaDefinition("app"));

        var schema = _sut.Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Kind.ShouldBe(ChangeKind.Remove);
        schema.Tables.Select(t => (t.Name, t.Kind)).ShouldBe([("gadgets", ChangeKind.Remove), ("widgets", ChangeKind.Remove)]);
    }

    [Fact]
    public void Compare_NewSchema_FoldsCommentGrantsAndTablesWithDefinition()
    {
        var current = Db();
        var desired = Db(new SchemaDefinition("reporting",
            Comment: "analytics",
            Grants: [new SchemaGrant("reader")],
            Tables: [new Table("metrics", Columns: [new Column("id", SqlType.Int)])]));

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
        var diff = _sut.Compare(Db(), Db(new SchemaDefinition("zeta"), new SchemaDefinition("alpha")));

        diff.Schemas.Select(s => s.Name).ShouldBe(["alpha", "zeta"]);
    }
}
