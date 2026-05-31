using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Schema;
using NSchema.State;

namespace NSchema.Tests.Hosting;

public sealed class DefaultStateCapturerTests
{
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();

    private DefaultStateCapturer CreateSut(
        ICurrentSchemaProvider currentProvider,
        ISchemaStateStore? store,
        MigrationOptions? options = null
    ) => new(Options.Create(options ?? new MigrationOptions()), _reporter, currentProvider, store);

    private static ICurrentSchemaProvider WithOnline(DatabaseSchema schema)
    {
        var source = Substitute.For<ISchemaProvider>();
        source.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(schema);

        var p = Substitute.For<ICurrentSchemaProvider>();
        p.GetSource(SchemaSourceMode.Online, required: true).Returns(source);
        return p;
    }

    private static ICurrentSchemaProvider WithoutOnline()
    {
        var p = Substitute.For<ICurrentSchemaProvider>();
        p.GetSource(SchemaSourceMode.Online, required: true)
            .Returns(_ => throw new InvalidOperationException("No online schema provider is registered."));
        return p;
    }

    [Fact]
    public async Task Capture_NoStore_ReturnsFalse()
    {
        // Arrange
        var sut = CreateSut(WithoutOnline(), store: null);

        // Act
        var result = await sut.Capture();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Capture_StoreButNoOnlineProvider_Throws()
    {
        // Arrange
        var store = Substitute.For<ISchemaStateStore>();
        var sut = CreateSut(WithoutOnline(), store);

        // Act / Assert
        await Should.ThrowAsync<InvalidOperationException>(() => sut.Capture());
    }

    [Fact]
    public async Task Capture_ReadsOnlineSchemaAndWritesToStore()
    {
        // Arrange
        var schema = DatabaseSchema.Create([SchemaDefinition.Create("app")]);
        var store = Substitute.For<ISchemaStateStore>();
        var sut = CreateSut(WithOnline(schema), store);

        // Act
        var result = await sut.Capture();

        // Assert
        result.ShouldBeTrue();
        await store.Received(1).Write(schema, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_ScopesTheReadBySchemaNames()
    {
        // Arrange
        var source = Substitute.For<ISchemaProvider>();
        source.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(DatabaseSchema.Create([]));
        var provider = Substitute.For<ICurrentSchemaProvider>();
        provider.GetSource(SchemaSourceMode.Online, required: true).Returns(source);
        var store = Substitute.For<ISchemaStateStore>();
        var sut = CreateSut(provider, store, new MigrationOptions { SchemaNames = ["app"] });

        // Act
        await sut.Capture();

        // Assert
        await source.Received(1).GetSchema(
            Arg.Is<string[]?>(names => names != null && names.SequenceEqual(new[] { "app" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Resolves_WhenNoStoreOrOnlineProviderRegistered()
    {
        // The store is optional: an app with no state store must still resolve the capturer.
        // ICurrentSchemaProvider is always registered by the framework even with no sources configured.
        var builder = NSchemaApplication.CreateBuilder();
        using var app = builder.Build();

        var capturer = app.Services.GetRequiredService<IStateCapturer>();

        capturer.ShouldBeOfType<DefaultStateCapturer>();
    }
}
