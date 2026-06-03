using NSchema.Migration.Diff;
using NSchema.Migration.Diff.Model;
using NSchema.Migration.Plan;
using NSchema.Schema;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Migration;

public class DefaultDiffBuilderTests
{
    private readonly DefaultDiffBuilder _sut = new();

    [Fact]
    public void Build_EmptyPlan_ProducesEmptyDiff()
    {
        var diff = _sut.Build(TestData.EmptyPlan);

        diff.IsEmpty.ShouldBeTrue();
        diff.Schemas.ShouldBeEmpty();
    }

    [Fact]
    public void Build_NestsTablesUnderTheirSchema()
    {
        var plan = new MigrationPlan([
            new CreateSchema("app"),
            new AddColumn("app", "orders", Column.Create("shipped_at", SqlType.DateTimeOffset)),
            new DropTable("app", "audit_log"),
        ], DatabaseSchema.Create([]));

        var diff = _sut.Build(plan);

        var schema = diff.Schemas.ShouldHaveSingleItem();
        schema.Name.ShouldBe("app");
        schema.Kind.ShouldBe(ChangeKind.Add);
        schema.Tables.Select(t => t.Name).ShouldBe(["audit_log", "orders"]); // ordered by name
        schema.Tables.Single(t => t.Name == "orders").Kind.ShouldBe(ChangeKind.Modify);
        schema.Tables.Single(t => t.Name == "audit_log").Kind.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Build_Summary_CountsChangedSchemasAndTablesByKind()
    {
        var plan = new MigrationPlan([
            new CreateSchema("app"),
            new AddColumn("app", "orders", Column.Create("shipped_at", SqlType.DateTimeOffset)),
            new DropTable("app", "audit_log"),
        ], DatabaseSchema.Create([]));

        var diff = _sut.Build(plan);

        diff.Summary.ShouldBe(new DiffSummary(Added: 1, Modified: 1, Removed: 1));
    }

    [Fact]
    public void Build_Summary_IgnoresUnchangedContainerSchemas()
    {
        // The schema entity itself is unchanged (Kind == null); only its table changed, so the schema must
        // not contribute to the summary.
        var plan = new MigrationPlan([
            new AddColumn("app", "users", Column.Create("email", SqlType.Text)),
        ], DatabaseSchema.Create([]));

        _sut.Build(plan).Summary.ShouldBe(new DiffSummary(Added: 0, Modified: 1, Removed: 0));
    }

    [Fact]
    public void Build_TableChangeWithoutSchemaChange_LeavesSchemaKindNull()
    {
        var plan = new MigrationPlan([
            new AddColumn("app", "users", Column.Create("email", SqlType.Text)),
        ], DatabaseSchema.Create([]));

        var schema = _sut.Build(plan).Schemas.ShouldHaveSingleItem();

        schema.Kind.ShouldBeNull();
        schema.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }

    [Fact]
    public void Build_CreateTable_AddsEveryColumnWithDefinitionAndFoldedComment()
    {
        var plan = new MigrationPlan([
            new CreateTable("app", Table.Create("users", columns:
            [
                Column.Create("id", SqlType.Int, isNullable: false),
                Column.Create("email", SqlType.Text, isNullable: false),
            ])),
            new SetColumnComment("app", "users", "email", null, "login"),
        ], DatabaseSchema.Create([]));

        var table = _sut.Build(plan).Schemas.Single().Tables.Single();

        table.Kind.ShouldBe(ChangeKind.Add);
        table.Columns.Select(c => c.Name).ShouldBe(["id", "email"]);
        table.Columns.ShouldAllBe(c => c.Kind == ChangeKind.Add && c.Definition != null);
        table.Columns.Single(c => c.Name == "email").Comment.ShouldBe(new ValueChange<string>(null, "login"));
        table.Columns.Single(c => c.Name == "id").Comment.ShouldBeNull();
    }

    [Fact]
    public void Build_MergesMultipleChangesToOneColumnIntoASingleDiff()
    {
        var plan = new MigrationPlan([
            new AlterColumnNullability("app", "users", "email", false, true),
            new SetColumnComment("app", "users", "email", "old", "new"),
        ], DatabaseSchema.Create([]));

        var column = _sut.Build(plan).Schemas.Single().Tables.Single().Columns.ShouldHaveSingleItem();

        column.Name.ShouldBe("email");
        column.Kind.ShouldBe(ChangeKind.Modify);
        column.Nullability.ShouldBe(new ValueChange<bool>(false, true));
        column.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Build_GroupsIndexesConstraintsAndGrantsUnderTheirTable()
    {
        var plan = new MigrationPlan([
            new AddPrimaryKey("app", "orders", new PrimaryKey("orders_pkey", ["id"])),
            new AddForeignKey("app", "orders", ForeignKey.Create("orders_user_fk", ["user_id"], "app", "users", ["id"])),
            new CreateIndex("app", "orders", TableIndex.Create("orders_user_ix", ["user_id"])),
            new GrantTablePrivileges("app", "orders", "reader", TablePrivilege.Insert),
        ], DatabaseSchema.Create([]));

        var table = _sut.Build(plan).Schemas.Single().Tables.Single();

        table.Constraints.Select(c => (c.Type, c.Name)).ShouldBe(
            [(ConstraintType.PrimaryKey, "orders_pkey"), (ConstraintType.ForeignKey, "orders_user_fk")]);
        table.Indexes.ShouldHaveSingleItem().Name.ShouldBe("orders_user_ix");
        var grant = table.Grants.ShouldHaveSingleItem();
        grant.Role.ShouldBe("reader");
        grant.Privileges.ShouldBe(TablePrivilege.Insert);
    }

    [Fact]
    public void Build_SeparatesDeploymentScriptsByType()
    {
        var plan = new MigrationPlan([
            new RunScript(new Script("before.sql", "SELECT 1", ScriptType.PreDeployment)),
            new RunScript(new Script("after.sql", "SELECT 2", ScriptType.PostDeployment)),
        ], DatabaseSchema.Create([]));

        var diff = _sut.Build(plan);

        diff.PreDeploymentScripts.ShouldBe(["before.sql"]);
        diff.PostDeploymentScripts.ShouldBe(["after.sql"]);
        diff.Schemas.ShouldBeEmpty();
        diff.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Build_FoldsSchemaRenameAndCommentIntoSchemaDiff()
    {
        var plan = new MigrationPlan([
            new RenameSchema("app_old", "app"),
            new SetSchemaComment("app", null, "new comment"),
            new GrantSchemaUsage("app", "reader"),
            new RevokeSchemaUsage("app", "writer"),
        ], DatabaseSchema.Create([]));

        var schema = _sut.Build(plan).Schemas.ShouldHaveSingleItem();

        schema.Kind.ShouldBe(ChangeKind.Modify);
        schema.RenamedFrom.ShouldBe("app_old");
        schema.Comment.ShouldBe(new ValueChange<string>(null, "new comment"));
        schema.Grants.ShouldBe([
            new GrantChange(ChangeKind.Add, "reader", null),
            new GrantChange(ChangeKind.Remove, "writer", null),
        ]);
    }
}
