using Microsoft.Extensions.DependencyInjection;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.EndToEnd;

/// <summary>
/// Drives <c>Refresh</c> and the offline <c>Plan</c>-against-captured-state workflow through a real
/// <see cref="NSchemaApplication"/> backed by an on-disk file state store.
/// </summary>
public sealed class RefreshEndToEndTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string _statePath;

    public RefreshEndToEndTests()
    {
        Directory.CreateDirectory(_tempDir);
        _statePath = Path.Combine(_tempDir, "state.json");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteJson(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static DatabaseSchema LiveSchema() => DatabaseSchema.Create([SchemaDefinition.Create("app", tables:
        [Table.Create("users", columns: [Column.Create("id", SqlType.Int)])])]);

    [Fact]
    public async Task Refresh_WritesLiveSchemaToStateStore()
    {
        var store = new RecordingStateStore();
        using var app = NSchemaApplication.CreateBuilder()
            .UseStateStore(store)
            .Tap(b => b.Services.AddKeyedSingleton<ISchemaProvider>(NSchemaKeys.OnlineSchemaProvider, new InMemorySchemaProvider(LiveSchema())))
            .Build();

        await app.Refresh(TestContext.Current.CancellationToken);

        store.Written.ShouldNotBeNull().Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public async Task Refresh_ThenOfflinePlan_AgainstCapturedState_SeesNoChanges()
    {
        // 1. Capture the live schema to the store via Refresh.
        using (var capture = NSchemaApplication.CreateBuilder()
            .UseFileStateStore(_statePath)
            .Tap(b => b.Services.AddKeyedSingleton<ISchemaProvider>(NSchemaKeys.OnlineSchemaProvider, new InMemorySchemaProvider(LiveSchema())))
            .Build())
        {
            await capture.Refresh(TestContext.Current.CancellationToken);
        }

        // 2. Plan offline against the captured state with a matching desired schema — no live database involved.
        var reporter = new RecordingReporter();
        var desired = WriteJson("schema.json",
            """
            { "schemas": [{ "name": "app", "tables": [{ "name": "users", "columns": [{ "name": "id", "type": "int" }] }] }], "droppedSchemas": [] }
            """);

        using var planner = NSchemaApplication.CreateBuilder()
            .UseFileStateStore(_statePath)
            .AddJsonSchema(desired)
            .AddReporter(RecordingReporter.FormatName, reporter)
            .WithRenderer(RecordingReporter.FormatName)
            .Build();

        await planner.Plan(TestContext.Current.CancellationToken);

        reporter.Diff.ShouldNotBeNull().IsEmpty.ShouldBeTrue();
    }
}
