using NSchema.Project.Domain.Models;

namespace NSchema.Tests.Schema.Model;

/// <summary>
/// The address contract: always fully qualified, with component-wise identifier equality.
/// </summary>
public class ObjectReferenceTests
{
    [Fact]
    public void Equals_CaseVariantComponents_AreTheSameAddress()
    {
        // Arrange
        var lower = new ObjectReference(new SqlIdentifier("app"), new SqlIdentifier("users"));
        var mixed = new ObjectReference(new SqlIdentifier("App"), new SqlIdentifier("USERS"));

        // Assert
        lower.ShouldBe(mixed);
        lower.GetHashCode().ShouldBe(mixed.GetHashCode());
    }

    [Fact]
    public void ToString_RendersAsWritten()
    {
        // Assert
        new ObjectReference(new SqlIdentifier("App"), new SqlIdentifier("Users")).ToString().ShouldBe("App.Users");
    }
}
