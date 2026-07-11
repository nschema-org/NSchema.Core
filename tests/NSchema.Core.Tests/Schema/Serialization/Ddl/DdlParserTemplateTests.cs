using NSchema.Schema.Ddl;
using NSchema.Schema.Model.Templates;

namespace NSchema.Tests.Schema.Serialization.Ddl;

public sealed class DdlParserTemplateTests
{
    private static TemplateDefinition ReadTemplate(string source) =>
        DdlReader.Instance.Read(source).Templates.Definitions.ShouldHaveSingleItem();

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
        document.Templates.Definitions.ShouldHaveSingleItem().Name.ShouldBe("t");
        document.Templates.Applications.ShouldHaveSingleItem().TemplateName.ShouldBe("t");
    }

    [Fact]
    public void Parse_ApplyTemplate_CapturesNameAndSchemaList()
    {
        var application = DdlReader.Instance.Read("APPLY TEMPLATE outbox IN SCHEMA billing, ordering, shipping;")
            .Templates.Applications.ShouldHaveSingleItem();

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
    public void Parse_Template_OldDeploymentFormInside_Throws()
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

    // --- table templates (FOR TABLE) and INCLUDE members -----------------------

    [Fact]
    public void Parse_TableTemplate_CapturesMembers()
    {
        var template = ReadTemplate(
            """
            TEMPLATE audit_columns FOR TABLE
            BEGIN
              created_at datetimeoffset NOT NULL,
              updated_at datetimeoffset NOT NULL,
              CONSTRAINT chk_updated CHECK (updated_at >= created_at),
              INDEX ix_updated_at (updated_at)
            END;
            """);

        template.Kind.ShouldBe(TemplateKind.Table);
        var members = template.Objects.Tables.ShouldHaveSingleItem();
        members.Columns.Select(c => c.Name).ShouldBe(["created_at", "updated_at"]);
        members.CheckConstraints.ShouldHaveSingleItem().Name.ShouldBe("chk_updated");
        members.Indexes.ShouldHaveSingleItem().Name.ShouldBe("ix_updated_at");
    }

    [Fact]
    public void Parse_SchemaTemplate_HasSchemaKind()
        => ReadTemplate("TEMPLATE t BEGIN END;").Kind.ShouldBe(TemplateKind.Schema);

    [Fact]
    public void Parse_ExplicitForSchema_IsAccepted()
        => ReadTemplate("TEMPLATE t FOR SCHEMA BEGIN END;").Kind.ShouldBe(TemplateKind.Schema);

    [Fact]
    public void Parse_ForUnknownKind_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadTemplate("TEMPLATE t FOR VIEW BEGIN END;"))
            .Message.ShouldContain("Expected SCHEMA or TABLE after FOR");

    [Fact]
    public void Parse_TableTemplate_UnqualifiedForeignKeyBindsToPlaceholder()
    {
        var template = ReadTemplate(
            """
            TEMPLATE tenant_columns FOR TABLE
            BEGIN
              tenant_id uuid NOT NULL,
              CONSTRAINT fk_tenant FOREIGN KEY (tenant_id) REFERENCES tenants (id)
            END;
            """);

        template.Objects.Tables.ShouldHaveSingleItem().ForeignKeys.ShouldHaveSingleItem()
            .ReferencedSchema.ShouldBe(TemplateDefinition.TargetSchemaPlaceholder);
    }

    [Fact]
    public void Parse_TableTemplate_EmptyBody_YieldsNoMembers()
        => ReadTemplate("TEMPLATE t FOR TABLE BEGIN END;")
            .Objects.Tables.ShouldHaveSingleItem().Columns.ShouldBeEmpty();

    [Fact]
    public void Parse_TableTemplate_IncludeInside_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadTemplate(
                """
                TEMPLATE t FOR TABLE
                BEGIN
                  INCLUDE other_template,
                  created_at datetimeoffset NOT NULL
                END;
                """))
            .Message.ShouldContain("cannot include another");

    [Fact]
    public void Parse_Include_CapturesTargetNameAndColumnPosition()
    {
        // Includes never live on the parsed Table — templates are an expansion layer over the domain model —
        // so the include rides the document, targeting its table by name.
        var document = DdlReader.Instance.Read(
            """
            CREATE TABLE app.invoices (
              id uuid NOT NULL,
              INCLUDE audit_columns,
              total decimal(18,2) NOT NULL
            );
            """);

        var include = document.Templates.Includes.ShouldHaveSingleItem();
        include.SchemaName.ShouldBe("app");
        include.TableName.ShouldBe("invoices");
        include.TemplateName.ShouldBe("audit_columns");
        include.ColumnPosition.ShouldBe(1);
    }

    [Fact]
    public void Parse_Include_AsLastMember()
        => DdlReader.Instance.Read("CREATE TABLE app.t (id int NOT NULL, INCLUDE audit_columns);")
            .Templates.Includes.ShouldHaveSingleItem().TemplateName.ShouldBe("audit_columns");

    [Fact]
    public void Parse_ColumnNamedInclude_StillParsesAsAColumn()
    {
        // Compat: 'include' followed by anything more than a bare identifier is a column named include, exactly
        // as it parsed before templates existed.
        var document = DdlReader.Instance.Read("CREATE TABLE app.t (include bigint NOT NULL);");

        document.Templates.Includes.ShouldBeEmpty();
        var column = document.Schema.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem()
            .Columns.ShouldHaveSingleItem();
        column.Name.ShouldBe("include");
        column.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Parse_IncludeInsideSchemaTemplateTable_RidesTheDefinition()
    {
        // Composition: a table declared by a schema template can include a table template. The include belongs
        // to the definition (not the document, and never the table), re-targeted per instance at expansion.
        var document = DdlReader.Instance.Read(
            """
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox (id uuid NOT NULL, INCLUDE audit_columns);
            END;
            """);

        document.Templates.Includes.ShouldBeEmpty();
        var include = document.Templates.Definitions.ShouldHaveSingleItem().Includes.ShouldHaveSingleItem();
        include.TemplateName.ShouldBe("audit_columns");
        include.SchemaName.ShouldBe(TemplateDefinition.TargetSchemaPlaceholder);
        include.TableName.ShouldBe("outbox");
    }
}
