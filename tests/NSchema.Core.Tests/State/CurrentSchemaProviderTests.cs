using Microsoft.Extensions.DependencyInjection;
using NSchema.Current;
using NSchema.Current.Backends;
using NSchema.Current.Domain.Models;
using NSchema.Current.Storage;
using NSchema.Current.Storage.Backends;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Schemas;

namespace NSchema.Tests.State;

public sealed class CurrentSchemaProviderTests
{
    private static readonly DatabaseSchema _onlineSchema = new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("online"))]);
    private static readonly DatabaseSchema _offlineSchema = new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("offline"))]);
    private static readonly ISchemaStateSerializer _serializer = new SchemaStateSerializer();

    private static CurrentSchemaProvider Create(ISchemaIntrospector? online = null, ISchemaStateStore? store = null) =>
        new(new SchemaStateManager(_serializer, store), online);

    private sealed class FakeIntrospector : ISchemaIntrospector
    {
        public ValueTask<DatabaseSchema> GetSchema(SchemaScope scope, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_onlineSchema);
    }

    // Returns the offline schema as a serialized payload, like a real store.
    private sealed class FakeStateStore : ISchemaStateStore
    {
        public Task<ReadOnlyMemory<byte>?> Read(CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(_serializer.Serialize(new SchemaState(_offlineSchema)));

        public Task Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task GetSchema_Online_ReturnsOnlineSchema()
    {
        var sut = Create(online: new FakeIntrospector());

        var result = await sut.GetSchema(SchemaSourceMode.Online, SchemaScope.All, TestContext.Current.CancellationToken);

        result.Require().ShouldBe(_onlineSchema);
    }

    [Fact]
    public async Task GetSchema_Online_ReAppliesTheScope_WhenTheIntrospectorOverReturns()
    {
        // The fake ignores its scope entirely — the provider's re-filter is what keeps scoping honest.
        var sut = Create(online: new FakeIntrospector());

        var result = await sut.GetSchema(SchemaSourceMode.Online, SchemaScope.Of(new SqlIdentifier("other")), TestContext.Current.CancellationToken);

        result.Require().Schemas.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSchema_Online_WhenNotConfigured_Fails()
    {
        var sut = Create();

        var result = await sut.GetSchema(SchemaSourceMode.Online, SchemaScope.All, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("live database provider");
    }

    [Fact]
    public async Task GetSchema_Online_WhenOnlyOfflineConfigured_Fails_WithoutFallingBack()
    {
        var sut = Create(store: new FakeStateStore());

        var result = await sut.GetSchema(SchemaSourceMode.Online, SchemaScope.All, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task GetSchema_Offline_ReturnsOfflineSchema()
    {
        var sut = Create(store: new FakeStateStore());

        var result = await sut.GetSchema(SchemaSourceMode.Offline, SchemaScope.All, TestContext.Current.CancellationToken);

        ShouldlyIdentifierExtensions.ShouldBe(result.Require().Schemas.ShouldHaveSingleItem().Name, "offline");
    }

    [Fact]
    public async Task GetSchema_Offline_WhenNotConfigured_Fails()
    {
        var sut = Create();

        var result = await sut.GetSchema(SchemaSourceMode.Offline, SchemaScope.All, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("state store");
    }

    [Fact]
    public async Task GetSchema_Offline_WhenOnlyOnlineConfigured_Fails_WithoutFallingBack()
    {
        var sut = Create(online: new FakeIntrospector());

        var result = await sut.GetSchema(SchemaSourceMode.Offline, SchemaScope.All, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
    }

    // --- DI integration ---

    [Fact]
    public async Task UseCurrentSchema_RegistersOnlineSource()
    {
        var builder = NSchemaApplication.CreateBuilder();
        builder.UseCurrentSchema<FakeIntrospector>();
        using var app = builder.Build();
        var current = app.Services.GetRequiredService<ICurrentSchemaProvider>();

        var online = await current.GetSchema(SchemaSourceMode.Online, SchemaScope.All, TestContext.Current.CancellationToken);
        var offline = await current.GetSchema(SchemaSourceMode.Offline, SchemaScope.All, TestContext.Current.CancellationToken);

        online.Require().ShouldBe(_onlineSchema);
        offline.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task UseStateStore_RegistersOfflineSource()
    {
        var builder = NSchemaApplication.CreateBuilder();
        builder.UseStateStore(new FakeStateStore());
        using var app = builder.Build();
        var current = app.Services.GetRequiredService<ICurrentSchemaProvider>();

        var offline = await current.GetSchema(SchemaSourceMode.Offline, SchemaScope.All, TestContext.Current.CancellationToken);
        var online = await current.GetSchema(SchemaSourceMode.Online, SchemaScope.All, TestContext.Current.CancellationToken);

        ShouldlyIdentifierExtensions.ShouldBe(offline.Require().Schemas.ShouldHaveSingleItem().Name, "offline");
        online.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task UseCurrentSchema_AndStateStore_BothSourcesAvailable()
    {
        var builder = NSchemaApplication.CreateBuilder();
        builder.UseCurrentSchema<FakeIntrospector>().UseStateStore(new FakeStateStore());
        using var app = builder.Build();
        var current = app.Services.GetRequiredService<ICurrentSchemaProvider>();

        var online = await current.GetSchema(SchemaSourceMode.Online, SchemaScope.All, TestContext.Current.CancellationToken);
        var offline = await current.GetSchema(SchemaSourceMode.Offline, SchemaScope.All, TestContext.Current.CancellationToken);

        ShouldlyIdentifierExtensions.ShouldBe(online.Require().Schemas.ShouldHaveSingleItem().Name, "online");
        ShouldlyIdentifierExtensions.ShouldBe(offline.Require().Schemas.ShouldHaveSingleItem().Name, "offline");
    }
}
