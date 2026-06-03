using NSchema.Migration;
using NSchema.Migration.Sources;
using NSchema.Schema;

namespace NSchema.Tests.Migration;

public sealed class DefaultDesiredSchemaProviderTests
{
    private readonly ISchemaAggregator _aggregator = Substitute.For<ISchemaAggregator>();

    [Fact]
    public async Task GetSchema_NoProviders_ThrowsWithHelpfulMessage()
    {
        var sut = new DefaultDesiredSchemaProvider([], _aggregator);

        var act = () => sut.GetSchema();

        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldContain("AddSchema");
    }

    [Fact]
    public async Task GetSchema_AggregatesAllProviders()
    {
        var s1 = DatabaseSchema.Create([SchemaDefinition.Create("a")]);
        var s2 = DatabaseSchema.Create([SchemaDefinition.Create("b")]);
        var merged = DatabaseSchema.Create([SchemaDefinition.Create("a"), SchemaDefinition.Create("b")]);
        var p1 = Substitute.For<ISchemaProvider>();
        var p2 = Substitute.For<ISchemaProvider>();
        p1.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(s1);
        p2.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(s2);
        _aggregator.Aggregate(Arg.Any<IReadOnlyList<DatabaseSchema>>()).Returns(merged);
        var sut = new DefaultDesiredSchemaProvider([p1, p2], _aggregator);

        var result = await sut.GetSchema(null, TestContext.Current.CancellationToken);

        result.ShouldBe(merged);
        _aggregator.Received(1).Aggregate(Arg.Is<IReadOnlyList<DatabaseSchema>>(l => l.Count == 2));
    }

    [Fact]
    public async Task GetSchema_PassesScopeToAllProviders()
    {
        var scope = new[] { "app", "admin" };
        string[]? captured1 = null;
        string[]? captured2 = null;
        var p1 = Substitute.For<ISchemaProvider>();
        var p2 = Substitute.For<ISchemaProvider>();
        p1.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { captured1 = call.Arg<string[]?>(); return Task.FromResult(DatabaseSchema.Create([])); });
        p2.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { captured2 = call.Arg<string[]?>(); return Task.FromResult(DatabaseSchema.Create([])); });
        _aggregator.Aggregate(Arg.Any<IReadOnlyList<DatabaseSchema>>()).Returns(DatabaseSchema.Create([]));
        var sut = new DefaultDesiredSchemaProvider([p1, p2], _aggregator);

        await sut.GetSchema(scope, TestContext.Current.CancellationToken);

        captured1.ShouldBe(scope);
        captured2.ShouldBe(scope);
    }
}
