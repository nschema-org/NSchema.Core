using Microsoft.Extensions.Options;
using NSchema.Import;
using NSchema.Resolution;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;

namespace NSchema.Tests.Import;

public sealed class FileSchemaImportTargetTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly IKeyedResolver<ISchemaSerializer> _serializers;

    public FileSchemaImportTargetTests()
    {
        Directory.CreateDirectory(_dir);

        _serializers = Substitute.For<IKeyedResolver<ISchemaSerializer>>();
        _serializers.Resolve(JsonSchemaSerializer.FormatName).Returns(JsonSchemaSerializer.Instance);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private FileSchemaImportTarget BuildSut(FileSchemaImportTargetOptions opts) =>
        new(Options.Create(opts), _serializers);

    private static async Task<DatabaseSchema> ReadSchema(string path)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSchemaSerializer.Instance.Read(stream);
    }

    // ── Partition mode: None (single file) ──────────────────────────────────

    [Fact]
    public async Task Write_None_NewFile_WritesAllTables()
    {
        var filePath = Path.Combine(_dir, "schema.json");
        var sut = BuildSut(new FileSchemaImportTargetOptions { OutputPath = filePath });
        var schema = DatabaseSchema.Create([SchemaDefinition.Create("app",
            tables: [Table.Create("users"), Table.Create("orders")])]);

        await sut.Write(schema, TestContext.Current.CancellationToken);

        var result = await ReadSchema(filePath);
        result.Schemas.Single().Tables.Select(t => t.Name).ShouldBe(["users", "orders"], ignoreOrder: true);
    }

    [Fact]
    public async Task Write_None_ExistingFile_PreservesTablesNotInIncoming()
    {
        var filePath = Path.Combine(_dir, "schema.json");
        var sut = BuildSut(new FileSchemaImportTargetOptions { OutputPath = filePath });

        var existing = DatabaseSchema.Create([SchemaDefinition.Create("app",
            tables: [Table.Create("audit_log")])]);
        await sut.Write(existing, TestContext.Current.CancellationToken);

        var incoming = DatabaseSchema.Create([SchemaDefinition.Create("app",
            tables: [Table.Create("users")])]);
        await sut.Write(incoming, TestContext.Current.CancellationToken);

        var result = await ReadSchema(filePath);
        result.Schemas.Single().Tables.Select(t => t.Name).ShouldBe(["audit_log", "users"], ignoreOrder: true);
    }

    [Fact]
    public async Task Write_None_ExistingFile_IncomingTableReplacesExisting()
    {
        var filePath = Path.Combine(_dir, "schema.json");
        var sut = BuildSut(new FileSchemaImportTargetOptions { OutputPath = filePath });

        var existing = DatabaseSchema.Create([SchemaDefinition.Create("app",
            tables: [Table.Create("users", columns: [Column.Create("old_col", SqlType.Text)])])]);
        await sut.Write(existing, TestContext.Current.CancellationToken);

        var incoming = DatabaseSchema.Create([SchemaDefinition.Create("app",
            tables: [Table.Create("users", columns: [Column.Create("new_col", SqlType.Text)])])]);
        await sut.Write(incoming, TestContext.Current.CancellationToken);

        var result = await ReadSchema(filePath);
        var usersTable = result.Schemas.Single().Tables.Single(t => t.Name == "users");
        usersTable.Columns.Select(c => c.Name).ShouldBe(["new_col"]);
    }

    // ── Partition mode: Schema (one file per schema namespace) ─────────────

    [Fact]
    public async Task Write_Schema_CreatesOneFilePerSchema()
    {
        var sut = BuildSut(new FileSchemaImportTargetOptions
        {
            OutputPath = _dir,
            Partition = ImportPartitionMode.Schema,
        });
        var schema = DatabaseSchema.Create([
            SchemaDefinition.Create("app"),
            SchemaDefinition.Create("audit"),
        ]);

        await sut.Write(schema, TestContext.Current.CancellationToken);

        File.Exists(Path.Combine(_dir, $"app.{JsonSchemaSerializer.FormatName}")).ShouldBeTrue();
        File.Exists(Path.Combine(_dir, $"audit.{JsonSchemaSerializer.FormatName}")).ShouldBeTrue();
    }

    [Fact]
    public async Task Write_Schema_EachFileContainsOnlyItsSchema()
    {
        var sut = BuildSut(new FileSchemaImportTargetOptions
        {
            OutputPath = _dir,
            Partition = ImportPartitionMode.Schema,
        });
        var schema = DatabaseSchema.Create([
            SchemaDefinition.Create("app", tables: [Table.Create("users")]),
            SchemaDefinition.Create("audit", tables: [Table.Create("logs")]),
        ]);

        await sut.Write(schema, TestContext.Current.CancellationToken);

        var app = await ReadSchema(Path.Combine(_dir, $"app.{JsonSchemaSerializer.FormatName}"));
        app.Schemas.Single().Name.ShouldBe("app");
        app.Schemas.Single().Tables.Single().Name.ShouldBe("users");

        var audit = await ReadSchema(Path.Combine(_dir, $"audit.{JsonSchemaSerializer.FormatName}"));
        audit.Schemas.Single().Name.ShouldBe("audit");
        audit.Schemas.Single().Tables.Single().Name.ShouldBe("logs");
    }

    // ── Partition mode: Table (one file per table) ──────────────────────────

    [Fact]
    public async Task Write_Table_CreatesOneFilePerTable()
    {
        var sut = BuildSut(new FileSchemaImportTargetOptions
        {
            OutputPath = _dir,
            Partition = ImportPartitionMode.Table,
        });
        var schema = DatabaseSchema.Create([SchemaDefinition.Create("app",
            tables: [Table.Create("users"), Table.Create("orders")])]);

        await sut.Write(schema, TestContext.Current.CancellationToken);

        File.Exists(Path.Combine(_dir, "app", $"users.{JsonSchemaSerializer.FormatName}")).ShouldBeTrue();
        File.Exists(Path.Combine(_dir, "app", $"orders.{JsonSchemaSerializer.FormatName}")).ShouldBeTrue();
    }

    [Fact]
    public async Task Write_Table_EachFileContainsOnlyItsTable()
    {
        var sut = BuildSut(new FileSchemaImportTargetOptions
        {
            OutputPath = _dir,
            Partition = ImportPartitionMode.Table,
        });
        var schema = DatabaseSchema.Create([SchemaDefinition.Create("app",
            tables: [Table.Create("users"), Table.Create("orders")])]);

        await sut.Write(schema, TestContext.Current.CancellationToken);

        var usersSchema = await ReadSchema(Path.Combine(_dir, "app", $"users.{JsonSchemaSerializer.FormatName}"));
        usersSchema.Schemas.Single().Tables.Single().Name.ShouldBe("users");

        var ordersSchema = await ReadSchema(Path.Combine(_dir, "app", $"orders.{JsonSchemaSerializer.FormatName}"));
        ordersSchema.Schemas.Single().Tables.Single().Name.ShouldBe("orders");
    }

    // ── Directory creation ──────────────────────────────────────────────────

    [Fact]
    public async Task Write_None_CreatesDirectoryIfMissing()
    {
        var filePath = Path.Combine(_dir, "nested", "deep", "schema.json");
        var sut = BuildSut(new FileSchemaImportTargetOptions { OutputPath = filePath });

        await sut.Write(DatabaseSchema.Create([]), TestContext.Current.CancellationToken);

        File.Exists(filePath).ShouldBeTrue();
    }
}
