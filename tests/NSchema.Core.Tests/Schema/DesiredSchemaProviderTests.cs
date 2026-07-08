using Microsoft.Extensions.FileSystemGlobbing;
using NSchema.Schema;

namespace NSchema.Tests.Schema;

public sealed class DesiredSchemaProviderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("nschema-desired-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Write(string relativePath, string sql)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sql);
    }

    private static DdlSchemaSource Source(string root, string glob)
    {
        var matcher = new Matcher();
        matcher.AddInclude(glob);
        return new DdlSchemaSource(root, matcher);
    }

    [Fact]
    public async Task GetProject_NoSources_Throws()
        => await Should.ThrowAsync<InvalidOperationException>(() => new DesiredSchemaProvider([]).GetProject().AsTask());

    [Fact]
    public async Task GetProject_NoMatchingFiles_Throws()
    {
        var sut = new DesiredSchemaProvider([Source(_root, "**/*.sql")]);

        await Should.ThrowAsync<FileNotFoundException>(() => sut.GetProject(cancellationToken: TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task GetProject_AggregatesAllSources()
    {
        Write("a.sql", "CREATE SCHEMA a;");
        Write("b.sql", "CREATE SCHEMA b;");
        var sut = new DesiredSchemaProvider([Source(_root, "**/*.sql")]);

        var project = await sut.GetProject(null, TestContext.Current.CancellationToken);

        project.Schema.Schemas.Select(s => s.Name).ShouldBe(["a", "b"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetProject_LayersMultipleSources()
    {
        // Mirrors the CLI's base + environment-overlay registration: two sources aggregate.
        Write("base.sql", "CREATE SCHEMA app;");
        Write("overlay.sql", "CREATE SCHEMA audit;");
        var sut = new DesiredSchemaProvider([Source(_root, "base.sql"), Source(_root, "overlay.sql")]);

        var project = await sut.GetProject(null, TestContext.Current.CancellationToken);

        project.Schema.Schemas.Select(s => s.Name).ShouldBe(["app", "audit"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetProject_SurfacesDeploymentScripts()
    {
        Write("schema.sql",
            """
            CREATE SCHEMA app;
            POST DEPLOYMENT 'backfill' AS $$ UPDATE app.t SET x = 1; $$;
            """);
        var sut = new DesiredSchemaProvider([Source(_root, "**/*.sql")]);

        var project = await sut.GetProject(null, TestContext.Current.CancellationToken);

        project.Scripts.ShouldHaveSingleItem().Name.ShouldBe("backfill");
    }

    [Fact]
    public async Task GetProject_FiltersSchemaByScope()
    {
        Write("schema.sql", "CREATE SCHEMA app; CREATE SCHEMA audit;");
        var sut = new DesiredSchemaProvider([Source(_root, "**/*.sql")]);

        var project = await sut.GetProject(["app"], TestContext.Current.CancellationToken);

        project.Schema.Schemas.Select(s => s.Name).ShouldBe(["app"]);
    }

    [Fact]
    public async Task GetProject_ExpandsTemplatesAcrossFiles()
    {
        // Templates are location-agnostic: the definition, its application, and the target schemas may each live
        // in a different file — expansion runs on the aggregate.
        Write("templates.sql", "TEMPLATE outbox BEGIN CREATE TABLE outbox (id int NOT NULL); END;");
        Write("schemas.sql",
            """
            CREATE SCHEMA billing;
            CREATE SCHEMA ordering;
            APPLY TEMPLATE outbox IN SCHEMA billing, ordering;
            """);
        var sut = new DesiredSchemaProvider([Source(_root, "**/*.sql")]);

        var project = await sut.GetProject(null, TestContext.Current.CancellationToken);

        project.Schema.Schemas.Select(s => s.Name).ShouldBe(["billing", "ordering"], ignoreOrder: true);
        project.Schema.Schemas.ShouldAllBe(s => s.Tables.Count == 1);
    }

    [Fact]
    public async Task GetProject_TemplateInstancesRespectScopeFilter()
    {
        // Expansion happens before the scope filter, so an instance in an out-of-scope schema is filtered away.
        Write("schema.sql",
            """
            CREATE SCHEMA billing;
            CREATE SCHEMA ordering;
            TEMPLATE outbox BEGIN CREATE TABLE outbox (id int NOT NULL); END;
            APPLY TEMPLATE outbox IN SCHEMA billing, ordering;
            """);
        var sut = new DesiredSchemaProvider([Source(_root, "**/*.sql")]);

        var project = await sut.GetProject(["billing"], TestContext.Current.CancellationToken);

        project.Schema.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Name.ShouldBe("outbox");
    }
}
