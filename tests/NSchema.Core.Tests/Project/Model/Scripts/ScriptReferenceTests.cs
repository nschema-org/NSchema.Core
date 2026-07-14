using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Tests.Schema.Model.Scripts;

/// <summary>
/// The script-address contract: the container is genuinely optional (null = global), with component-wise
/// identifier equality.
/// </summary>
public class ScriptReferenceTests
{
    [Fact]
    public void Equals_CaseVariantComponents_AreTheSameAddress()
    {
        // Arrange
        var lower = new ScriptReference(new SqlIdentifier("sales"), new SqlIdentifier("seed"));
        var mixed = new ScriptReference(new SqlIdentifier("Sales"), new SqlIdentifier("SEED"));

        // Assert
        lower.ShouldBe(mixed);
        lower.GetHashCode().ShouldBe(mixed.GetHashCode());
    }

    [Fact]
    public void Equals_SameNameInDifferentScopes_AreDistinctScripts()
    {
        // Arrange
        var sales = new ScriptReference(new SqlIdentifier("sales"), new SqlIdentifier("seed"));
        var billing = new ScriptReference(new SqlIdentifier("billing"), new SqlIdentifier("seed"));
        var global = new ScriptReference(null, new SqlIdentifier("seed"));

        // Assert
        sales.ShouldNotBe(billing);
        sales.ShouldNotBe(global);
    }

    [Fact]
    public void ToString_Scoped_RendersLikeAnyOtherReference()
        => new ScriptReference(new SqlIdentifier("Sales"), new SqlIdentifier("seed")).ToString().ShouldBe("Sales.seed");

    [Fact]
    public void ToString_Global_RendersTheBareName()
        => new ScriptReference(null, new SqlIdentifier("seed")).ToString().ShouldBe("seed");
}
