using NSchema.Model;

namespace NSchema.Tests.Project.Model.Scripts;

/// <summary>
/// The script-address contract: the container is genuinely optional (null = global), with component-wise
/// identifier equality.
/// </summary>
public class ScopedAddressTests
{
    [Fact]
    public void Equals_CaseVariantComponents_AreTheSameAddress()
    {
        // Arrange
        var lower = new ScopedAddress(new SqlIdentifier("sales"), new SqlIdentifier("seed"));
        var mixed = new ScopedAddress(new SqlIdentifier("Sales"), new SqlIdentifier("SEED"));

        // Assert
        lower.ShouldBe(mixed);
        lower.GetHashCode().ShouldBe(mixed.GetHashCode());
    }

    [Fact]
    public void Equals_SameNameInDifferentScopes_AreDistinctScripts()
    {
        // Arrange
        var sales = new ScopedAddress(new SqlIdentifier("sales"), new SqlIdentifier("seed"));
        var billing = new ScopedAddress(new SqlIdentifier("billing"), new SqlIdentifier("seed"));
        var global = new ScopedAddress(null, new SqlIdentifier("seed"));

        // Assert
        sales.ShouldNotBe(billing);
        sales.ShouldNotBe(global);
    }

    [Fact]
    public void ToString_Scoped_RendersLikeAnyOtherReference()
        => new ScopedAddress(new SqlIdentifier("Sales"), new SqlIdentifier("seed")).ToString().ShouldBe("Sales.seed");

    [Fact]
    public void ToString_Global_RendersTheBareName()
        => new ScopedAddress(null, new SqlIdentifier("seed")).ToString().ShouldBe("seed");
}
