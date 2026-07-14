using NSchema.Project.Nsql;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.CompositeTypes;

namespace NSchema.Tests.Schema.Serialization.Ddl;

/// <summary>
/// Parser coverage for <c>CREATE TYPE s.t AS (field &lt;type&gt;, …)</c>.
/// </summary>
public sealed class DdlParserCompositeTypeTests
{
    private static CompositeType ParseType(string sql) =>
        new TestDdlParser("CREATE SCHEMA app; " + sql).Parse().Schema
            .Schemas.ShouldHaveSingleItem().CompositeTypes.ShouldHaveSingleItem();

    [Fact]
    public void Parse_SimpleType_CapturesNameAndFields()
    {
        var type = ParseType("CREATE TYPE app.address AS (street text, zip int);");
        ShouldlyIdentifierExtensions.ShouldBe(type.Name, "address");
        type.Fields.Count.ShouldBe(2);
        ShouldlyIdentifierExtensions.ShouldBe(type.Fields[0].Name, "street");
        type.Fields[0].DataType.ShouldBe(SqlType.Text);
        ShouldlyIdentifierExtensions.ShouldBe(type.Fields[1].Name, "zip");
        type.Fields[1].DataType.ShouldBe(SqlType.Int);
    }

    [Fact]
    public void Parse_SingleField_IsCaptured()
        => ShouldlyIdentifierExtensions.ShouldBe(ParseType("CREATE TYPE app.point AS (x int);").Fields.ShouldHaveSingleItem().Name, "x");

    [Fact]
    public void Parse_EmptyFieldList_IsCaptured()
        => ParseType("CREATE TYPE app.empty AS ();").Fields.ShouldBeEmpty();

    [Fact]
    public void Parse_QualifiedFieldType_IsCaptured()
        => ParseType("CREATE TYPE app.t AS (id app.typeid);").Fields.ShouldHaveSingleItem()
            .DataType.Name.ShouldBe("app.typeid");

    [Fact]
    public void Parse_RenamedFrom_SetsOldName()
        => ShouldlyIdentifierExtensions.ShouldBe(ParseType("CREATE TYPE app.address RENAMED FROM legacy_address AS (street text);").OldName, "legacy_address");

    [Fact]
    public void Parse_WithDocComment_AttachesComment()
        => ParseType("--- a postal address\nCREATE TYPE app.address AS (street text);").Comment.ShouldBe("a postal address");

    [Fact]
    public void Parse_DropType_RecordsDroppedCompositeType()
        => ShouldlyIdentifierExtensions.ShouldBe(new TestDdlParser("CREATE SCHEMA app; DROP TYPE app.address;").Parse().Schema
                .Schemas.ShouldHaveSingleItem().DroppedCompositeTypes.ShouldHaveSingleItem(), "address");

    [Fact]
    public void Parse_DuplicateType_FailsTheRead()
        => new TestDdlParser("CREATE SCHEMA app; CREATE TYPE app.t AS (a int); CREATE TYPE app.t AS (b int);").Project().Errors.ShouldHaveSingleItem()
            .Message.ShouldContain("already declared");

    [Fact]
    public void Parse_PartialType_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestDdlParser("CREATE PARTIAL TYPE app.t AS (a int);").Parse())
            .Message.ShouldContain("PARTIAL applies to SCHEMA");
}
