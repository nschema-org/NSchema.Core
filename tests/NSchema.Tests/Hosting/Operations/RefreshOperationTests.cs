using Microsoft.Extensions.Options;
using NSchema.Hosting.Operations;
using NSchema.Migration;
using NSchema.Schema;
using NSchema.State;

namespace NSchema.Tests.Hosting.Operations;

public sealed class RefreshOperationTests
{
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();
    private readonly ICurrentSchemaProvider _currentProvider = Substitute.For<ICurrentSchemaProvider>();

    private RefreshOperation BuildSut(ISchemaStateStore? store)
    {
        var source = Substitute.For<ISchemaProvider>();
        source.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(DatabaseSchema.Create([]));
        _currentProvider.GetSource(SchemaSourceMode.Online, required: true).Returns(source);
        return new RefreshOperation(Options.Create(new MigrationOptions()), _reporter, _currentProvider, store);
    }

    [Fact]
    public async Task Execute_WritesLiveSchemaToStore()
    {
        var schema = DatabaseSchema.Create([SchemaDefinition.Create("app")]);
        var source = Substitute.For<ISchemaProvider>();
        source.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(schema);
        _currentProvider.GetSource(SchemaSourceMode.Online, required: true).Returns(source);
        var store = Substitute.For<ISchemaStateStore>();
        var sut = new RefreshOperation(Options.Create(new MigrationOptions()), _reporter, _currentProvider, store);

        await sut.Execute();

        await store.Received(1).Write(schema, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoStateStore_Throws()
    {
        var sut = BuildSut(store: null);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Execute());
    }

    [Fact]
    public async Task Execute_ScopesReadBySchemaNames()
    {
        var source = Substitute.For<ISchemaProvider>();
        source.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(DatabaseSchema.Create([]));
        _currentProvider.GetSource(SchemaSourceMode.Online, required: true).Returns(source);
        var store = Substitute.For<ISchemaStateStore>();
        var sut = new RefreshOperation(
            Options.Create(new MigrationOptions { SchemaNames = ["app"] }),
            _reporter, _currentProvider, store);

        await sut.Execute();

        await source.Received(1).GetSchema(
            Arg.Is<string[]?>(names => names != null && names.SequenceEqual(new[] { "app" })),
            Arg.Any<CancellationToken>());
    }
}
