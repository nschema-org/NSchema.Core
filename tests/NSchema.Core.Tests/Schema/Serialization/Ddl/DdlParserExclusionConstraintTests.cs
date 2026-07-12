using NSchema.Project.Ddl;
using NSchema.Project.Domain.Models.Constraints;

namespace NSchema.Tests.Schema.Serialization.Ddl;

/// <summary>
/// Parser coverage for exclusion constraints:
/// <c>CONSTRAINT n EXCLUDE [USING method] (element WITH operator, …) [WHERE (predicate)]</c>.
/// </summary>
public sealed class DdlParserExclusionConstraintTests
{
    private static ExclusionConstraint ParseExclusion(string constraint) =>
        DdlReader.Instance.Read($"CREATE TABLE app.bookings (room int, during int, {constraint});").Schema
            .Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().ExclusionConstraints.ShouldHaveSingleItem();

    [Fact]
    public void Parse_SimpleExclusion_CapturesElementsAndMethod()
    {
        var exclusion = ParseExclusion("CONSTRAINT no_overlap EXCLUDE USING gist (room WITH =, during WITH &&)");
        exclusion.Name.ShouldBe("no_overlap");
        exclusion.Method.ShouldBe("gist");
        exclusion.Elements.Count.ShouldBe(2);
        exclusion.Elements[0].Expression.ShouldBe("room");
        exclusion.Elements[0].Operator.ShouldBe("=");
        exclusion.Elements[1].Expression.ShouldBe("during");
        exclusion.Elements[1].Operator.ShouldBe("&&");
    }

    [Fact]
    public void Parse_NoMethod_IsNull()
        => ParseExclusion("CONSTRAINT x EXCLUDE (room WITH =)").Method.ShouldBeNull();

    [Fact]
    public void Parse_Predicate_IsCaptured()
        => ParseExclusion("CONSTRAINT x EXCLUDE USING gist (during WITH &&) WHERE (room > 0)")
            .Predicate.ShouldBe("room > 0");

    [Fact]
    public void Parse_ExpressionElement_IsCaptured()
    {
        var element = ParseExclusion("CONSTRAINT x EXCLUDE USING gist ((int4range(room, during)) WITH &&)")
            .Elements.ShouldHaveSingleItem();
        element.IsExpression.ShouldBeTrue();
        element.Expression.ShouldBe("int4range(room, during)");
        element.Operator.ShouldBe("&&");
    }

    [Fact]
    public void Parse_WithDocComment_AttachesComment()
        => ParseExclusion("--- no double-booking\n  CONSTRAINT no_overlap EXCLUDE USING gist (during WITH &&)")
            .Comment.ShouldBe("no double-booking");

    [Fact]
    public void Parse_Exclusion_RoundTripsThroughWriter()
    {
        var schema = DdlReader.Instance.Read(
            "CREATE TABLE app.bookings (room int, during int, " +
            "CONSTRAINT no_overlap EXCLUDE USING gist (room WITH =, during WITH &&) WHERE (room > 0));").Schema;

        var exclusion = DdlReader.Instance.Read(DdlWriter.Instance.Write(schema)).Schema
            .Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().ExclusionConstraints.ShouldHaveSingleItem();
        exclusion.Name.ShouldBe("no_overlap");
        exclusion.Method.ShouldBe("gist");
        exclusion.Predicate.ShouldBe("room > 0");
        exclusion.Elements.Select(e => (e.Expression, e.Operator)).ShouldBe([("room", "="), ("during", "&&")]);
    }
}
