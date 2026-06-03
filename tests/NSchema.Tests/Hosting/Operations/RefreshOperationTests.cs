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
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(DatabaseSchema.Create([]));
        return new RefreshOperation(_reporter, _currentProvider, store);
    }

    [Fact]
    public async Task Execute_WritesLiveSchemaToStore()
    {
        var schema = DatabaseSchema.Create([SchemaDefinition.Create("app")]);
        var store = Substitute.For<ISchemaStateStore>();
        var sut = BuildSut(store);
        _currentProvider
            .GetSchema(SchemaSourceMode.Online, Arg.Any<string[]?>(), required: true, Arg.Any<CancellationToken>())
            .Returns(schema);

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
    public async Task Execute_DoesNotScopeReadBySchemaNames()
    {
        var store = Substitute.For<ISchemaStateStore>();
        var sut = BuildSut(store);

        await sut.Execute();

        await _currentProvider.Received(1).GetSchema(
            SchemaSourceMode.Online,
            Arg.Is<string[]?>(names => names == null),
            required: true,
            Arg.Any<CancellationToken>());
    }
}
