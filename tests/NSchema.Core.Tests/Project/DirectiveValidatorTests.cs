using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project;
using NSchema.Project.Model.Directives;
using NSchema.Project.Model.Services;

namespace NSchema.Tests.Project;

/// <summary>
/// The static directive rules — everything checkable against the declarations alone. Rules needing current
/// state (self-expiry) live with the differ.
/// </summary>
public sealed class DirectiveValidatorTests
{
    private static readonly SqlIdentifier _app = new("app");

    private static ObjectAddress App(string name) => new(_app, new SqlIdentifier(name));

    private static ProjectDefinition Project(ProjectDirectives directives, params Schema[] schemas) =>
        new(new Database(schemas), directives);

    private static Schema AppSchema(params Table[] tables) => new(_app, tables: tables);

    private static Table Table(string name, params string[] columns) =>
        new(new SqlIdentifier(name), columns: [.. columns.Select(c => new Column(new SqlIdentifier(c), SqlType.Int))]);

    private static IReadOnlyList<Diagnostic> Validate(ProjectDefinition project) => [.. DirectiveValidator.Validate(project)];

    [Fact]
    public void Validate_WellFormedRename_ProducesNothing()
    {
        var project = Project(new ProjectDirectives(
                ObjectRenames: [new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), new SqlIdentifier("people"))]),
            AppSchema(Table("people", "id")));

        Validate(project).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_RenameTargetNotDeclared_IsAnError()
    {
        var project = Project(new ProjectDirectives(
                ObjectRenames: [new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), new SqlIdentifier("people"))]),
            AppSchema());

        Validate(project).ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.RenameTargetNotDeclared("table", "app.users", new SqlIdentifier("people")));
    }

    [Fact]
    public void Validate_RenameSourceStillDeclared_IsAnError()
    {
        var project = Project(new ProjectDirectives(
                ObjectRenames: [new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), new SqlIdentifier("people"))]),
            AppSchema(Table("people", "id"), Table("users", "id")));

        Validate(project).ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.RenameSourceStillDeclared("table", "app.users", new SqlIdentifier("people")));
    }

    [Fact]
    public void Validate_DirectiveIntoUndeclaredSchema_IsAnError()
    {
        var project = Project(new ProjectDirectives(
            ObjectRenames: [new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, new ObjectAddress(new SqlIdentifier("ghost"), new SqlIdentifier("t"))), new SqlIdentifier("t2"))]));

        Validate(project).ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.DirectiveSchemaNotDeclared("RENAME of table 'ghost.t'", new SqlIdentifier("ghost")));
    }

    [Fact]
    public void Validate_SelfRename_IsAnError()
    {
        var project = Project(new ProjectDirectives(
                ObjectRenames: [new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), new SqlIdentifier("users"))]),
            AppSchema(Table("users", "id")));

        Validate(project).ShouldContain(ProjectDiagnostics.SelfRename("table", "app.users"));
    }

    [Fact]
    public void Validate_TwoRenamesSharingASource_IsAnError()
    {
        var project = Project(new ProjectDirectives(
                ObjectRenames:
                [
                    new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), new SqlIdentifier("people")),
                    new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), new SqlIdentifier("members")),
                ]),
            AppSchema(Table("people", "id"), Table("members", "id")));

        Validate(project).ShouldContain(ProjectDiagnostics.DuplicateRenameSource("table", "app.users"));
    }

    [Fact]
    public void Validate_TwoRenamesSharingATarget_IsAnError()
    {
        var project = Project(new ProjectDirectives(
                ObjectRenames:
                [
                    new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), new SqlIdentifier("people")),
                    new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("members")), new SqlIdentifier("people")),
                ]),
            AppSchema(Table("people", "id")));

        Validate(project).ShouldContain(ProjectDiagnostics.DuplicateRenameTarget("table", "app.people"));
    }

    [Fact]
    public void Validate_RenameChain_IsAnError()
    {
        // a → b and b → c: renames are unordered, so the chain is ambiguous, whichever way it is written.
        var project = Project(new ProjectDirectives(
                ObjectRenames:
                [
                    new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("a")), new SqlIdentifier("b")),
                    new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("b")), new SqlIdentifier("c")),
                ]),
            AppSchema(Table("b", "id"), Table("c", "id")));

        Validate(project).ShouldContain(ProjectDiagnostics.RenameChain("table", "app.b"));
    }

    [Fact]
    public void Validate_SameBareNamesInDifferentSchemas_DoNotInteract()
    {
        // Renames partition by container: two schemas each renaming their own 'users' is not a conflict.
        var other = new SqlIdentifier("audit");
        var project = new ProjectDefinition(
            new Database([AppSchema(Table("people", "id")), new Schema(other, tables: [Table("people", "id")])]),
            new ProjectDirectives(ObjectRenames: [
                new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), new SqlIdentifier("people")),
                new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, new ObjectAddress(other, new SqlIdentifier("users"))), new SqlIdentifier("people")),
            ]));

        Validate(project).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ObjectDirectivesUnderASchemaRename_ResolveThroughIt()
    {
        // The directives address current reality — schema 'sales', though the declaration is 'core' — so the
        // validator resolves the container through the schema rename before checking declarations.
        var core = new SqlIdentifier("core");
        var sales = new SqlIdentifier("sales");
        var project = new ProjectDefinition(
            new Database([new Schema(core, tables: [Table("people", "id", "full_name")])]),
            new ProjectDirectives(
                SchemaRenames: [new SchemaRenameDirective(sales, core)],
                ObjectRenames: [new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, new ObjectAddress(sales, new SqlIdentifier("users"))), new SqlIdentifier("people"))],
                MemberRenames:
                [
                    new MemberRenameDirective(new MemberAddress(sales, new SqlIdentifier("users"), new SqlIdentifier("name")), new SqlIdentifier("full_name")),
                ]));

        Validate(project).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ColumnRenameTargetNotDeclared_IsAnError()
    {
        var project = Project(new ProjectDirectives(
                MemberRenames: [new MemberRenameDirective(new MemberAddress(_app, new SqlIdentifier("users"), new SqlIdentifier("name")), new SqlIdentifier("full_name"))]),
            AppSchema(Table("users", "id")));

        Validate(project).ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.RenameTargetNotDeclared("column", "app.users.name", new SqlIdentifier("full_name")));
    }

    [Fact]
    public void Validate_ColumnRenameIntoUndeclaredTable_IsAnError()
    {
        var project = Project(new ProjectDirectives(
                MemberRenames: [new MemberRenameDirective(new MemberAddress(_app, new SqlIdentifier("ghost"), new SqlIdentifier("name")), new SqlIdentifier("full_name"))]),
            AppSchema());

        Validate(project).ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.DirectiveTableNotDeclared(new MemberAddress(_app, new SqlIdentifier("ghost"), new SqlIdentifier("name"))));
    }
}
