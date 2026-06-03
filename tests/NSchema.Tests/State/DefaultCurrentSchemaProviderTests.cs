using Microsoft.Extensions.DependencyInjection;
using NSchema.Migration.Sources;
using NSchema.Schema;
using NSchema.State;

namespace NSchema.Tests.State;

public sealed class DefaultCurrentSchemaProviderTests
{
    private static readonly DatabaseSchema _onlineSchema = DatabaseSchema.Create([SchemaDefinition.Create("online")]);
    private static readonly DatabaseSchema _offlineSchema = DatabaseSchema.Create([SchemaDefinition.Create("offline")]);

    private sealed class FakeOnlineProvider : ISchemaProvider
    {
        public Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(_onlineSchema);
    }

    private sealed class FakeStateStore : ISchemaStateStore
    {
        public Task<DatabaseSchema?> Read(CancellationToken cancellationToken = default) =>
            Task.FromResult<DatabaseSchema?>(_offlineSchema);

        public Task Write(DatabaseSchema schema, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    // --- required: true (strict) ---

    [Fact]
    public async Task GetSchema_Online_Required_ReturnsOnlineSchema()
    {
        var sut = new DefaultCurrentSchemaProvider(online: new FakeOnlineProvider());

        var result = await sut.GetSchema(SchemaSourceMode.Online, null, true, TestContext.Current.CancellationToken);

        result.ShouldBe(_onlineSchema);
    }

    [Fact]
    public void GetSchema_Online_Required_WhenNotConfigured_Throws()
    {
        var sut = new DefaultCurrentSchemaProvider();

        Should.Throw<InvalidOperationException>(() => sut.GetSchema(SchemaSourceMode.Online, null));
    }

    [Fact]
    public void GetSchema_Online_Required_WhenOnlyOfflineConfigured_Throws()
    {
        var sut = new DefaultCurrentSchemaProvider(store: new FakeStateStore());

        Should.Throw<InvalidOperationException>(() => sut.GetSchema(SchemaSourceMode.Online, null));
    }

    [Fact]
    public async Task GetSchema_Offline_Required_ReturnsOfflineSchema()
    {
        var sut = new DefaultCurrentSchemaProvider(store: new FakeStateStore());

        var result = await sut.GetSchema(SchemaSourceMode.Offline, null, true, TestContext.Current.CancellationToken);

        result.ShouldBe(_offlineSchema);
    }

    [Fact]
    public void GetSchema_Offline_Required_WhenNotConfigured_Throws()
    {
        var sut = new DefaultCurrentSchemaProvider();

        Should.Throw<InvalidOperationException>(() => sut.GetSchema(SchemaSourceMode.Offline, null, true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GetSchema_Offline_Required_WhenOnlyOnlineConfigured_Throws()
    {
        var sut = new DefaultCurrentSchemaProvider(online: new FakeOnlineProvider());

        Should.Throw<InvalidOperationException>(() => sut.GetSchema(SchemaSourceMode.Offline, null));
    }

    // --- required: false (with fallback) ---

    [Fact]
    public async Task GetSchema_Online_NotRequired_ReturnsOnlineSchema_WhenConfigured()
    {
        var sut = new DefaultCurrentSchemaProvider(online: new FakeOnlineProvider());

        var result = await sut.GetSchema(SchemaSourceMode.Online, null, required: false, TestContext.Current.CancellationToken);

        result.ShouldBe(_onlineSchema);
    }

    [Fact]
    public async Task GetSchema_Online_NotRequired_FallsBackToOffline_WhenOnlineNotConfigured()
    {
        var sut = new DefaultCurrentSchemaProvider(store: new FakeStateStore());

        var result = await sut.GetSchema(SchemaSourceMode.Online, null, required: false, TestContext.Current.CancellationToken);

        result.ShouldBe(_offlineSchema);
    }

    [Fact]
    public async Task GetSchema_Offline_NotRequired_ReturnsOfflineSchema_WhenConfigured()
    {
        var sut = new DefaultCurrentSchemaProvider(online: new FakeOnlineProvider(), store: new FakeStateStore());

        var result = await sut.GetSchema(SchemaSourceMode.Offline, null, required: false, TestContext.Current.CancellationToken);

        result.ShouldBe(_offlineSchema);
    }

    [Fact]
    public async Task GetSchema_Offline_NotRequired_FallsBackToOnline_WhenOfflineNotConfigured()
    {
        var sut = new DefaultCurrentSchemaProvider(online: new FakeOnlineProvider());

        var result = await sut.GetSchema(SchemaSourceMode.Offline, null, required: false, TestContext.Current.CancellationToken);

        result.ShouldBe(_onlineSchema);
    }

    [Fact]
    public void GetSchema_NotRequired_WhenNeitherConfigured_Throws()
    {
        var sut = new DefaultCurrentSchemaProvider();

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
        offline.ShouldBe(_offlineSchema);
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
