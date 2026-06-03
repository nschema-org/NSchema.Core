using NSchema.Schema.Model.Fluent;

namespace NSchema.Tests.Schema.Fluent;

public sealed class SchemaBuilderTests
{
    private readonly SchemaBuilder _sut = new("public");

    [Fact]
    public void Build_ByDefault_ProducesSchemaWithName()
    {
        // Arrange

        // Act
        var schema = _sut.Build();

        // Assert
        schema.Name.ShouldBe("public");
        schema.OldName.ShouldBeNull();
        schema.IsPartial.ShouldBeFalse();
        schema.Comment.ShouldBeNull();
        schema.Tables.ShouldBeEmpty();
        schema.DroppedTables.ShouldBeEmpty();
        schema.Grants.ShouldBeEmpty();
    }

    [Fact]
    public void Name_ExposesConstructorName()
    {
        // Arrange

        // Act
        var name = _sut.Name;

        // Assert
        name.ShouldBe("public");
    }

    [Fact]
    public void IsDropped_ByDefault_IsFalse()
    {
        // Arrange

        // Act
        var isDropped = _sut.IsDropped;

        // Assert
        isDropped.ShouldBeFalse();
    }

    [Fact]
    public void Dropped_SetsIsDroppedTrue()
    {
        // Arrange

        // Act
        _sut.Dropped();

        // Assert
        _sut.IsDropped.ShouldBeTrue();
    }

    [Fact]
    public void Table_ReturnsBuilder_AndIncludesItInBuild()
    {
        // Arrange
        var builder = _sut.Table("users");

        // Act
        var schema = _sut.Build();

        // Assert
        builder.ShouldNotBeNull();
        schema.Tables.Select(t => t.Name).ShouldBe(["users"]);
    }

    [Fact]
    public void Table_MultipleTables_AllAppearInBuild()
    {
        // Arrange
        _sut.Table("users");
        _sut.Table("posts");

        // Act
        var schema = _sut.Build();

        // Assert
        schema.Tables.Select(t => t.Name).ShouldBe(["users", "posts"]);
    }

    [Fact]
    public void Table_WithConfigure_InvokesConfigureAndReturnsSchemaBuilder()
    {
        // Arrange
        TableBuilder? captured = null;

        // Act
        var result = _sut.Table("users", t => captured = t);

        // Assert
        result.ShouldBeSameAs(_sut);
        captured.ShouldNotBeNull();
        _sut.Build().Tables.Select(t => t.Name).ShouldBe(["users"]);
    }

    [Fact]
    public void Table_Dropped_AppearsInDroppedTablesNotTables()
    {
        // Arrange
        _sut.Table("legacy").Dropped();
        _sut.Table("users");

        // Act
        var schema = _sut.Build();

        // Assert
        schema.Tables.Select(t => t.Name).ShouldBe(["users"]);
        schema.DroppedTables.ShouldBe(["legacy"]);
    }

    [Fact]
    public void Comment_SetsCommentOnBuiltSchema()
    {
        // Arrange

        // Act
        _sut.Comment("App schema");

        // Assert
        _sut.Build().Comment.ShouldBe("App schema");
    }

    [Fact]
    public void RenamedFrom_SetsOldNameOnBuiltSchema()
    {
        // Arrange

        // Act
        _sut.RenamedFrom("old_schema");

        // Assert
        _sut.Build().OldName.ShouldBe("old_schema");
    }

    [Fact]
    public void Grant_AddsGrantToBuiltSchema()
    {
        // Arrange

        // Act
        _sut.Grant("reporting");

        // Assert
        _sut.Build().Grants.Select(g => g.Role).ShouldBe(["reporting"]);
    }

    [Fact]
    public void Grant_MultipleRoles_AllAppearInBuild()
    {
        // Arrange

        // Act
        _sut.Grant("reporting");
        _sut.Grant("app_user");

        // Assert
        _sut.Build().Grants.Select(g => g.Role).ShouldBe(["reporting", "app_user"]);
    }

    [Fact]
    public void AsPartial_SetsIsPartialTrueOnBuiltSchema()
    {
        // Arrange

        // Act
        _sut.AsPartial();

        // Assert
        _sut.Build().IsPartial.ShouldBeTrue();
    }

    [Fact]
    public void FluentMethods_AreChainable()
    {
        // Arrange

        // Act
        var result = _sut
            .Comment("c")
            .RenamedFrom("old")
            .Grant("role")
            .AsPartial()
            .Dropped();

        // Assert
        result.ShouldBeSameAs(_sut);
    }
}
