using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.Tests.Schema;

public sealed class DesiredSchemaProviderTests
{
    [Fact]
    public async Task GetSchema_NoProviders_Throws()
    {
        var sut = new DesiredSchemaProvider([], []);

        var act = () => sut.GetSchema().AsTask();

        await Should.ThrowAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task GetSchema_AggregatesAllProviders()
    {
        var s1 = new DatabaseSchema([new SchemaDefinition("a")]);
        var s2 = new DatabaseSchema([new SchemaDefinition("b")]);
        var merged = new DatabaseSchema([new SchemaDefinition("a"), new SchemaDefinition("b")]);
        var p1 = Substitute.For<ISchemaProvider>();
        var p2 = Substitute.For<ISchemaProvider>();
        p1.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(s1);
        p2.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(s2);
        var sut = new DesiredSchemaProvider([p1, p2], []);

        var result = await sut.GetSchema(null, TestContext.Current.CancellationToken);

        result.Schemas.Count.ShouldBe(2);
        result.Schemas.Select(s => s.Name).ShouldBe(["a", "b"]);
    }

    [Fact]
    public async Task GetSchema_AppliesTransformersAfterAggregation()
    {

        var transformed = new DatabaseSchema([new SchemaDefinition("transformed")]);
        var provider = Substitute.For<ISchemaProvider>();
        provider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(new DatabaseSchema([]));
        var transformer = Substitute.For<ISchemaTransformer>();
        transformer.Transform(Arg.Any<DatabaseSchema>()).Returns(transformed);
        var sut = new DesiredSchemaProvider([provider], [transformer]);

        var result = await sut.GetSchema(null, TestContext.Current.CancellationToken);

        result.ShouldBe(transformed);
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
            .Returns(call => { captured1 = call.Arg<string[]?>(); return new DatabaseSchema([]); });
        p2.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { captured2 = call.Arg<string[]?>(); return new DatabaseSchema([]); });
        var sut = new DesiredSchemaProvider([p1, p2], []);

        await sut.GetSchema(scope, TestContext.Current.CancellationToken);

        captured1.ShouldBe(scope);
        captured2.ShouldBe(scope);
    }
}
