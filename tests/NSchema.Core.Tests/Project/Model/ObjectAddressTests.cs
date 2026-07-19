using NSchema.Model;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// The address contract: always fully qualified, with component-wise identifier equality.
/// </summary>
public class ObjectAddressTests
{
    [Fact]
    public void Equals_CaseVariantComponents_AreDifferentAddresses()
    {
        // Arrange
        var lower = new ObjectAddress(new SqlIdentifier("app"), new SqlIdentifier("users"));
        var mixed = new ObjectAddress(new SqlIdentifier("App"), new SqlIdentifier("USERS"));

        // Assert
        lower.ShouldNotBe(mixed);
        lower.ShouldBe(new ObjectAddress(new SqlIdentifier("app"), new SqlIdentifier("users")));
    }

    [Fact]
    public void ToString_RendersAsWritten()
    {
        // Assert
        new ObjectAddress(new SqlIdentifier("App"), new SqlIdentifier("Users")).ToString().ShouldBe("App.Users");
    }
}
