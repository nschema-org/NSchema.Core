using Microsoft.Extensions.DependencyInjection;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Schemas;
using NSchema.State;

namespace NSchema.Tests.State;

public sealed class CurrentSchemaProviderTests
{
    private static readonly DatabaseSchema _onlineSchema = new DatabaseSchema([new SchemaDefinition("online")]);
    private static readonly DatabaseSchema _offlineSchema = new DatabaseSchema([new SchemaDefinition("offline")]);
    private static readonly ISchemaStateSerializer _serializer = new SchemaStateSerializer();

    private static CurrentSchemaProvider Create(ISchemaProvider? online = null, ISchemaStateStore? store = null) =>
        new(_serializer, online, store);

    private sealed class FakeOnlineProvider : ISchemaProvider
    {
        public ValueTask<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_onlineSchema);
    }

    // Returns the offline schema as a serialized payload, like a real store.
    private sealed class FakeStateStore : ISchemaStateStore
    {
        public Task<ReadOnlyMemory<byte>?> Read(CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(_serializer.Serialize(_offlineSchema));

        public Task Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    // --- required: true (strict) ---

    [Fact]
    public async Task GetSchema_Online_Required_ReturnsOnlineSchema()
    {
        var sut = Create(online: new FakeOnlineProvider());

        var result = await sut.GetSchema(SchemaSourceMode.Online, null, true, TestContext.Current.CancellationToken);

        result.ShouldBe(_onlineSchema);
    }

    [Fact]
    public void GetSchema_Online_Required_WhenNotConfigured_Throws()
    {
        var sut = Create();

        Should.Throw<InvalidOperationException>(() => sut.GetSchema(SchemaSourceMode.Online, null));
    }

    [Fact]
    public void GetSchema_Online_Required_WhenOnlyOfflineConfigured_Throws()
    {
        var sut = Create(store: new FakeStateStore());

        Should.Throw<InvalidOperationException>(() => sut.GetSchema(SchemaSourceMode.Online, null));
    }

    [Fact]
    public async Task GetSchema_Offline_Required_ReturnsOfflineSchema()
    {
        var sut = Create(store: new FakeStateStore());

        var result = await sut.GetSchema(SchemaSourceMode.Offline, null, true, TestContext.Current.CancellationToken);

        result.Schemas.ShouldHaveSingleItem().Name.ShouldBe("offline");
    }

    [Fact]
    public void GetSchema_Offline_Required_WhenNotConfigured_Throws()
    {
        var sut = Create();

        Should.Throw<InvalidOperationException>(() => sut.GetSchema(SchemaSourceMode.Offline, null, true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GetSchema_Offline_Required_WhenOnlyOnlineConfigured_Throws()
    {
        var sut = Create(online: new FakeOnlineProvider());

        Should.Throw<InvalidOperationException>(() => sut.GetSchema(SchemaSourceMode.Offline, null));
    }

    // --- required: false (with fallback) ---

    [Fact]
    public async Task GetSchema_Online_NotRequired_ReturnsOnlineSchema_WhenConfigured()
    {
        var sut = Create(online: new FakeOnlineProvider());

        var result = await sut.GetSchema(SchemaSourceMode.Online, null, required: false, TestContext.Current.CancellationToken);

        result.ShouldBe(_onlineSchema);
    }

    [Fact]
    public async Task GetSchema_Online_NotRequired_FallsBackToOffline_WhenOnlineNotConfigured()
    {
        var sut = Create(store: new FakeStateStore());

        var result = await sut.GetSchema(SchemaSourceMode.Online, null, required: false, TestContext.Current.CancellationToken);

        result.Schemas.ShouldHaveSingleItem().Name.ShouldBe("offline");
    }

    [Fact]
    public async Task GetSchema_Offline_NotRequired_ReturnsOfflineSchema_WhenConfigured()
    {
        var sut = Create(online: new FakeOnlineProvider(), store: new FakeStateStore());

        var result = await sut.GetSchema(SchemaSourceMode.Offline, null, required: false, TestContext.Current.CancellationToken);

        result.Schemas.ShouldHaveSingleItem().Name.ShouldBe("offline");
    }

    [Fact]
    public async Task GetSchema_Offline_NotRequired_FallsBackToOnline_WhenOfflineNotConfigured()
    {
        var sut = Create(online: new FakeOnlineProvider());

        var result = await sut.GetSchema(SchemaSourceMode.Offline, null, required: false, TestContext.Current.CancellationToken);

        result.ShouldBe(_onlineSchema);
    }

    [Fact]
    public void GetSchema_NotRequired_WhenNeitherConfigured_Throws()
    {
        var sut = Create();

        Should.Throw<InvalidOperationException>(() => sut.GetSchema(SchemaSourceMode.Offline, null, required: false));
        Should.Throw<InvalidOperationException>(() => sut.GetSchema(SchemaSourceMode.Online, null, required: false));
    }

    // --- DI integration ---

    [Fact]
    public async Task UseCurrentSchema_RegistersOnlineSource()
    {
        var builder = NSchemaApplication.CreateBuilder();
        builder.UseCurrentSchema<FakeOnlineProvider>();
        using var app = builder.Build();
        var current = app.Services.GetRequiredService<ICurrentSchemaProvider>();

        var online = await current.GetSchema(SchemaSourceMode.Online, null, true, TestContext.Current.CancellationToken);
        online.ShouldBe(_onlineSchema);
        Should.Throw<InvalidOperationException>(() => current.GetSchema(SchemaSourceMode.Offline, null));
    }

    [Fact]
    public async Task UseStateStore_RegistersOfflineSource()
    {
        var builder = NSchemaApplication.CreateBuilder();
        builder.UseStateStore(new FakeStateStore());
        using var app = builder.Build();
        var current = app.Services.GetRequiredService<ICurrentSchemaProvider>();

        var offline = await current.GetSchema(SchemaSourceMode.Offline, null, true, TestContext.Current.CancellationToken);
        offline.Schemas.ShouldHaveSingleItem().Name.ShouldBe("offline");
        Should.Throw<InvalidOperationException>(() => current.GetSchema(SchemaSourceMode.Online, null));
    }

    [Fact]
    public async Task UseCurrentSchema_AndStateStore_BothSourcesAvailable()
    {
        var builder = NSchemaApplication.CreateBuilder();
        builder.UseCurrentSchema<FakeOnlineProvider>().UseStateStore(new FakeStateStore());
        using var app = builder.Build();
        var current = app.Services.GetRequiredService<ICurrentSchemaProvider>();

        var online = await current.GetSchema(SchemaSourceMode.Online, null, true, TestContext.Current.CancellationToken);
        var offline = await current.GetSchema(SchemaSourceMode.Offline, null, true, TestContext.Current.CancellationToken);

        online.Schemas.ShouldHaveSingleItem().Name.ShouldBe("online");
        offline.Schemas.ShouldHaveSingleItem().Name.ShouldBe("offline");
    }
}
