using NSchema.Schema;
using NSchema.Schema.Fluent;

namespace NSchema.Tests.Schema.Fluent;

public sealed class TableBuilderTests
{
    private readonly TableBuilder _sut = new("users");

    [Fact]
    public void Build_ByDefault_ProducesTableWithName()
    {
        // Arrange

        // Act
        var table = _sut.Build();

        // Assert
        table.Name.ShouldBe("users");
        table.OldName.ShouldBeNull();
        table.PrimaryKey.ShouldBeNull();
        table.Comment.ShouldBeNull();
        table.Columns.ShouldBeEmpty();
        table.ForeignKeys.ShouldBeEmpty();
        table.Indexes.ShouldBeEmpty();
        table.Grants.ShouldBeEmpty();
    }

    [Fact]
    public void Name_ExposesConstructorName()
    {
        // Arrange

        // Act
        var name = _sut.Name;

        // Assert
        name.ShouldBe("users");
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
    public void Column_ReturnsBuilder_AndIncludesItInBuild()
    {
        // Arrange
        var builder = _sut.Column("id", SqlType.BigInt);

        // Act
        var table = _sut.Build();

        // Assert
        builder.ShouldNotBeNull();
        table.Columns.Select(c => (c.Name, c.Type)).ShouldBe([("id", SqlType.BigInt)]);
    }

    [Fact]
    public void Column_WithConfigure_InvokesConfigureAndReturnsTableBuilder()
    {
        // Arrange
        ColumnBuilder? captured = null;

        // Act
        var result = _sut.Column("id", SqlType.BigInt, c => captured = c);

        // Assert
        result.ShouldBeSameAs(_sut);
        captured.ShouldNotBeNull();
    }

    [Fact]
    public void PrimaryKey_SetsPrimaryKeyOnBuiltTable()
    {
        // Arrange

        // Act
        _sut.PrimaryKey("pk_users", ["id"]);

        // Assert
        var pk = _sut.Build().PrimaryKey;
        pk.ShouldNotBeNull();
        pk!.Name.ShouldBe("pk_users");
        pk.ColumnNames.ShouldBe(["id"]);
    }

    [Fact]
    public void ForeignKey_ReturnsBuilder_AndIncludesItInBuild()
    {
        // Arrange
        var builder = _sut.ForeignKey("fk", ["user_id"], "public", "users", ["id"]);

        // Act
        var table = _sut.Build();

        // Assert
        builder.ShouldNotBeNull();
        table.ForeignKeys.Select(f => f.Name).ShouldBe(["fk"]);
    }

    [Fact]
    public void ForeignKey_WithConfigure_InvokesConfigureAndReturnsTableBuilder()
    {
        // Arrange
        ForeignKeyBuilder? captured = null;

        // Act
        var result = _sut.ForeignKey("fk", ["user_id"], "public", "users", ["id"], f => captured = f);

        // Assert
        result.ShouldBeSameAs(_sut);
        captured.ShouldNotBeNull();
        _sut.Build().ForeignKeys.Select(f => f.Name).ShouldBe(["fk"]);
    }

    [Fact]
    public void Index_ReturnsBuilder_AndIncludesItInBuild()
    {
        // Arrange
        var builder = _sut.Index("ix_email", ["email"]);

        // Act
        var table = _sut.Build();

        // Assert
        builder.ShouldNotBeNull();
        table.Indexes.Select(i => i.Name).ShouldBe(["ix_email"]);
    }

    [Fact]
    public void Index_WithConfigure_InvokesConfigureAndReturnsTableBuilder()
    {
        // Arrange
        IndexBuilder? captured = null;

        // Act
        var result = _sut.Index("ix_email", ["email"], i => captured = i);

        // Assert
        result.ShouldBeSameAs(_sut);
        captured.ShouldNotBeNull();
        _sut.Build().Indexes.Select(i => i.Name).ShouldBe(["ix_email"]);
    }

    [Fact]
    public void Comment_SetsCommentOnBuiltTable()
    {
        // Arrange

        // Act
        _sut.Comment("User accounts");

        // Assert
        _sut.Build().Comment.ShouldBe("User accounts");
    }

    [Fact]
    public void RenamedFrom_SetsOldNameOnBuiltTable()
    {
        // Arrange

        // Act
        _sut.RenamedFrom("members");

        // Assert
        _sut.Build().OldName.ShouldBe("members");
    }

    [Fact]
    public void Grant_AddsGrantToBuiltTable()
    {
        // Arrange

        // Act
        _sut.Grant("app_user", TablePrivilege.Select);

        // Assert
        var grants = _sut.Build().Grants;
        grants.Select(g => (g.Role, g.Privileges)).ShouldBe([("app_user", TablePrivilege.Select)]);
    }

    [Fact]
    public void FluentMethods_AreChainable()
    {
        // Arrange

        // Act
        var result = _sut
            .Comment("c")
            .RenamedFrom("old")
            .PrimaryKey("pk", ["id"])
            .Grant("role", TablePrivilege.All)
            .Dropped();

        // Assert
        result.ShouldBeSameAs(_sut);
    }
}
