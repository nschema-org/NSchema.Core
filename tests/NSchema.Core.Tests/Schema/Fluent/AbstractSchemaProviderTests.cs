using NSchema.Schema.Fluent;

namespace NSchema.Tests.Schema.Fluent;

public sealed class AbstractSchemaProviderTests
{
    private sealed class TestSchemaProvider : AbstractSchemaProvider;

    private readonly TestSchemaProvider _sut = new();

    [Fact]
    public async Task GetSchema_WithNoSchemas_ReturnsEmptySchemaList()
    {
        // Arrange

        // Act
        var model = await _sut.GetSchema(null, TestContext.Current.CancellationToken);

        // Assert
        model.Schemas.ShouldBeEmpty();
        model.DroppedSchemas.ShouldBeEmpty();
    }

    [Fact]
    public async Task Schema_ReturnsBuilder_ThatIsIncludedInModel()
    {
        // Arrange
        var builder = _sut.Schema("public");

        // Act
        var model = await _sut.GetSchema(null, TestContext.Current.CancellationToken);

        // Assert
        builder.ShouldNotBeNull();
        model.Schemas.Select(s => s.Name).ShouldBe(["public"]);
    }

    [Fact]
    public async Task Schema_WithConfigureDelegate_InvokesConfigure()
    {
        // Arrange
        var invoked = false;
        _sut.Schema("public", _ => invoked = true);

        // Act
        await _sut.GetSchema(null, TestContext.Current.CancellationToken);

        // Assert
        invoked.ShouldBeTrue();
    }

    [Fact]
    public void Schema_WithConfigureDelegate_ReturnsProviderForChaining()
    {
        // Arrange

        // Act
        var result = _sut.Schema("public", _ => { });

        // Assert
        result.ShouldBeSameAs(_sut);
    }

    [Fact]
    public async Task GetSchema_MultipleSchemas_AllAppearInModel()
    {
        // Arrange
        _sut.Schema("public");
        _sut.Schema("admin");

        // Act
        var model = await _sut.GetSchema(null, TestContext.Current.CancellationToken);

        // Assert
        model.Schemas.Select(s => s.Name).ShouldBe(["public", "admin"]);
    }

    [Fact]
    public async Task GetSchema_WithScope_FiltersToNamedSchemasOnly()
    {
        // Arrange
        _sut.Schema("public");
        _sut.Schema("admin");
        _sut.Schema("internal");

        // Act
        var model = await _sut.GetSchema(["public", "internal"], TestContext.Current.CancellationToken);

        // Assert
        model.Schemas.Select(s => s.Name).ShouldBe(["public", "internal"]);
    }

    [Fact]
    public async Task GetSchema_WithScope_IsCaseInsensitive()
    {
        // Arrange
        _sut.Schema("Public");

        // Act
        var model = await _sut.GetSchema(["public"], TestContext.Current.CancellationToken);

        // Assert
        model.Schemas.Select(s => s.Name).ShouldBe(["Public"]);
    }

    [Fact]
    public async Task GetSchema_WithEmptyScope_ReturnsAllSchemas()
    {
        // Arrange
        _sut.Schema("public");
        _sut.Schema("admin");

        // Act
        var model = await _sut.GetSchema([], TestContext.Current.CancellationToken);

        // Assert
        model.Schemas.Select(s => s.Name).ShouldBe(["public", "admin"]);
    }

    [Fact]
    public async Task GetSchema_DroppedSchema_AppearsInDroppedListOnly()
    {
        // Arrange
        _sut.Schema("legacy").Dropped();
        _sut.Schema("public");

        // Act
        var model = await _sut.GetSchema(null, TestContext.Current.CancellationToken);

        // Assert
        model.Schemas.Select(s => s.Name).ShouldBe(["public"]);
        model.DroppedSchemas.ShouldBe(["legacy"]);
    }
}
