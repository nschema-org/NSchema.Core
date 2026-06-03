using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Model;
using NSchema.Schema.Model;
using DefaultSchemaComparer = NSchema.Diff.DefaultSchemaComparer;

namespace NSchema.Tests.Migration;

/// <summary>
/// Covers the structured-diff projection the comparer now produces directly (formerly the responsibility of
/// DefaultDiffBuilder), driven from realistic schema inputs.
/// </summary>
public class DefaultSchemaComparerDiffTests
{
    private readonly DefaultSchemaComparer _sut = new(NullLogger<DefaultSchemaComparer>.Instance);

    private static DatabaseSchema Db(params SchemaDefinition[] schemas) => DatabaseSchema.Create(schemas);

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
    public void Compare_Summary_CountsChangedSchemasAndTablesByKind()
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

        _sut.Compare(current, desired).Summary.ShouldBe(new DiffSummary(Added: 1, Modified: 1, Removed: 1));
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
}
