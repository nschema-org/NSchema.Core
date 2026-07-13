using NSchema.Project.Ddl;

namespace NSchema.Tests.Schema.Serialization.Ddl;

/// <summary>
/// The read seam's contract: parsing outcomes are a <see cref="Result{T}"/>, never a thrown exception.
/// The grammar itself is covered by the <c>DdlParser*Tests</c> against the parser directly.
/// </summary>
public sealed class DdlReaderTests
{
    [Fact]
    public void Read_ValidDocument_Succeeds()
    {
        // Act
        var result = DdlReader.Instance.Read("CREATE SCHEMA app;");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Schema.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public void Read_SyntaxError_FailsWithAPositionedDiagnostic()
    {
        // Act
        var result = DdlReader.Instance.Read("CREATE SCHEMA app");

        // Assert — the position rides in the message; there is no structured position on the diagnostic (yet).
        result.IsFailure.ShouldBeTrue();
        var error = result.Errors.ShouldHaveSingleItem();
        error.Source.ShouldBe("syntax");
        error.Message.ShouldContain("line 1");
    }

}
