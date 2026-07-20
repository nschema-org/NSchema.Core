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

    private static ObjectAddress App(string name) => new(_app, name);

    private static ProjectDefinition Project(ProjectDirectives directives, params Schema[] schemas) =>
        new(new Database { Schemas = [.. schemas] }, directives);

    private static Schema AppSchema(params Table[] tables) => new Schema { Name = _app, Tables = [.. tables] };

    private static Table Table(string name, params string[] columns) =>
        new Table { Name = name, Columns = [.. columns.Select(c => new Column { Name = c, Type = SqlType.Int })] };

    private static IReadOnlyList<Diagnostic> Validate(ProjectDefinition project) => [.. DirectiveValidator.Validate(project)];

    [Fact]
    public void Validate_WellFormedRename_ProducesNothing()
    {
        var project = Project(new ProjectDirectives(
                ObjectRenames: [new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), "people")]),
            AppSchema(Table("people", "id")));

        Validate(project).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_RenameTargetNotDeclared_IsAnError()
    {
        var project = Project(new ProjectDirectives(
                ObjectRenames: [new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), "people")]),
            AppSchema());

        Validate(project).ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.RenameTargetNotDeclared("table", "app.users", "people"));
    }

    [Fact]
    public void Validate_RenameSourceStillDeclared_IsAnError()
    {
        var project = Project(new ProjectDirectives(
                ObjectRenames: [new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), "people")]),
            AppSchema(Table("people", "id"), Table("users", "id")));

        Validate(project).ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.RenameSourceStillDeclared("table", "app.users", "people"));
    }

    [Fact]
    public void Validate_DirectiveIntoUndeclaredSchema_IsAnError()
    {
        var rename = new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, new ObjectAddress("ghost", "t")), "t2");
        var project = Project(new ProjectDirectives(ObjectRenames: [rename]));

        Validate(project).ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.DirectiveSchemaNotDeclared($"RENAME of table '{rename.From}'", "ghost"));
    }

    [Fact]
    public void Validate_SelfRename_IsAnError()
    {
        var project = Project(new ProjectDirectives(
                ObjectRenames: [new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), "users")]),
            AppSchema(Table("users", "id")));

        Validate(project).ShouldContain(ProjectDiagnostics.SelfRename("table", "app.users"));
    }

    [Fact]
    public void Validate_TwoRenamesSharingASource_IsAnError()
    {
        var project = Project(new ProjectDirectives(
                ObjectRenames:
                [
                    new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), "people"),
                    new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), "members"),
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
                    new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), "people"),
                    new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("members")), "people"),
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
                    new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("a")), "b"),
                    new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("b")), "c"),
                ]),
            AppSchema(Table("b", "id"), Table("c", "id")));

        Validate(project).ShouldContain(ProjectDiagnostics.RenameChain("table", "app.b"));
    }

    [Fact]
    public void Validate_SameBareNamesInDifferentSchemas_DoNotInteract()
    {
        // Renames partition by container: two schemas each renaming their own 'users' is not a conflict.
        SqlIdentifier other = "audit";
        var project = new ProjectDefinition(
            new Database { Schemas = [AppSchema(Table("people", "id")), new Schema { Name = other, Tables = [Table("people", "id")] }] },
            new ProjectDirectives(ObjectRenames: [
                new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, App("users")), "people"),
                new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, new ObjectAddress(other, "users")), "people"),
            ]));

        Validate(project).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ObjectDirectivesUnderASchemaRename_ResolveThroughIt()
    {
        // The directives address current reality — schema 'sales', though the declaration is 'core' — so the
        // validator resolves the container through the schema rename before checking declarations.
        SqlIdentifier core = "core";
        SqlIdentifier sales = "sales";
        var project = new ProjectDefinition(
            new Database { Schemas = [new Schema { Name = core, Tables = [Table("people", "id", "full_name")] }] },
            new ProjectDirectives(
                SchemaRenames: [new SchemaRenameDirective(sales, core)],
                ObjectRenames: [new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, new ObjectAddress(sales, "users")), "people")],
                MemberRenames:
                [
                    new MemberRenameDirective(new MemberAddress(sales, "users", "name"), "full_name"),
                ]));

        Validate(project).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ColumnRenameTargetNotDeclared_IsAnError()
    {
        var project = Project(new ProjectDirectives(
                MemberRenames: [new MemberRenameDirective(new MemberAddress(_app, "users", "name"), "full_name")]),
            AppSchema(Table("users", "id")));

        Validate(project).ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.RenameTargetNotDeclared("column", "app.users.name", "full_name"));
    }

    [Fact]
    public void Validate_ColumnRenameIntoUndeclaredTable_IsAnError()
    {
        var project = Project(new ProjectDirectives(
                MemberRenames: [new MemberRenameDirective(new MemberAddress(_app, "ghost", "name"), "full_name")]),
            AppSchema());

        Validate(project).ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.DirectiveTableNotDeclared(new MemberAddress(_app, "ghost", "name")));
    }
}
