using NSchema.Operations;
using NSchema.Operations.Import;
using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;

namespace NSchema.Tests.Operations.Import;

public sealed class ImportOperationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly ICurrentSchemaProvider _currentSchema = Substitute.For<ICurrentSchemaProvider>();
    private readonly IKeyedResolver<ISchemaSerializer> _serializers = Substitute.For<IKeyedResolver<ISchemaSerializer>>();
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();

    private readonly DatabaseSchema _schema = DatabaseSchema.Create([SchemaDefinition.Create("app",
        tables: [Table.Create("users"), Table.Create("orders")])]);

    public ImportOperationTests()
    {
        Directory.CreateDirectory(_dir);
        _serializers.Resolve(JsonSchemaSerializer.FormatName).Returns(JsonSchemaSerializer.Instance);
        Source(_schema);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Source(DatabaseSchema schema) => _currentSchema
        .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
        .Returns(ValueTask.FromResult(schema));

    private ImportOperation BuildSut() => new(
        _currentSchema,
        _serializers,
        Helpers.TestReporters.ResolverFor(_reporter));

    private string FilePath => Path.Combine(_dir, "schema.json");

    private Task Execute(ImportArguments arguments) =>
        BuildSut().Execute(arguments, TestContext.Current.CancellationToken);

    private static async Task<DatabaseSchema> ReadSchema(string path)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSchemaSerializer.Instance.Read(stream);
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
        Source(DatabaseSchema.Create([SchemaDefinition.Create("app", tables: [Table.Create("audit_log")])]));
        await Execute(new ImportArguments { OutputFile = FilePath });

        Source(DatabaseSchema.Create([SchemaDefinition.Create("app", tables: [Table.Create("users")])]));
        await Execute(new ImportArguments { OutputFile = FilePath });

        var result = await ReadSchema(FilePath);
        result.Schemas.Single().Tables.Select(t => t.Name).ShouldBe(["audit_log", "users"], ignoreOrder: true);
    }

    [Fact]
    public async Task Execute_None_ExistingFile_IncomingTableReplacesExisting()
    {
        Source(DatabaseSchema.Create([SchemaDefinition.Create("app",
            tables: [Table.Create("users", columns: [Column.Create("old_col", SqlType.Text)])])]));
        await Execute(new ImportArguments { OutputFile = FilePath });

        Source(DatabaseSchema.Create([SchemaDefinition.Create("app",
            tables: [Table.Create("users", columns: [Column.Create("new_col", SqlType.Text)])])]));
        await Execute(new ImportArguments { OutputFile = FilePath });

        var result = await ReadSchema(FilePath);
        var usersTable = result.Schemas.Single().Tables.Single(t => t.Name == "users");
        usersTable.Columns.Select(c => c.Name).ShouldBe(["new_col"]);
    }

    [Fact]
    public async Task Execute_None_CreatesDirectoryIfMissing()
    {
        var filePath = Path.Combine(_dir, "nested", "deep", "schema.json");
        Source(DatabaseSchema.Create([]));

        await Execute(new ImportArguments { OutputFile = filePath });

        File.Exists(filePath).ShouldBeTrue();
    }

    // ── Partition mode: Schema (one file per schema namespace) ──────────────

    [Fact]
    public async Task Execute_Schema_CreatesOneFilePerSchema()
    {
        Source(DatabaseSchema.Create([
            SchemaDefinition.Create("app"),
            SchemaDefinition.Create("audit"),
        ]));

        await Execute(new ImportArguments { OutputDirectory = _dir, Partition = ImportPartitionMode.Schema });

        File.Exists(Path.Combine(_dir, $"app.{JsonSchemaSerializer.FormatName}")).ShouldBeTrue();
        File.Exists(Path.Combine(_dir, $"audit.{JsonSchemaSerializer.FormatName}")).ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_Schema_EachFileContainsOnlyItsSchema()
    {
        Source(DatabaseSchema.Create([
            SchemaDefinition.Create("app", tables: [Table.Create("users")]),
            SchemaDefinition.Create("audit", tables: [Table.Create("logs")]),
        ]));

        await Execute(new ImportArguments { OutputDirectory = _dir, Partition = ImportPartitionMode.Schema });

        var app = await ReadSchema(Path.Combine(_dir, $"app.{JsonSchemaSerializer.FormatName}"));
        app.Schemas.Single().Name.ShouldBe("app");
        app.Schemas.Single().Tables.Single().Name.ShouldBe("users");

        var audit = await ReadSchema(Path.Combine(_dir, $"audit.{JsonSchemaSerializer.FormatName}"));
        audit.Schemas.Single().Name.ShouldBe("audit");
        audit.Schemas.Single().Tables.Single().Name.ShouldBe("logs");
    }

    // ── Partition mode: Table (one file per table) ──────────────────────────

    [Fact]
    public async Task Execute_Table_CreatesOneFilePerTable()
    {
        await Execute(new ImportArguments { OutputDirectory = _dir, Partition = ImportPartitionMode.Table });

        File.Exists(Path.Combine(_dir, "app", $"users.{JsonSchemaSerializer.FormatName}")).ShouldBeTrue();
        File.Exists(Path.Combine(_dir, "app", $"orders.{JsonSchemaSerializer.FormatName}")).ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_Table_EachFileContainsOnlyItsTable()
    {
        await Execute(new ImportArguments { OutputDirectory = _dir, Partition = ImportPartitionMode.Table });

        var usersSchema = await ReadSchema(Path.Combine(_dir, "app", $"users.{JsonSchemaSerializer.FormatName}"));
        usersSchema.Schemas.Single().Tables.Single().Name.ShouldBe("users");

        var ordersSchema = await ReadSchema(Path.Combine(_dir, "app", $"orders.{JsonSchemaSerializer.FormatName}"));
        ordersSchema.Schemas.Single().Tables.Single().Name.ShouldBe("orders");
    }
}
