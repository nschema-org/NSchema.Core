using Microsoft.Extensions.DependencyInjection;
using NSchema.Migration;
using NSchema.Schema;
using NSchema.State;

namespace NSchema.Tests.State;

public sealed class DefaultCurrentSchemaProviderTests
{
    private static readonly DatabaseSchema OnlineSchema = DatabaseSchema.Create([SchemaDefinition.Create("online")]);
    private static readonly DatabaseSchema OfflineSchema = DatabaseSchema.Create([SchemaDefinition.Create("offline")]);

    private sealed class FakeOnlineProvider : ISchemaProvider
    {
        public Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(OnlineSchema);
    }

    private sealed class FakeStateStore : ISchemaStateStore
    {
        public Task<DatabaseSchema?> Read(CancellationToken cancellationToken = default) =>
            Task.FromResult<DatabaseSchema?>(OfflineSchema);

        public Task Write(DatabaseSchema schema, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    // --- required: true (strict) ---

    [Fact]
    public void GetSource_Online_Required_ReturnsOnlineProvider()
    {
        var provider = new FakeOnlineProvider();
        var sut = new DefaultCurrentSchemaProvider(online: provider);

        sut.GetSource(SchemaSourceMode.Online).ShouldBeSameAs(provider);
    }

    [Fact]
    public void GetSource_Online_Required_WhenNotConfigured_Throws()
    {
        var sut = new DefaultCurrentSchemaProvider();

        Should.Throw<InvalidOperationException>(() => sut.GetSource(SchemaSourceMode.Online));
    }

    [Fact]
    public void GetSource_Online_Required_WhenOnlyOfflineConfigured_Throws()
    {
        var sut = new DefaultCurrentSchemaProvider(store: new FakeStateStore());

        Should.Throw<InvalidOperationException>(() => sut.GetSource(SchemaSourceMode.Online));
    }

    [Fact]
    public async Task GetSource_Offline_Required_ReturnsOfflineProvider()
    {
        var sut = new DefaultCurrentSchemaProvider(store: new FakeStateStore());

        var result = await sut.GetSource(SchemaSourceMode.Offline).GetSchema();

        result.ShouldBeSameAs(OfflineSchema);
    }

    [Fact]
    public void GetSource_Offline_Required_WhenNotConfigured_Throws()
    {
        var sut = new DefaultCurrentSchemaProvider();

        Should.Throw<InvalidOperationException>(() => sut.GetSource(SchemaSourceMode.Offline));
    }

    [Fact]
    public void GetSource_Offline_Required_WhenOnlyOnlineConfigured_Throws()
    {
        var sut = new DefaultCurrentSchemaProvider(online: new FakeOnlineProvider());

        Should.Throw<InvalidOperationException>(() => sut.GetSource(SchemaSourceMode.Offline));
    }

    // --- required: false (with fallback) ---

    [Fact]
    public void GetSource_Online_NotRequired_ReturnsOnline_WhenConfigured()
    {
        var provider = new FakeOnlineProvider();
        var sut = new DefaultCurrentSchemaProvider(online: provider);

        sut.GetSource(SchemaSourceMode.Online, required: false).ShouldBeSameAs(provider);
    }

    [Fact]
    public async Task GetSource_Online_NotRequired_FallsBackToOffline_WhenOnlineNotConfigured()
    {
        var sut = new DefaultCurrentSchemaProvider(store: new FakeStateStore());

        var result = await sut.GetSource(SchemaSourceMode.Online, required: false).GetSchema();

        result.ShouldBeSameAs(OfflineSchema);
    }

    [Fact]
    public void GetSource_Offline_NotRequired_ReturnsOffline_WhenConfigured()
    {
        var sut = new DefaultCurrentSchemaProvider(
            online: new FakeOnlineProvider(),
            store: new FakeStateStore());

        // When offline is available it should be returned, not the online fallback.
        var source = sut.GetSource(SchemaSourceMode.Offline, required: false);
        source.ShouldNotBeSameAs(new FakeOnlineProvider()); // it's the offline provider
    }

    [Fact]
    public void GetSource_Offline_NotRequired_FallsBackToOnline_WhenOfflineNotConfigured()
    {
        var provider = new FakeOnlineProvider();
        var sut = new DefaultCurrentSchemaProvider(online: provider);

        sut.GetSource(SchemaSourceMode.Offline, required: false).ShouldBeSameAs(provider);
    }

    [Fact]
    public void GetSource_NotRequired_WhenNeitherConfigured_Throws()
    {
        var sut = new DefaultCurrentSchemaProvider();

        Should.Throw<InvalidOperationException>(() => sut.GetSource(SchemaSourceMode.Offline, required: false));
        Should.Throw<InvalidOperationException>(() => sut.GetSource(SchemaSourceMode.Online, required: false));
    }

    // --- DI integration ---

    [Fact]
    public void UseCurrentSchema_RegistersOnlineSource()
    {
        var builder = NSchemaApplication.CreateBuilder();
        builder.UseCurrentSchema<FakeOnlineProvider>();
        using var app = builder.Build();
        var current = app.Services.GetRequiredService<ICurrentSchemaProvider>();

        // Online is configured; offline is not.
        current.GetSource(SchemaSourceMode.Online).ShouldNotBeNull();
        Should.Throw<InvalidOperationException>(() => current.GetSource(SchemaSourceMode.Offline));
    }

    [Fact]
    public void UseStateStore_RegistersOfflineSource()
    {
        var builder = NSchemaApplication.CreateBuilder();
        builder.UseStateStore(new FakeStateStore());
        using var app = builder.Build();
        var current = app.Services.GetRequiredService<ICurrentSchemaProvider>();

        // Offline is configured; online is not.
        current.GetSource(SchemaSourceMode.Offline).ShouldNotBeNull();
        Should.Throw<InvalidOperationException>(() => current.GetSource(SchemaSourceMode.Online));
    }

    [Fact]
    public async Task UseCurrentSchema_AndStateStore_BothSourcesAvailable()
    {
        var builder = NSchemaApplication.CreateBuilder();
        builder
            .UseCurrentSchema<FakeOnlineProvider>()
            .UseStateStore(new FakeStateStore());
        using var app = builder.Build();
        var current = app.Services.GetRequiredService<ICurrentSchemaProvider>();

        var online = await current.GetSource(SchemaSourceMode.Online).GetSchema();
        var offline = await current.GetSource(SchemaSourceMode.Offline).GetSchema();

        online.Schemas.ShouldHaveSingleItem().Name.ShouldBe("online");
        offline.Schemas.ShouldHaveSingleItem().Name.ShouldBe("offline");
    }
}
