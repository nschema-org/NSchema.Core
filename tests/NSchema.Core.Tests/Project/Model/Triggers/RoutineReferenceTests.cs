using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Triggers;

namespace NSchema.Tests.Project.Model.Triggers;

/// <summary>
/// The as-written reference contract: component-wise identifier equality, and the distinction between
/// a qualified reference and one left to the engine's search path.
/// </summary>
public class RoutineReferenceTests
{
    [Fact]
    public void Equals_CaseVariantComponents_AreTheSameReference()
    {
        // Arrange
        var lower = new RoutineReference(new SqlIdentifier("app"), new SqlIdentifier("log_change"));
        var mixed = new RoutineReference(new SqlIdentifier("App"), new SqlIdentifier("LOG_CHANGE"));

        // Assert
        lower.ShouldBe(mixed);
        lower.GetHashCode().ShouldBe(mixed.GetHashCode());
    }

    [Fact]
    public void Equals_QualifiedAndUnqualified_AreDistinct()
    {
        // An unqualified reference resolves by search path; it is not the same reference as a qualified one.
        var qualified = new RoutineReference(new SqlIdentifier("app"), new SqlIdentifier("log"));
        var unqualified = new RoutineReference(null, new SqlIdentifier("log"));

        // Assert
        qualified.ShouldNotBe(unqualified);
    }

    [Fact]
    public void ToString_RendersAsWritten()
    {
        // Assert
        new RoutineReference(new SqlIdentifier("App"), new SqlIdentifier("Log")).ToString().ShouldBe("App.Log");
        new RoutineReference(null, new SqlIdentifier("log")).ToString().ShouldBe("log");
    }

    [Fact]
    public void With_ResolvingAnUnqualifiedReference_SetsTheSchemaPart()
    {
        // The template applicator resolves a template routine into the applied schema by setting the
        // schema part — never by string concatenation.
        var unqualified = new RoutineReference(null, new SqlIdentifier("log"));

        // Act
        var qualified = unqualified with { Schema = new SqlIdentifier("app") };

        // Assert
        qualified.ShouldBe(new RoutineReference(new SqlIdentifier("app"), new SqlIdentifier("log")));
    }
}
