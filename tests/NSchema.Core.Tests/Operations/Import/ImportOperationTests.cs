using NSchema.Operations;
using NSchema.Operations.Import;
using NSchema.Schema;
using NSchema.Schema.Ddl;
using NSchema.Schema.Model;

namespace NSchema.Tests.Operations.Import;

public sealed class ImportOperationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly ICurrentSchemaProvider _currentSchema = Substitute.For<ICurrentSchemaProvider>();
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();

    // Tables carry a column because the DDL grammar has no empty-table form.
    private static Table MakeTable(string name) => new(name, Columns: [new Column("id", SqlType.Int)]);

    private readonly DatabaseSchema _schema = new DatabaseSchema([new SchemaDefinition("app",
        Tables: [MakeTable("users"), MakeTable("orders")])]);

    public ImportOperationTests()
    {
        Directory.CreateDirectory(_dir);
        Source(_schema);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Source(DatabaseSchema schema) => _currentSchema
        .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
        .Returns(ValueTask.FromResult(schema));

    private ImportOperation BuildSut() => new(_currentSchema, Helpers.TestReporters.ResolverFor(_reporter));

    private Task Execute(ImportArguments arguments) =>
        BuildSut().Execute(arguments, TestContext.Current.CancellationToken);

    private static async Task<DatabaseSchema> ReadSchema(string path)
    {
        var text = await File.ReadAllTextAsync(path);
        return DdlReader.Instance.Read(text).Schema;
    }

    private string ObjectPath(string type, string name) => Path.Combine(_dir, "app", type, $"{name}.sql");
    private string HeaderPath => Path.Combine(_dir, "app.sql");

    // Combines every .sql file written under the output directory, as the desired-schema providers would.
    private async Task<DatabaseSchema> ReadAll()
    {
        var combined = new DatabaseSchema([]);
        foreach (var file in Directory.EnumerateFiles(_dir, "*.sql", SearchOption.AllDirectories))
        {
            combined = combined.Combine(await ReadSchema(file));
        }
        return combined;
    }

    // ── Source fetching ─────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_FetchesSchemaFromOnlineSource()
    {
        await Execute(new ImportArguments { OutputDirectory = _dir });

        await _currentSchema.Received(1).GetSchema(
            SchemaSourceMode.Online, Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PassesSchemaFilterToSource()
    {
        var arguments = new ImportArguments { OutputDirectory = _dir, Schemas = ["app", "audit"] };

        await Execute(arguments);

        await _currentSchema.Received(1).GetSchema(
            SchemaSourceMode.Online, arguments.Schemas, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithTableFilter_FiltersSchemaBeforeWriting()
    {
        await Execute(new ImportArguments { OutputDirectory = _dir, Tables = ["users"] });

        File.Exists(ObjectPath("tables", "users")).ShouldBeTrue();
        File.Exists(ObjectPath("tables", "orders")).ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_WithEmptyTableFilter_WritesSchemaUnfiltered()
    {
        await Execute(new ImportArguments { OutputDirectory = _dir, Tables = [] });

        File.Exists(ObjectPath("tables", "users")).ShouldBeTrue();
        File.Exists(ObjectPath("tables", "orders")).ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_ReportsProgress()
    {
        await Execute(new ImportArguments { OutputDirectory = _dir });

        _reporter.Received(2).Report(Arg.Any<MessageKind>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Execute_CreatesOutputDirectoryIfMissing()
    {
        var nested = Path.Combine(_dir, "nested", "deep");

        await Execute(new ImportArguments { OutputDirectory = nested });

        File.Exists(Path.Combine(nested, "app", "tables", "users.sql")).ShouldBeTrue();
    }

    // ── Object layout ───────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_CreatesOneFilePerMajorObjectGroupedByType()
    {
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir });

        File.Exists(ObjectPath("tables", "users")).ShouldBeTrue();
        File.Exists(ObjectPath("tables", "orders")).ShouldBeTrue();
        File.Exists(ObjectPath("views", "active")).ShouldBeTrue();
        File.Exists(ObjectPath("functions", "calc")).ShouldBeTrue();
        File.Exists(ObjectPath("procedures", "sync")).ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_EachFileContainsOnlyItsObject()
    {
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir });

        (await ReadSchema(ObjectPath("tables", "users"))).Schemas.Single().Tables.Single().Name.ShouldBe("users");
        (await ReadSchema(ObjectPath("views", "active"))).Schemas.Single().Views.Single().Name.ShouldBe("active");
        (await ReadSchema(ObjectPath("functions", "calc"))).Schemas.Single().Functions.Single().Name.ShouldBe("calc");
        (await ReadSchema(ObjectPath("procedures", "sync"))).Schemas.Single().Procedures.Single().Name.ShouldBe("sync");

        // An object file carries nothing but its own object.
        var users = (await ReadSchema(ObjectPath("tables", "users"))).Schemas.Single();
        users.Views.ShouldBeEmpty();
        users.Functions.ShouldBeEmpty();
        users.Enums.ShouldBeEmpty();
        users.Sequences.ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_WritesLeftoverObjectsToPerSchemaHeaderFile()
    {
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir });

        var header = (await ReadSchema(HeaderPath)).Schemas.Single();
        header.Enums.ShouldHaveSingleItem().Name.ShouldBe("status");
        header.Sequences.ShouldHaveSingleItem().Name.ShouldBe("order_id");
        // The major objects live in their own files, not the header.
        header.Tables.ShouldBeEmpty();
        header.Views.ShouldBeEmpty();
        header.Functions.ShouldBeEmpty();
        header.Procedures.ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_AllFilesCombineWithoutDuplicates()
    {
        // Loading every emitted file together (as desired-schema providers do) must reconstruct the
        // original schema without tripping the aggregator's duplicate detection.
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir });

        var app = (await ReadAll()).Schemas.Single();
        app.Tables.Select(t => t.Name).ShouldBe(["users", "orders"], ignoreOrder: true);
        app.Views.ShouldHaveSingleItem().Name.ShouldBe("active");
        app.Functions.ShouldHaveSingleItem().Name.ShouldBe("calc");
        app.Procedures.ShouldHaveSingleItem().Name.ShouldBe("sync");
        app.Enums.ShouldHaveSingleItem().Name.ShouldBe("status");
        app.Sequences.ShouldHaveSingleItem().Name.ShouldBe("order_id");
    }

    [Fact]
    public async Task Execute_MultipleSchemas_EachGetsItsOwnDirectory()
    {
        Source(new DatabaseSchema([
            new SchemaDefinition("app", Tables: [MakeTable("users")]),
            new SchemaDefinition("audit", Tables: [MakeTable("logs")]),
        ]));

        await Execute(new ImportArguments { OutputDirectory = _dir });

        (await ReadSchema(Path.Combine(_dir, "app", "tables", "users.sql"))).Schemas.Single().Tables.Single().Name.ShouldBe("users");
        (await ReadSchema(Path.Combine(_dir, "audit", "tables", "logs.sql"))).Schemas.Single().Tables.Single().Name.ShouldBe("logs");
    }

    // ── Additive re-import (merge) ───────────────────────────────────────────

    [Fact]
    public async Task Execute_ReimportPreservesObjectsNotInIncoming()
    {
        // Each object is its own file, so an object absent from a later import is simply not rewritten.
        Source(new DatabaseSchema([new SchemaDefinition("app", Tables: [MakeTable("audit_log")])]));
        await Execute(new ImportArguments { OutputDirectory = _dir });

        Source(new DatabaseSchema([new SchemaDefinition("app", Tables: [MakeTable("users")])]));
        await Execute(new ImportArguments { OutputDirectory = _dir });

        (await ReadAll()).Schemas.Single().Tables.Select(t => t.Name).ShouldBe(["audit_log", "users"], ignoreOrder: true);
    }

    [Fact]
    public async Task Execute_ReimportReplacesAnObjectInPlace()
    {
        Source(new DatabaseSchema([new SchemaDefinition("app",
            Tables: [new Table("users", Columns: [new Column("old_col", SqlType.Text)])])]));
        await Execute(new ImportArguments { OutputDirectory = _dir });

        Source(new DatabaseSchema([new SchemaDefinition("app",
            Tables: [new Table("users", Columns: [new Column("new_col", SqlType.Text)])])]));
        await Execute(new ImportArguments { OutputDirectory = _dir });

        var users = (await ReadSchema(ObjectPath("tables", "users"))).Schemas.Single().Tables.Single(t => t.Name == "users");
        users.Columns.Select(c => c.Name).ShouldBe(["new_col"]);
    }

    [Fact]
    public async Task Execute_ReimportReplacesHeaderObjects()
    {
        // The header file holds schema-level objects; a re-import must replace (not duplicate) them.
        var schema = new DatabaseSchema([new SchemaDefinition("app",
            Enums: [new EnumType("status", ["a"])],
            Sequences: [new Sequence("order_id", new SequenceOptions(StartWith: 1))])]);

        Source(schema);
        await Execute(new ImportArguments { OutputDirectory = _dir });

        Source(new DatabaseSchema([new SchemaDefinition("app",
            Enums: [new EnumType("status", ["a", "b"])],
            Sequences: [new Sequence("order_id", new SequenceOptions(StartWith: 100))])]));
        await Execute(new ImportArguments { OutputDirectory = _dir });

        var header = (await ReadSchema(HeaderPath)).Schemas.Single();
        header.Enums.ShouldHaveSingleItem().Values.ShouldBe(["a", "b"]); // incoming wins
        header.Sequences.ShouldHaveSingleItem().Options.StartWith.ShouldBe(100);
    }

    private static DatabaseSchema RichSchema() => new([new SchemaDefinition("app",
        Tables: [MakeTable("users"), MakeTable("orders")],
        Views: [new View("active", "SELECT 1")],
        Functions: [new Function("calc", "", "RETURNS int LANGUAGE sql AS $$ SELECT 1 $$")],
        Procedures: [new Procedure("sync", "", "LANGUAGE sql AS $$ SELECT 1 $$")],
        Enums: [new EnumType("status", ["a"])],
        Sequences: [new Sequence("order_id", new SequenceOptions(StartWith: 1))])]);
}
