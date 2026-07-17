using NSchema.Model;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// The address contract: always fully qualified, with component-wise identifier equality.
/// </summary>
public class ObjectAddressTests
{
    [Fact]
    public void Equals_CaseVariantComponents_AreTheSameAddress()
    {
        // Arrange
        var lower = new ObjectAddress(new SqlIdentifier("app"), new SqlIdentifier("users"));
        var mixed = new ObjectAddress(new SqlIdentifier("App"), new SqlIdentifier("USERS"));

        // Assert
        lower.ShouldBe(mixed);
        lower.GetHashCode().ShouldBe(mixed.GetHashCode());
    }

    [Fact]
    public void ToString_RendersAsWritten()
    {
        // Assert
        new ObjectAddress(new SqlIdentifier("App"), new SqlIdentifier("Users")).ToString().ShouldBe("App.Users");
    }
}
