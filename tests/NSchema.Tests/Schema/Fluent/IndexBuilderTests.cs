using NSchema.Schema.Model.Fluent;

namespace NSchema.Tests.Schema.Fluent;

public sealed class IndexBuilderTests
{
    private readonly IndexBuilder _sut = new("ix_users_email", ["email"]);

    [Fact]
    public void Build_ByDefault_PopulatesConstructorValuesAndIsNotUnique()
    {
        // Arrange

        // Act
        var index = _sut.Build();

        // Assert
        index.Name.ShouldBe("ix_users_email");
        index.ColumnNames.ShouldBe(["email"]);
        index.IsUnique.ShouldBeFalse();
        index.Comment.ShouldBeNull();
        index.Predicate.ShouldBeNull();
    }

    [Fact]
    public void Unique_SetsIsUniqueTrue()
    {
        // Arrange

        // Act
        _sut.Unique();

        // Assert
        _sut.Build().IsUnique.ShouldBeTrue();
    }

    [Fact]
    public void Comment_SetsCommentOnBuiltIndex()
    {
        // Arrange

        // Act
        _sut.Comment("Email lookup");

        // Assert
        _sut.Build().Comment.ShouldBe("Email lookup");
    }

    [Fact]
    public void Where_SetsPredicateOnBuiltIndex()
    {
        // Arrange

        // Act
        _sut.Where("deleted_at IS NULL");

        // Assert
        _sut.Build().Predicate.ShouldBe("deleted_at IS NULL");
    }

    [Fact]
    public void CompositeIndex_PreservesColumnOrder()
    {
        // Arrange
        var sut = new IndexBuilder("ix_composite", ["last_name", "first_name"]);

        // Act
        var index = sut.Build();

        // Assert
        index.ColumnNames.ShouldBe(["last_name", "first_name"]);
    }

    [Fact]
    public void FluentMethods_AreChainable()
    {
        // Arrange

        // Act
        var result = _sut
            .Unique()
            .Comment("c")
            .Where("x IS NULL");

        // Assert
        result.ShouldBeSameAs(_sut);
    }
}
