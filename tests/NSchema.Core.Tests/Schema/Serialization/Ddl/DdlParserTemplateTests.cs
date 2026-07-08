using NSchema.Schema.Ddl;
using NSchema.Schema.Model.Templates;

namespace NSchema.Tests.Schema.Serialization.Ddl;

public sealed class DdlParserTemplateTests
{
    private static TemplateDefinition ReadTemplate(string source) =>
        DdlReader.Instance.Read(source).Templates.ShouldHaveSingleItem();

    [Fact]
    public void Parse_Template_CapturesNameAndObjects()
    {
        var template = ReadTemplate(
            """
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox (
                id uuid NOT NULL,
                payload text NOT NULL,
                CONSTRAINT pk_outbox PRIMARY KEY (id)
              );
              CREATE SEQUENCE outbox_seq;
            END;
            """);

        template.Name.ShouldBe("outbox");
        template.Objects.Name.ShouldBe(TemplateDefinition.TargetSchemaPlaceholder);
        template.Objects.Tables.ShouldHaveSingleItem().Name.ShouldBe("outbox");
        template.Objects.Sequences.ShouldHaveSingleItem().Name.ShouldBe("outbox_seq");
    }

    [Fact]
    public void Parse_Template_ObjectsAreNotPartOfTheSchema()
        => DdlReader.Instance.Read("TEMPLATE t BEGIN CREATE TABLE x (id int NOT NULL); END;")
            .Schema.Schemas.ShouldBeEmpty();

    [Fact]
    public void Parse_Template_UnqualifiedForeignKeyBindsToPlaceholder()
    {
        var template = ReadTemplate(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE child (
                parent_id int NOT NULL,
                CONSTRAINT fk FOREIGN KEY (parent_id) REFERENCES parent (id)
              );
            END;
            """);

        var fk = template.Objects.Tables.ShouldHaveSingleItem().ForeignKeys.ShouldHaveSingleItem();
        fk.ReferencedSchema.ShouldBe(TemplateDefinition.TargetSchemaPlaceholder);
        fk.ReferencedTable.ShouldBe("parent");
    }

    [Fact]
    public void Parse_Template_QualifiedForeignKeyEscapes()
    {
        var template = ReadTemplate(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE child (
                user_id int NOT NULL,
                CONSTRAINT fk FOREIGN KEY (user_id) REFERENCES public.users (id)
              );
            END;
            """);

        template.Objects.Tables.ShouldHaveSingleItem().ForeignKeys.ShouldHaveSingleItem()
            .ReferencedSchema.ShouldBe("public");
    }

    [Fact]
    public void Parse_Template_StandaloneIndexAttachesToItsTable()
    {
        var template = ReadTemplate(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE outbox (created_at datetimeoffset NOT NULL);
              CREATE INDEX ix_outbox_created_at ON outbox (created_at);
            END;
            """);

        template.Objects.Tables.ShouldHaveSingleItem().Indexes.ShouldHaveSingleItem()
            .Name.ShouldBe("ix_outbox_created_at");
    }

    [Fact]
    public void Parse_Template_TriggerAttachesToItsTable()
    {
        var template = ReadTemplate(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE outbox (id int NOT NULL);
              CREATE TRIGGER trg AFTER INSERT ON outbox FOR EACH ROW EXECUTE FUNCTION publish();
            END;
            """);

        template.Objects.Tables.ShouldHaveSingleItem().Triggers.ShouldHaveSingleItem()
            .Function.ShouldBe("publish");
    }

    [Fact]
    public void Parse_Template_TableGrantAttaches()
    {
        var template = ReadTemplate(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE outbox (id int NOT NULL);
              GRANT SELECT, INSERT ON outbox TO svc;
            END;
            """);

        template.Objects.Tables.ShouldHaveSingleItem().Grants.ShouldHaveSingleItem().Role.ShouldBe("svc");
    }

    [Fact]
    public void Parse_Template_DocCommentAttachesToInnerObject()
    {
        var template = ReadTemplate(
            """
            TEMPLATE t
            BEGIN
              --- The transactional outbox.
              CREATE TABLE outbox (id int NOT NULL);
            END;
            """);

        template.Objects.Tables.ShouldHaveSingleItem().Comment.ShouldBe("The transactional outbox.");
    }

    [Fact]
    public void Parse_Template_EmptyBody_YieldsNoObjects()
    {
        var template = ReadTemplate("TEMPLATE t BEGIN END;");

        template.Objects.Tables.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_TemplateAndSchema_Coexist()
    {
        var document = DdlReader.Instance.Read(
            """
            CREATE SCHEMA app;
            TEMPLATE t BEGIN CREATE TABLE x (id int NOT NULL); END;
            APPLY TEMPLATE t IN SCHEMA app;
            """);

        document.Schema.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
        document.Templates.ShouldHaveSingleItem().Name.ShouldBe("t");
        document.Applications.ShouldHaveSingleItem().TemplateName.ShouldBe("t");
    }

    [Fact]
    public void Parse_ApplyTemplate_CapturesNameAndSchemaList()
    {
        var application = DdlReader.Instance.Read("APPLY TEMPLATE outbox IN SCHEMA billing, ordering, shipping;")
            .Applications.ShouldHaveSingleItem();

        application.TemplateName.ShouldBe("outbox");
        application.SchemaNames.ShouldBe(["billing", "ordering", "shipping"]);
    }

    [Fact]
    public void Parse_ApplyTemplate_DuplicateSchema_Throws()
        => Should.Throw<DdlSyntaxException>(() => DdlReader.Instance.Read("APPLY TEMPLATE t IN SCHEMA a, A;"))
            .Message.ShouldContain("more than once");

    [Fact]
    public void Parse_Template_QualifiedDeclaration_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadTemplate(
                "TEMPLATE t BEGIN CREATE TABLE app.outbox (id int NOT NULL); END;"))
            .Message.ShouldContain("unqualified");

    [Fact]
    public void Parse_Template_CreateSchemaInside_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadTemplate("TEMPLATE t BEGIN CREATE SCHEMA app; END;"))
            .Message.ShouldContain("CREATE SCHEMA is not supported inside a template");

    [Fact]
    public void Parse_Template_CreateViewInside_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadTemplate("TEMPLATE t BEGIN CREATE VIEW v AS SELECT 1; END;"))
            .Message.ShouldContain("CREATE VIEW is not supported inside a template");

    [Fact]
    public void Parse_Template_CreateMaterializedViewInside_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadTemplate("TEMPLATE t BEGIN CREATE MATERIALIZED VIEW v AS SELECT 1; END;"))
            .Message.ShouldContain("CREATE MATERIALIZED VIEW is not supported inside a template");

    [Fact]
    public void Parse_Template_CreateExtensionInside_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadTemplate("TEMPLATE t BEGIN CREATE EXTENSION citext; END;"))
            .Message.ShouldContain("CREATE EXTENSION is not supported inside a template");

    [Fact]
    public void Parse_Template_GrantUsageOnSchemaInside_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadTemplate("TEMPLATE t BEGIN GRANT USAGE ON SCHEMA app TO svc; END;"))
            .Message.ShouldContain("GRANT USAGE ON SCHEMA is not supported inside a template");

    [Fact]
    public void Parse_Template_DropInside_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadTemplate("TEMPLATE t BEGIN DROP TABLE app.x; END;"))
            .Message.ShouldContain("inside a template");

    [Fact]
    public void Parse_Template_DeploymentScriptInside_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadTemplate("TEMPLATE t BEGIN PRE DEPLOYMENT 'x' AS $$ SELECT 1; $$; END;"))
            .Message.ShouldContain("inside a template");

    [Fact]
    public void Parse_Template_NestedTemplateInside_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadTemplate("TEMPLATE t BEGIN TEMPLATE u BEGIN END; END;"))
            .Message.ShouldContain("inside a template");

    [Fact]
    public void Parse_Template_Unterminated_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadTemplate("TEMPLATE t BEGIN CREATE TABLE x (id int NOT NULL);"))
            .Message.ShouldContain("Unterminated template 't'");

    [Fact]
    public void Parse_Template_MissingBegin_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadTemplate("TEMPLATE t CREATE TABLE x (id int NOT NULL); END;"))
            .Message.ShouldContain("BEGIN");

    [Fact]
    public void Parse_UnqualifiedNameOutsideTemplate_StillThrows()
        // The template-body binding must not leak: outside a template every name stays schema-qualified.
        => Should.Throw<DdlSyntaxException>(() => DdlReader.Instance.Read("CREATE TABLE outbox (id int NOT NULL);"))
            .Message.ShouldContain("Expected '.'");
}
