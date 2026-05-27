using NSchema.Schema;
using NSchema.Schema.Fluent;

namespace NSchema.Tests.Schema.Fluent;

public sealed class ColumnBuilderTests
{
    private readonly TableBuilder _table = new("users");
    private readonly ColumnBuilder _sut;

    public ColumnBuilderTests()
    {
        _sut = new ColumnBuilder(_table, "email", SqlType.Text);
    }

    [Fact]
    public void Build_ByDefault_ProducesColumnWithNameAndType()
    {
        // Arrange

        // Act
        var column = _sut.Build();

        // Assert
        column.Name.ShouldBe("email");
        column.Type.ShouldBe(SqlType.Text);
        column.IsNullable.ShouldBeTrue();
        column.IsIdentity.ShouldBeFalse();
        column.DefaultExpression.ShouldBeNull();
        column.OldName.ShouldBeNull();
        column.Comment.ShouldBeNull();
        column.IdentityOptions.ShouldBeNull();
    }

    [Fact]
    public void NotNull_SetsIsNullableFalse()
    {
        // Arrange

        // Act
        _sut.NotNull();

        // Assert
        _sut.Build().IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Nullable_AfterNotNull_SetsIsNullableTrue()
    {
        // Arrange
        _sut.NotNull();

        // Act
        _sut.Nullable();

        // Assert
        _sut.Build().IsNullable.ShouldBeTrue();
    }

    [Fact]
    public void Identity_WithDefaults_SetsIsIdentityWithDefaultOptions()
    {
        // Arrange

        // Act
        _sut.Identity();

        // Assert
        var column = _sut.Build();
        column.IsIdentity.ShouldBeTrue();
        column.IdentityOptions.ShouldBe(new IdentityOptions(1, 1, 1));
    }

    [Fact]
    public void Identity_WithExplicitValues_PopulatesIdentityOptions()
    {
        // Arrange

        // Act
        _sut.Identity(startWith: 100, minValue: 50, incrementBy: 5);

        // Assert
        _sut.Build().IdentityOptions.ShouldBe(new IdentityOptions(100, 50, 5));
    }

    [Fact]
    public void Default_SetsDefaultExpression()
    {
        // Arrange

        // Act
        _sut.Default("now()");

        // Assert
        _sut.Build().DefaultExpression.ShouldBe("now()");
    }

    [Fact]
    public void Comment_SetsCommentOnBuiltColumn()
    {
        // Arrange

        // Act
        _sut.Comment("Email address");

        // Assert
        _sut.Build().Comment.ShouldBe("Email address");
    }

    [Fact]
    public void RenamedFrom_SetsOldNameOnBuiltColumn()
    {
        // Arrange

        // Act
        _sut.RenamedFrom("email_address");

        // Assert
        _sut.Build().OldName.ShouldBe("email_address");
    }

    [Fact]
    public void PrimaryKey_RegistersPrimaryKeyOnParentTable()
    {
        // Arrange

        // Act
        _sut.PrimaryKey("pk_users");

        // Assert
        var pk = _table.Build().PrimaryKey;
        pk.ShouldNotBeNull();
        pk!.Name.ShouldBe("pk_users");
        pk.ColumnNames.ShouldBe(["email"]);
    }

    [Fact]
    public void PrimaryKey_ImpliesNotNull()
    {
        // Arrange

        // Act
        _sut.PrimaryKey("pk_users");

        // Assert
        _sut.Build().IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void FluentMethods_AreChainable()
    {
        // Arrange

        // Act
        var result = _sut
            .NotNull()
            .Nullable()
            .Default("x")
            .Comment("c")
            .RenamedFrom("old")
            .Identity();

        // Assert
        result.ShouldBeSameAs(_sut);
    }
}
