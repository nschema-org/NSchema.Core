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

    private string FilePath => Path.Combine(_dir, "schema.sql");

    private Task Execute(ImportArguments arguments) =>
        BuildSut().Execute(arguments, TestContext.Current.CancellationToken);

    private static async Task<DatabaseSchema> ReadSchema(string path)
    {
        var text = await File.ReadAllTextAsync(path);
        return DdlReader.Instance.Read(text).Schema;
    }

    // ── Source fetching ─────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_FetchesSchemaFromOnlineSource()
    {
        await Execute(new ImportArguments { OutputFile = FilePath });

        await _currentSchema.Received(1).GetSchema(
            SchemaSourceMode.Online, Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PassesSchemaFilterToSource()
    {
        var arguments = new ImportArguments { OutputFile = FilePath, Schemas = ["app", "audit"] };

        await Execute(arguments);

        await _currentSchema.Received(1).GetSchema(
            SchemaSourceMode.Online, arguments.Schemas, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithTableFilter_FiltersSchemaBeforeWriting()
    {
        await Execute(new ImportArguments { OutputFile = FilePath, Tables = ["users"] });

        var result = await ReadSchema(FilePath);
        result.Schemas.Single().Tables.Select(t => t.Name).ShouldBe(["users"]);
    }

    [Fact]
    public async Task Execute_WithEmptyTableFilter_WritesSchemaUnfiltered()
    {
        await Execute(new ImportArguments { OutputFile = FilePath, Tables = [] });

        var result = await ReadSchema(FilePath);
        result.Schemas.Single().Tables.Select(t => t.Name).ShouldBe(["users", "orders"], ignoreOrder: true);
    }

    [Fact]
    public async Task Execute_ReportsProgress()
    {
        await Execute(new ImportArguments { OutputFile = FilePath });

        _reporter.Received(2).Info(Arg.Any<string>());
    }

    // ── Partition mode: None (single file) ──────────────────────────────────

    [Fact]
    public async Task Execute_None_NewFile_WritesAllTables()
    {
        await Execute(new ImportArguments { OutputFile = FilePath });

        var result = await ReadSchema(FilePath);
        result.Schemas.Single().Tables.Select(t => t.Name).ShouldBe(["users", "orders"], ignoreOrder: true);
    }

    [Fact]
    public async Task Execute_None_ExistingFile_PreservesTablesNotInIncoming()
    {
        Source(new DatabaseSchema([new SchemaDefinition("app", Tables: [MakeTable("audit_log")])]));
        await Execute(new ImportArguments { OutputFile = FilePath });

        Source(new DatabaseSchema([new SchemaDefinition("app", Tables: [MakeTable("users")])]));
        await Execute(new ImportArguments { OutputFile = FilePath });

        var result = await ReadSchema(FilePath);
        result.Schemas.Single().Tables.Select(t => t.Name).ShouldBe(["audit_log", "users"], ignoreOrder: true);
    }

    [Fact]
    public async Task Execute_None_ExistingFile_IncomingTableReplacesExisting()
    {
        Source(new DatabaseSchema([new SchemaDefinition("app",
            Tables: [new Table("users", Columns: [new Column("old_col", SqlType.Text)])])]));
        await Execute(new ImportArguments { OutputFile = FilePath });

        Source(new DatabaseSchema([new SchemaDefinition("app",
            Tables: [new Table("users", Columns: [new Column("new_col", SqlType.Text)])])]));
        await Execute(new ImportArguments { OutputFile = FilePath });

        var result = await ReadSchema(FilePath);
        var usersTable = result.Schemas.Single().Tables.Single(t => t.Name == "users");
        usersTable.Columns.Select(c => c.Name).ShouldBe(["new_col"]);
    }

    [Fact]
    public async Task Execute_None_ExistingFile_ReimportReplacesViewsEnumsAndSequences()
    {
        // A re-import must replace (not duplicate) schema-level objects already in the target file —
        // previously the merge pruned only tables, so this threw "Duplicate view".
        var schema = new DatabaseSchema([new SchemaDefinition("app",
            Tables: [MakeTable("users")],
            Views: [new View("active", "SELECT 1")],
            Enums: [new EnumType("status", ["a"])],
            Sequences: [new Sequence("order_id", new SequenceOptions(StartWith: 1))])]);

        Source(schema);
        await Execute(new ImportArguments { OutputFile = FilePath });

        Source(schema with
        {
            Schemas = [schema.Schemas[0] with
            {
                Enums = [new EnumType("status", ["a", "b"])],
                Sequences = [new Sequence("order_id", new SequenceOptions(StartWith: 100))],
            }],
        });
        await Execute(new ImportArguments { OutputFile = FilePath });

        var result = await ReadSchema(FilePath);
        var app = result.Schemas.Single();
        app.Views.ShouldHaveSingleItem().Name.ShouldBe("active");
        app.Enums.ShouldHaveSingleItem().Values.ShouldBe(["a", "b"]); // incoming wins
        app.Sequences.ShouldHaveSingleItem().Options.StartWith.ShouldBe(100);
    }

    [Fact]
    public async Task Execute_None_CreatesDirectoryIfMissing()
    {
        var filePath = Path.Combine(_dir, "nested", "deep", "schema.sql");
        Source(new DatabaseSchema([]));

        await Execute(new ImportArguments { OutputFile = filePath });

        File.Exists(filePath).ShouldBeTrue();
    }

    // ── Partition mode: Schema (one file per schema namespace) ──────────────

    [Fact]
    public async Task Execute_Schema_CreatesOneFilePerSchema()
    {
        Source(new DatabaseSchema([
            new SchemaDefinition("app"),
            new SchemaDefinition("audit"),
        ]));

        await Execute(new ImportArguments { OutputDirectory = _dir, Partition = ImportPartitionMode.Schema });

        File.Exists(Path.Combine(_dir, $"app.sql")).ShouldBeTrue();
        File.Exists(Path.Combine(_dir, $"audit.sql")).ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_Schema_EachFileContainsOnlyItsSchema()
    {
        Source(new DatabaseSchema([
            new SchemaDefinition("app", Tables: [MakeTable("users")]),
            new SchemaDefinition("audit", Tables: [MakeTable("logs")]),
        ]));

        await Execute(new ImportArguments { OutputDirectory = _dir, Partition = ImportPartitionMode.Schema });

        var app = await ReadSchema(Path.Combine(_dir, $"app.sql"));
        app.Schemas.Single().Name.ShouldBe("app");
        app.Schemas.Single().Tables.Single().Name.ShouldBe("users");

        var audit = await ReadSchema(Path.Combine(_dir, $"audit.sql"));
        audit.Schemas.Single().Name.ShouldBe("audit");
        audit.Schemas.Single().Tables.Single().Name.ShouldBe("logs");
    }

    // ── Partition mode: Object (one file per major object) ──────────────────

    private string ObjectPath(string type, string name) =>
        Path.Combine(_dir, "app", type, $"{name}.sql");

    private string HeaderPath => Path.Combine(_dir, $"app.sql");

    [Fact]
    public async Task Execute_Object_CreatesOneFilePerMajorObjectGroupedByType()
    {
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir, Partition = ImportPartitionMode.Object });

        File.Exists(ObjectPath("tables", "users")).ShouldBeTrue();
        File.Exists(ObjectPath("tables", "orders")).ShouldBeTrue();
        File.Exists(ObjectPath("views", "active")).ShouldBeTrue();
        File.Exists(ObjectPath("functions", "calc")).ShouldBeTrue();
        File.Exists(ObjectPath("procedures", "sync")).ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_Object_EachFileContainsOnlyItsObject()
    {
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir, Partition = ImportPartitionMode.Object });

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
    public async Task Execute_Object_WritesLeftoverObjectsToPerSchemaHeaderFile()
    {
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir, Partition = ImportPartitionMode.Object });

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
    public async Task Execute_Object_AllFilesCombineWithoutDuplicates()
    {
        // Loading every emitted file together (as desired-schema providers do) must reconstruct the
        // original schema without tripping the aggregator's duplicate detection.
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir, Partition = ImportPartitionMode.Object });

        var files = Directory.EnumerateFiles(_dir, $"*.sql", SearchOption.AllDirectories);
        var combined = new DatabaseSchema([]);
        foreach (var file in files)
        {
            combined = combined.Combine(await ReadSchema(file));
        }

        var app = combined.Schemas.Single();
        app.Tables.Select(t => t.Name).ShouldBe(["users", "orders"], ignoreOrder: true);
        app.Views.ShouldHaveSingleItem().Name.ShouldBe("active");
        app.Functions.ShouldHaveSingleItem().Name.ShouldBe("calc");
        app.Procedures.ShouldHaveSingleItem().Name.ShouldBe("sync");
        app.Enums.ShouldHaveSingleItem().Name.ShouldBe("status");
        app.Sequences.ShouldHaveSingleItem().Name.ShouldBe("order_id");
    }

    private static DatabaseSchema RichSchema() => new([new SchemaDefinition("app",
        Tables: [MakeTable("users"), MakeTable("orders")],
        Views: [new View("active", "SELECT 1")],
        Functions: [new Function("calc", "", "RETURNS int LANGUAGE sql AS $$ SELECT 1 $$")],
        Procedures: [new Procedure("sync", "", "LANGUAGE sql AS $$ SELECT 1 $$")],
        Enums: [new EnumType("status", ["a"])],
        Sequences: [new Sequence("order_id", new SequenceOptions(StartWith: 1))])]);
}
