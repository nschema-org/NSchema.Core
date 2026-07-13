using NSchema.Current;
using NSchema.Operations;
using NSchema.Operations.Progress;
using NSchema.Project.Ddl;
using NSchema.Project.Domain;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Domains;
using NSchema.Project.Domain.Models.Enums;
using NSchema.Project.Domain.Models.Extensions;
using NSchema.Project.Domain.Models.Routines;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Sequences;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Views;

namespace NSchema.Tests.Operations;

public sealed class ImportOperationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly ICurrentSchemaProvider _currentSchema = Substitute.For<ICurrentSchemaProvider>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();

    // Tables carry a column because the DDL grammar has no empty-table form.
    private static Table MakeTable(string name) => new(new SqlIdentifier(name), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)]);

    private readonly DatabaseSchema _schema = new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"),
        Tables: [MakeTable("users"), MakeTable("orders")])]);

    public ImportOperationTests()
    {
        Directory.CreateDirectory(_dir);
        Source(_schema);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Source(DatabaseSchema schema) => _currentSchema
        .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<SchemaScope>(), Arg.Any<CancellationToken>())
        .Returns(Result.Success(schema));

    private ImportOperation BuildSut() => new(_currentSchema, _progress);

    private Task Execute(ImportArguments arguments) =>
        BuildSut().Execute(arguments, TestContext.Current.CancellationToken);

    private static async Task<DatabaseSchema> ReadSchema(string path)
    {
        var text = await File.ReadAllTextAsync(path);
        return DdlReader.Instance.Read(text).Require().Schema;
    }

    private string ObjectPath(string type, string name) => Path.Combine(_dir, "app", type, $"{name}.sql");
    private string HeaderPath => Path.Combine(_dir, "app", "schema.sql");

    // Combines every .sql file written under the output directory, as the desired-schema providers would.
    private async Task<DatabaseSchema> ReadAll()
    {
        var combined = new DatabaseSchema([]);
        foreach (var file in Directory.EnumerateFiles(_dir, "*.sql", SearchOption.AllDirectories))
        {
            combined = SchemaAggregator.Combine(combined, await ReadSchema(file)).Require();
        }
        return combined;
    }

    // ── Result payload ──────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_ReturnsTheImportedSchemaAndEveryWrittenFile()
    {
        // Act
        var result = await BuildSut().Execute(new ImportArguments { OutputDirectory = _dir }, TestContext.Current.CancellationToken);

        // Assert — the result reports what was read and exactly which files it wrote (the schema header + one per table).
        result.IsSuccess.ShouldBeTrue();
        result.Value!.ImportedSchema.ShouldBe(_schema);
        result.Value!.WrittenFiles.ShouldBe([HeaderPath, ObjectPath("tables", "users"), ObjectPath("tables", "orders")], ignoreOrder: true);
    }

    // ── Source fetching ─────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_FetchesSchemaFromOnlineSource()
    {
        await Execute(new ImportArguments { OutputDirectory = _dir });

        await _currentSchema.Received(1).GetSchema(
            SchemaSourceMode.Online, Arg.Any<SchemaScope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PassesSchemaFilterToSource()
    {
        var arguments = new ImportArguments { OutputDirectory = _dir, Scope = SchemaScope.Of(new SqlIdentifier("app"), new SqlIdentifier("audit")) };

        await Execute(arguments);

        await _currentSchema.Received(1).GetSchema(
            SchemaSourceMode.Online, arguments.Scope, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ReportsVerboseCensusAndPerFileWrites()
    {
        await Execute(new ImportArguments { OutputDirectory = _dir });

        // A census of what was fetched...
        _progress.Received(1).Report(OperationProgress.Detail("Fetched 1 schema, 2 tables from the database."));
        // ...and a line per object file, marked "Wrote" because nothing existed to merge into.
        _progress.Received(1).Report(OperationProgress.Detail($"Wrote {ObjectPath("tables", "users")}."));
        _progress.Received(1).Report(OperationProgress.Detail($"Wrote {ObjectPath("tables", "orders")}."));
    }

    [Fact]
    public async Task Execute_ReImport_ReportsMergeIntoExistingFile()
    {
        await Execute(new ImportArguments { OutputDirectory = _dir });
        _progress.ClearReceivedCalls();

        // A second import of the same object merges into the file written by the first.
        await Execute(new ImportArguments { OutputDirectory = _dir });

        _progress.Received(1).Report(OperationProgress.Detail($"Merged into {ObjectPath("tables", "users")}."));
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
        File.Exists(ObjectPath("routines", "calc")).ShouldBeTrue();
        File.Exists(ObjectPath("routines", "sync")).ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_EachFileContainsOnlyItsObject()
    {
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir });

        (await ReadSchema(ObjectPath("tables", "users"))).Schemas.Single().Tables.Single().Name.ShouldBe("users");
        (await ReadSchema(ObjectPath("views", "active"))).Schemas.Single().Views.Single().Name.ShouldBe("active");
        (await ReadSchema(ObjectPath("routines", "calc"))).Schemas.Single().Routines.Single().Name.ShouldBe("calc");
        (await ReadSchema(ObjectPath("routines", "sync"))).Schemas.Single().Routines.Single().Name.ShouldBe("sync");

        // An object file carries nothing but its own object.
        var users = (await ReadSchema(ObjectPath("tables", "users"))).Schemas.Single();
        users.Views.ShouldBeEmpty();
        users.Routines.ShouldBeEmpty();
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
        header.Routines.ShouldBeEmpty();
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
        app.Routines.Select(r => r.Name).ShouldBe(["calc", "sync"], ignoreOrder: true);
        app.Enums.ShouldHaveSingleItem().Name.ShouldBe("status");
        app.Sequences.ShouldHaveSingleItem().Name.ShouldBe("order_id");
    }

    [Fact]
    public async Task Execute_MultipleSchemas_EachGetsItsOwnDirectory()
    {
        Source(new DatabaseSchema([
            new SchemaDefinition(new SqlIdentifier("app"), Tables: [MakeTable("users")]),
            new SchemaDefinition(new SqlIdentifier("audit"), Tables: [MakeTable("logs")]),
        ]));

        await Execute(new ImportArguments { OutputDirectory = _dir });

        (await ReadSchema(Path.Combine(_dir, "app", "tables", "users.sql"))).Schemas.Single().Tables.Single().Name.ShouldBe("users");
        (await ReadSchema(Path.Combine(_dir, "audit", "tables", "logs.sql"))).Schemas.Single().Tables.Single().Name.ShouldBe("logs");
    }

    [Fact]
    public async Task Execute_ObjectFilesCarryNoSchemaStatement()
    {
        // Only the header declares the schema; object files hold just their object, so the declaration
        // doesn't repeat across every file.
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir });

        foreach (var type in new[] { "tables", "views", "routines" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(_dir, "app", type), "*.sql"))
            {
                var text = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
                text.ShouldNotContain("CREATE SCHEMA", customMessage: file);
            }
        }
        (await File.ReadAllTextAsync(HeaderPath, TestContext.Current.CancellationToken)).ShouldContain("CREATE SCHEMA app;");
    }

    // ── Additive re-import (merge) ───────────────────────────────────────────

    [Fact]
    public async Task Execute_ReimportPreservesObjectsNotInIncoming()
    {
        // Each object is its own file, so an object absent from a later import is simply not rewritten.
        Source(new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"), Tables: [MakeTable("audit_log")])]));
        await Execute(new ImportArguments { OutputDirectory = _dir });

        Source(new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"), Tables: [MakeTable("users")])]));
        await Execute(new ImportArguments { OutputDirectory = _dir });

        (await ReadAll()).Schemas.Single().Tables.Select(t => t.Name).ShouldBe(["audit_log", "users"], ignoreOrder: true);
    }

    [Fact]
    public async Task Execute_ReimportReplacesAnObjectInPlace()
    {
        Source(new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"),
            Tables: [new Table(new SqlIdentifier("users"), Columns: [new Column(new SqlIdentifier("old_col"), SqlType.Text)])])]));
        await Execute(new ImportArguments { OutputDirectory = _dir });

        Source(new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"),
            Tables: [new Table(new SqlIdentifier("users"), Columns: [new Column(new SqlIdentifier("new_col"), SqlType.Text)])])]));
        await Execute(new ImportArguments { OutputDirectory = _dir });

        var users = (await ReadSchema(ObjectPath("tables", "users"))).Schemas.Single().Tables.Single(t => t.Name.Value.Equals("users"));
        users.Columns.Select(c => c.Name).ShouldBe(["new_col"]);
    }

    [Fact]
    public async Task Execute_ReimportReplacesHeaderObjects()
    {
        // The header file holds schema-level objects (enums, sequences, domains); a re-import must replace (not
        // duplicate) them.
        var schema = new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"),
            Enums: [new EnumType(new SqlIdentifier("status"), ["a"])],
            Sequences: [new Sequence(new SqlIdentifier("order_id"), new SequenceOptions(StartWith: 1))],
            Domains: [new DomainDefinition(new SqlIdentifier("typeid"), SqlType.Text)])]);

        Source(schema);
        await Execute(new ImportArguments { OutputDirectory = _dir });

        Source(new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"),
            Enums: [new EnumType(new SqlIdentifier("status"), ["a", "b"])],
            Sequences: [new Sequence(new SqlIdentifier("order_id"), new SequenceOptions(StartWith: 100))],
            Domains: [new DomainDefinition(new SqlIdentifier("typeid"), SqlType.VarChar(64))])]));
        await Execute(new ImportArguments { OutputDirectory = _dir });

        var header = (await ReadSchema(HeaderPath)).Schemas.Single();
        header.Enums.ShouldHaveSingleItem().Values.ShouldBe(["a", "b"]); // incoming wins
        header.Sequences.ShouldHaveSingleItem().Options.StartWith.ShouldBe(100);
        header.Domains.ShouldHaveSingleItem().DataType.ShouldBe(SqlType.VarChar(64)); // incoming wins, no duplicate
    }

    // ── Extensions (database-global, root-level) ─────────────────────────────

    [Fact]
    public async Task Execute_WritesExtensionsToTopLevelFile()
    {
        Source(new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"), Tables: [MakeTable("users")])],
            Extensions: [new Extension(new SqlIdentifier("citext")), new Extension(new SqlIdentifier("postgis"), Version: "3.4")]));

        await Execute(new ImportArguments { OutputDirectory = _dir });

        // Extensions land in a single top-level file, not under any per-schema directory.
        var extensions = (await ReadSchema(Path.Combine(_dir, "extensions.sql"))).Extensions;
        extensions.Select(e => e.Name).ShouldBe(["citext", "postgis"], ignoreOrder: true);
    }

    [Fact]
    public async Task Execute_NoExtensions_WritesNoExtensionsFile()
    {
        await Execute(new ImportArguments { OutputDirectory = _dir });

        File.Exists(Path.Combine(_dir, "extensions.sql")).ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_ReimportMergesExtensionsAdditively()
    {
        Source(new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"), Tables: [MakeTable("users")])],
            Extensions: [new Extension(new SqlIdentifier("citext"))]));
        await Execute(new ImportArguments { OutputDirectory = _dir });

        Source(new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"), Tables: [MakeTable("users")])],
            Extensions: [new Extension(new SqlIdentifier("postgis"), Version: "3.4")]));
        await Execute(new ImportArguments { OutputDirectory = _dir });

        var extensions = (await ReadSchema(Path.Combine(_dir, "extensions.sql"))).Extensions;
        extensions.Select(e => e.Name).ShouldBe(["citext", "postgis"], ignoreOrder: true);
    }

    // ── Canonical layout ─────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_WritesFormatterCanonicalDdl()
    {
        // Import output must already be in the formatter's canonical layout, so running `fmt` over an
        // imported file changes nothing. This is the invariant that keeps the two DDL paths from drifting.
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir });

        foreach (var file in Directory.EnumerateFiles(_dir, "*.sql", SearchOption.AllDirectories))
        {
            var text = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
            DdlFormatter.Instance.Format(text).ShouldBe(text, $"{file} is not formatter-canonical");
        }
    }

    private static DatabaseSchema RichSchema() => new([new SchemaDefinition(new SqlIdentifier("app"),
        Tables: [MakeTable("users"), MakeTable("orders")],
        Views: [new View(new SqlIdentifier("active"), new SqlText("SELECT 1"))],
        Routines:
        [
            new Routine(new SqlIdentifier("calc"), RoutineKind.Function, new SqlText(""), new SqlText("RETURNS int LANGUAGE sql AS $$ SELECT 1 $$")),
            new Routine(new SqlIdentifier("sync"), RoutineKind.Procedure, new SqlText(""), new SqlText("LANGUAGE sql AS $$ SELECT 1 $$")),
        ],
        Enums: [new EnumType(new SqlIdentifier("status"), ["a"])],
        Sequences: [new Sequence(new SqlIdentifier("order_id"), new SequenceOptions(StartWith: 1))])]);
}
