using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using NSchema.Project;
using NSchema.Project.Domain;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Tests;

public sealed class AddProjectSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public AddProjectSourceTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string WriteSchemaFile(string relativePath, string schemaName)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"CREATE SCHEMA {schemaName};");
        return path;
    }

    // Resolves the desired project the way the operations do — through the aggregated IProjectProvider.
    private static async Task<ProjectDefinition> ResolveProject(Action<NSchemaApplicationBuilder> configure, SchemaScope? scope = null)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        using var app = builder.Build();
        return (await app.Services.GetRequiredService<IProjectProvider>().GetProject(scope ?? SchemaScope.All, TestContext.Current.CancellationToken)).Value!;
    }

    private static async Task<List<string>> ResolveSchemaNames(Action<NSchemaApplicationBuilder> configure) =>
        (await ResolveProject(configure)).Schema.Schemas.Select(s => s.Name).ToList();

    [Fact]
    public async Task AddSqlSchemas_LoadsSingleFile()
    {
        // A wildcard-free pattern names a single file under the base directory.
        WriteSchemaFile("app.sql", "app");

        var names = await ResolveSchemaNames(b => b.AddProjectSource(_root, "app.sql"));

        names.ShouldBe(["app"]);
    }

    [Fact]
    public async Task AddSqlSchemas_MatchesFilesAtEveryDepth()
    {
        WriteSchemaFile("a.sql", "a");
        WriteSchemaFile("nested/b.sql", "b");
        WriteSchemaFile("nested/deep/c.sql", "c");

        var names = await ResolveSchemaNames(b => b.AddProjectSource(_root));

        names.ShouldBe(["a", "b", "c"], ignoreOrder: true);
    }

    [Fact]
    public async Task AddSqlSchemas_WithMatcher_HonoursExcludes()
    {
        // The CLI's case: include every .sql but exclude the pre/post deployment scripts.
        WriteSchemaFile("app.sql", "app");
        WriteSchemaFile("app.pre.sql", "pre");
        WriteSchemaFile("app.post.sql", "post");

        var matcher = new Matcher();
        matcher.AddInclude("**/*.sql");
        matcher.AddExclude("**/*.pre.sql");
        matcher.AddExclude("**/*.post.sql");

        var names = await ResolveSchemaNames(b => b.AddProjectSource(_root, matcher));

        names.ShouldBe(["app"]);
    }

    [Fact]
    public async Task AddSqlSchemas_AggregatesMultipleCalls()
    {
        // Multiple AddSqlSchemas calls aggregate — the shape the CLI's environment overlay relies on.
        WriteSchemaFile("base.sql", "app");
        WriteSchemaFile("extra.sql", "audit");

        var names = await ResolveSchemaNames(b => b.AddProjectSource(_root, "base.sql").AddProjectSource(_root, "extra.sql"));

        names.ShouldBe(["app", "audit"], ignoreOrder: true);
    }

    [Fact]
    public async Task AddSqlSchemas_MatchingNothing_FailsTheRead()
    {
        // Planning against an empty desired schema would read as "drop everything", so a pattern that
        // resolves to no files is a configuration error rather than an empty schema.
        var builder = NSchemaApplication.CreateBuilder();
        builder.AddProjectSource(_root);
        using var app = builder.Build();

        var project = await app.Services.GetRequiredService<IProjectProvider>()
            .GetProject(SchemaScope.All, TestContext.Current.CancellationToken);

        project.IsFailure.ShouldBeTrue();
        project.Errors.ShouldHaveSingleItem().ShouldBe(ProjectDiagnostics.NoFilesMatched());
    }

    [Fact]
    public async Task AddSqlSchemas_FiltersBySchemaNames()
    {
        File.WriteAllText(Path.Combine(_root, "multi.sql"), "CREATE SCHEMA app; CREATE SCHEMA audit;");

        var project = await ResolveProject(b => b.AddProjectSource(_root), scope: SchemaScope.Of("app"));

        project.Schema.Schemas.Select(s => s.Name).ShouldBe(["app"]);
    }

    [Fact]
    public async Task AddSqlSchemas_SurfacesInlineDeploymentScripts()
    {
        File.WriteAllText(Path.Combine(_root, "schema.sql"),
            """
            CREATE SCHEMA app;
            SCRIPT 'backfill' RUN ON POST DEPLOYMENT AS $$ UPDATE app.t SET x = 1; $$;
            """);

        var project = await ResolveProject(b => b.AddProjectSource(_root));

        var script = project.Scripts.ShouldHaveSingleItem();
        script.Name.ShouldBe("backfill");
        script.Event.ShouldBe(new DeploymentEvent(DeploymentPhase.Post));
    }

    [Fact]
    public async Task AddSqlSchemas_WithNoDeploymentBlocks_YieldsNoScripts()
    {
        WriteSchemaFile("app.sql", "app");

        (await ResolveProject(b => b.AddProjectSource(_root))).Scripts.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddSqlSchemas_RegistersOneSource_AndMatchesAtReadTime()
    {
        // The glob is evaluated when the project is read, not at registration: one source is registered, and a file
        // written after the builder is configured is still picked up.
        var builder = NSchemaApplication.CreateBuilder();
        builder.AddProjectSource(_root);
        using var app = builder.Build();

        app.Services.GetServices<ProjectSource>().ShouldHaveSingleItem();
        WriteSchemaFile("late.sql", "late");

        var project = (await app.Services.GetRequiredService<IProjectProvider>().GetProject(SchemaScope.All, TestContext.Current.CancellationToken)).Value!;
        project.Schema.Schemas.Select(s => s.Name).ShouldBe(["late"]);
    }
}
