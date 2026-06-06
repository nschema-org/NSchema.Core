using NSchema.Schema.Fluent;
using NSchema.Schema.Model;

namespace NSchema.Tests.Schema.Fluent;

public sealed class ForeignKeyBuilderTests
{
    private readonly ForeignKeyBuilder _sut = new(
        "fk_posts_user",
        ["user_id"],
        "public",
        "users",
        ["id"]
    );

    [Fact]
    public void Build_ByDefault_PopulatesConstructorValuesAndNoActionRules()
    {
        // Arrange

        // Act
        var fk = _sut.Build();

        // Assert
        fk.Name.ShouldBe("fk_posts_user");
        fk.ColumnNames.ShouldBe(["user_id"]);
        fk.ReferencedSchema.ShouldBe("public");
        fk.ReferencedTable.ShouldBe("users");
        fk.ReferencedColumnNames.ShouldBe(["id"]);
        fk.OnDelete.ShouldBe(ReferentialAction.NoAction);
        fk.OnUpdate.ShouldBe(ReferentialAction.NoAction);
    }

    [Fact]
    public void OnDelete_SetsDeleteActionOnBuiltForeignKey()
    {
        // Arrange

        // Act
        _sut.OnDelete(ReferentialAction.Cascade);

        // Assert
        _sut.Build().OnDelete.ShouldBe(ReferentialAction.Cascade);
    }

    [Fact]
    public void OnUpdate_SetsUpdateActionOnBuiltForeignKey()
    {
        // Arrange

        // Act
        _sut.OnUpdate(ReferentialAction.SetNull);

        // Assert
        _sut.Build().OnUpdate.ShouldBe(ReferentialAction.SetNull);
    }

    [Fact]
    public void FluentMethods_AreChainable()
    {
        // Arrange

        // Act
        var result = _sut
            .OnDelete(ReferentialAction.Cascade)
            .OnUpdate(ReferentialAction.SetNull);

        // Assert
        result.ShouldBeSameAs(_sut);
    }
}
