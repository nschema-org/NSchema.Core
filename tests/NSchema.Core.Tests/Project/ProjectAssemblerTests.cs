using NSchema.Model;
using NSchema.Project.Model.Services;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project;

/// <summary>
/// Cross-file assembly: one accumulator spans every document, so declarations merge, dedup, and resolve
/// project-wide — the file boundary is organization, not semantics.
/// </summary>
public sealed class ProjectAssemblerTests
{
    private static NsqlDiagnostic SingleError(Result<NSchema.Project.Model.Directives.ProjectDefinition> result)
        => result.Errors.ShouldHaveSingleItem().ShouldBeOfType<NsqlDiagnostic>();

    // ── Aggregation across files ──────────────────────────────────────────────

    [Fact]
    public void Assemble_DistinctSchemasAcrossFiles_ProducesAllSchemas()
    {
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int);",
            "CREATE SCHEMA admin; CREATE TABLE admin.roles (id int);").Require();

        result.Database.Schemas.Select(s => s.Name).ShouldBe(["app", "admin"]);
    }

    [Fact]
    public void Assemble_SameSchemaAcrossFiles_MergesObjects()
    {
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int); CREATE VIEW app.active AS SELECT 1 FROM app.users;",
            "CREATE TABLE app.posts (id int); CREATE VIEW app.recent AS SELECT 1 FROM app.posts;").Require();

        var schema = result.Database.Schemas.ShouldHaveSingleItem();
        schema.Tables.Select(t => t.Name).ShouldBe(["users", "posts"]);
        schema.Views.Select(v => v.Name).ShouldBe(["active", "recent"]);
    }

    [Fact]
    public void Assemble_SchemaDeclaredInOneFile_CarriesItsCommentToTheMerge()
    {
        var result = TestNsqlParser.Assemble(
            "--- App schema\nCREATE SCHEMA app;",
            "CREATE TABLE app.posts (id int);").Require();

        var schema = result.Database.Schemas.ShouldHaveSingleItem();
        schema.Comment.ShouldBe("App schema");
        schema.Tables.ShouldHaveSingleItem().Name.ShouldBe("posts");
    }

    [Fact]
    public void Assemble_SchemaGrantsAcrossFiles_UnionAndDedup()
    {
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; GRANT USAGE ON SCHEMA app TO app_user;",
            "GRANT USAGE ON SCHEMA app TO reporting; GRANT USAGE ON SCHEMA app TO app_user;").Require();

        result.Database.Schemas.Single().Grants.Select(g => g.Role).ShouldBe(["app_user", "reporting"], ignoreOrder: true);
    }

    // ── Cross-file duplicates — positioned at the offending re-declaration ────

    [Fact]
    public void Assemble_DuplicateTableAcrossFiles_IsAnError()
    {
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int);",
            "CREATE TABLE app.users (id int);");

        var error = SingleError(result);
        error.ShouldBe(ProjectDiagnostics.ObjectAlreadyDeclared(
            ObjectKind.Table, "app", "users", error.Position) with
        { File = "file2.sql" });
        error.Position.Line.ShouldBe(1);
    }

    [Fact]
    public void Assemble_DuplicateViewAcrossFiles_IsAnError()
    {
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE VIEW app.v AS SELECT 1 FROM app.t;",
            "CREATE VIEW app.v AS SELECT 2 FROM app.t;");

        var error = SingleError(result);
        error.ShouldBe(ProjectDiagnostics.ObjectAlreadyDeclared(
            ObjectKind.View, "app", "v", error.Position) with
        { File = "file2.sql" });
    }

    [Fact]
    public void Assemble_DuplicateRoutineAcrossFiles_IsAnError()
    {
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE FUNCTION app.f() RETURNS int AS $$ SELECT 1 $$;",
            "CREATE FUNCTION app.f() RETURNS int AS $$ SELECT 2 $$;");

        SingleError(result).Message.ShouldContain("Routine 'app.f' is already declared");
    }

    [Fact]
    public void Assemble_FunctionAndProcedureAcrossFiles_ShareOneNameSpace()
    {
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE FUNCTION app.r() RETURNS int AS $$ SELECT 1 $$;",
            "CREATE PROCEDURE app.r() AS $$ SELECT 1 $$;");

        SingleError(result).Message.ShouldContain("share one name space");
    }

    [Fact]
    public void Assemble_DuplicateSchemaDeclarationAcrossFiles_IsAnError()
    {
        // Only the declaration is unique: objects land in a schema from any file without redeclaring it,
        // so a second CREATE SCHEMA is a duplicate wherever it lives.
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app;",
            "CREATE SCHEMA app;");

        var error = SingleError(result);
        error.ShouldBe(ProjectDiagnostics.SchemaAlreadyDeclared("app", error.Position) with
        { File = "file2.sql" });
    }

    [Fact]
    public void Assemble_DuplicateExtensionAcrossFiles_IsAnError()
    {
        var result = TestNsqlParser.Assemble(
            "CREATE EXTENSION citext;",
            "CREATE EXTENSION citext;");

        var error = SingleError(result);
        error.ShouldBe(ProjectDiagnostics.ExtensionAlreadyDeclared("citext", error.Position) with
        { File = "file2.sql" });
    }

    // ── Standalone statements resolve project-wide, not per file ──────────────

    [Fact]
    public void Assemble_TriggerInAnotherFile_AttachesToItsTable()
    {
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int);",
            "CREATE TRIGGER audit AFTER INSERT ON app.users EXECUTE FUNCTION app.f();").Require();

        var table = result.Database.Schemas.Single().Tables.Single();
        table.Triggers.ShouldHaveSingleItem().Name.ShouldBe("audit");
    }

    [Fact]
    public void Assemble_TableGrantInAnotherFile_AttachesToItsTable()
    {
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int);",
            "GRANT SELECT ON app.users TO readers;").Require();

        var table = result.Database.Schemas.Single().Tables.Single();
        table.Grants.ShouldHaveSingleItem().Role.ShouldBe("readers");
    }

    [Fact]
    public void Assemble_SameTableGrantInTwoFiles_IsOneGrant()
    {
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int); GRANT SELECT ON app.users TO readers;",
            "GRANT SELECT ON app.users TO readers;").Require();

        result.Database.Schemas.Single().Tables.Single().Grants.ShouldHaveSingleItem();
    }

    [Fact]
    public void Assemble_StandaloneIndexInAnotherFile_AttachesToItsTable()
    {
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int, email text);",
            "CREATE INDEX users_email_ix ON app.users (email);").Require();

        var table = result.Database.Schemas.Single().Tables.Single();
        table.Indexes.ShouldHaveSingleItem().Name.ShouldBe("users_email_ix");
    }

    [Fact]
    public void Assemble_UnknownGrantTable_CarriesTheGrantingFile()
    {
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int);",
            "GRANT SELECT ON app.ghost TO readers;");

        var error = SingleError(result);
        error.ShouldBe(ProjectDiagnostics.UnknownGrantTable(
            "app", "ghost", error.Position) with
        { File = "file2.sql" });
    }
}
