using NSchema.Schema;
using NSchema.Schema.Ddl;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Schemas;

namespace NSchema.Tests.Schema;

public sealed class TemplateExpanderTests
{
    /// <summary>Parses <paramref name="source"/> and expands its templates, as the desired-schema provider would.</summary>
    private static DatabaseSchema Expand(string source)
    {
        var document = DdlReader.Instance.Read(source);
        return TemplateExpander.Expand(document.Schema, document.Templates).Schema;
    }

    private static SchemaDefinition Schema(DatabaseSchema schema, string name)
    {
        var match = schema.Schemas.FirstOrDefault(s => s.Name == name);
        match.ShouldNotBeNull($"expected a schema named '{name}'");
        return match;
    }

    [Fact]
    public void Expand_InstantiatesTheTemplateIntoEachTargetSchema()
    {
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            CREATE SCHEMA ordering;
            TEMPLATE outbox BEGIN CREATE TABLE outbox (id uuid NOT NULL); END;
            APPLY TEMPLATE outbox IN SCHEMA billing, ordering;
            """);

        Schema(schema, "billing").Tables.ShouldHaveSingleItem().Name.ShouldBe("outbox");
        Schema(schema, "ordering").Tables.ShouldHaveSingleItem().Name.ShouldBe("outbox");
    }

    [Fact]
    public void Expand_InstancesCoexistWithHandWrittenObjects()
    {
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            CREATE TABLE billing.invoices (id int NOT NULL);
            TEMPLATE outbox BEGIN CREATE TABLE outbox (id uuid NOT NULL); END;
            APPLY TEMPLATE outbox IN SCHEMA billing;
            """);

        Schema(schema, "billing").Tables.Select(t => t.Name).ShouldBe(["invoices", "outbox"], ignoreOrder: true);
    }

    [Fact]
    public void Expand_RewritesUnqualifiedForeignKeyToTheTargetSchema()
    {
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            TEMPLATE t
            BEGIN
              CREATE TABLE parent (id int NOT NULL, CONSTRAINT pk PRIMARY KEY (id));
              CREATE TABLE child (
                parent_id int NOT NULL,
                CONSTRAINT fk FOREIGN KEY (parent_id) REFERENCES parent (id)
              );
            END;
            APPLY TEMPLATE t IN SCHEMA billing;
            """);

        var child = Schema(schema, "billing").Tables.First(t => t.Name == "child");
        var fk = child.ForeignKeys.ShouldHaveSingleItem();
        fk.ReferencedSchema.ShouldBe("billing");
        fk.ReferencedTable.ShouldBe("parent");
    }

    [Fact]
    public void Expand_LeavesQualifiedForeignKeyAlone()
    {
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            TEMPLATE t
            BEGIN
              CREATE TABLE child (
                user_id int NOT NULL,
                CONSTRAINT fk FOREIGN KEY (user_id) REFERENCES public.users (id)
              );
            END;
            APPLY TEMPLATE t IN SCHEMA billing;
            """);

        Schema(schema, "billing").Tables.ShouldHaveSingleItem().ForeignKeys.ShouldHaveSingleItem()
            .ReferencedSchema.ShouldBe("public");
    }

    [Fact]
    public void Expand_QualifiesColumnTypesTheTemplateDeclares()
    {
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            TEMPLATE t
            BEGIN
              CREATE ENUM outbox_status ('pending', 'sent');
              CREATE TABLE outbox (status outbox_status NOT NULL, payload text NOT NULL);
            END;
            APPLY TEMPLATE t IN SCHEMA billing;
            """);

        var columns = Schema(schema, "billing").Tables.ShouldHaveSingleItem().Columns;
        columns.First(c => c.Name == "status").Type.Name.ShouldBe("billing.outbox_status");
        columns.First(c => c.Name == "payload").Type.Name.ShouldBe("text");
    }

    [Fact]
    public void Expand_LeavesTypesTheTemplateDoesNotDeclareAlone()
    {
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            TEMPLATE t
            BEGIN
              CREATE TABLE outbox (kind public.kind_enum NOT NULL, tag citext NOT NULL);
            END;
            APPLY TEMPLATE t IN SCHEMA billing;
            """);

        var columns = Schema(schema, "billing").Tables.ShouldHaveSingleItem().Columns;
        columns.First(c => c.Name == "kind").Type.Name.ShouldBe("public.kind_enum");
        columns.First(c => c.Name == "tag").Type.Name.ShouldBe("citext");
    }

    [Fact]
    public void Expand_QualifiesCompositeFieldTypesTheTemplateDeclares()
    {
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            TEMPLATE t
            BEGIN
              CREATE ENUM status ('a');
              CREATE TYPE envelope AS (state status, note text);
            END;
            APPLY TEMPLATE t IN SCHEMA billing;
            """);

        var fields = Schema(schema, "billing").CompositeTypes.ShouldHaveSingleItem().Fields;
        fields.First(f => f.Name == "state").DataType.Name.ShouldBe("billing.status");
        fields.First(f => f.Name == "note").DataType.Name.ShouldBe("text");
    }

    [Fact]
    public void Expand_QualifiesTriggerFunctionTheTemplateDeclares()
    {
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            TEMPLATE t
            BEGIN
              CREATE FUNCTION publish() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NEW; END; $$;
              CREATE TABLE outbox (id int NOT NULL);
              CREATE TRIGGER trg AFTER INSERT ON outbox FOR EACH ROW EXECUTE FUNCTION publish();
            END;
            APPLY TEMPLATE t IN SCHEMA billing;
            """);

        Schema(schema, "billing").Tables.ShouldHaveSingleItem().Triggers.ShouldHaveSingleItem()
            .Function.ShouldBe("billing.publish");
    }

    [Fact]
    public void Expand_LeavesTriggerFunctionTheTemplateDoesNotDeclareAlone()
    {
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            TEMPLATE t
            BEGIN
              CREATE TABLE outbox (id int NOT NULL);
              CREATE TRIGGER trg AFTER INSERT ON outbox FOR EACH ROW EXECUTE FUNCTION public.publish();
            END;
            APPLY TEMPLATE t IN SCHEMA billing;
            """);

        Schema(schema, "billing").Tables.ShouldHaveSingleItem().Triggers.ShouldHaveSingleItem()
            .Function.ShouldBe("public.publish");
    }

    [Fact]
    public void Expand_UnusedTemplate_IsInert()
    {
        var schema = Expand(
            """
            CREATE SCHEMA app;
            TEMPLATE unused BEGIN CREATE TABLE x (id int NOT NULL); END;
            """);

        Schema(schema, "app").Tables.ShouldBeEmpty();
    }

    [Fact]
    public void Expand_UnknownTemplate_Throws()
        => Should.Throw<InvalidOperationException>(() => Expand(
                """
                CREATE SCHEMA app;
                APPLY TEMPLATE missing IN SCHEMA app;
                """))
            .Message.ShouldContain("unknown template 'missing'");

    [Fact]
    public void Expand_UnknownTargetSchema_Throws()
        => Should.Throw<InvalidOperationException>(() => Expand(
                """
                TEMPLATE t BEGIN CREATE TABLE x (id int NOT NULL); END;
                APPLY TEMPLATE t IN SCHEMA missing;
                """))
            .Message.ShouldContain("unknown schema 'missing'");

    [Fact]
    public void Expand_DuplicateTemplateName_Throws()
        => Should.Throw<InvalidOperationException>(() => Expand(
                """
                TEMPLATE t BEGIN END;
                TEMPLATE t BEGIN END;
                """))
            .Message.ShouldContain("Duplicate template 't'");

    [Fact]
    public void Expand_InstanceCollidingWithDeclaredObject_Throws()
        => Should.Throw<InvalidOperationException>(() => Expand(
                """
                CREATE SCHEMA billing;
                CREATE TABLE billing.outbox (id int NOT NULL);
                TEMPLATE t BEGIN CREATE TABLE outbox (id uuid NOT NULL); END;
                APPLY TEMPLATE t IN SCHEMA billing;
                """))
            .Message.ShouldContain("Duplicate table 'outbox'");

    [Fact]
    public void Expand_ApplyingTheSameTemplateTwiceToOneSchema_Throws()
        => Should.Throw<InvalidOperationException>(() => Expand(
                """
                CREATE SCHEMA billing;
                TEMPLATE t BEGIN CREATE TABLE outbox (id uuid NOT NULL); END;
                APPLY TEMPLATE t IN SCHEMA billing;
                APPLY TEMPLATE t IN SCHEMA billing;
                """))
            .Message.ShouldContain("Duplicate table 'outbox'");

    // --- table templates (INCLUDE) ---------------------------------------------

    private const string AuditColumns =
        """
        TEMPLATE audit_columns FOR TABLE
        BEGIN
          created_at datetimeoffset NOT NULL,
          updated_at datetimeoffset NOT NULL,
          CONSTRAINT chk_audit CHECK (updated_at >= created_at)
        END;
        """;

    [Fact]
    public void Expand_MergesIncludedMembersIntoTheTable()
    {
        var schema = Expand(
            $"""
            CREATE SCHEMA billing;
            CREATE TABLE billing.invoices (id uuid NOT NULL, INCLUDE audit_columns);
            {AuditColumns}
            """);

        var table = Schema(schema, "billing").Tables.ShouldHaveSingleItem();
        table.Columns.Select(c => c.Name).ShouldBe(["id", "created_at", "updated_at"]);
        table.CheckConstraints.ShouldHaveSingleItem().Name.ShouldBe("chk_audit");
    }

    [Fact]
    public void Expand_InsertsIncludedColumnsAtTheIncludePosition()
    {
        var schema = Expand(
            $"""
            CREATE SCHEMA billing;
            CREATE TABLE billing.invoices (
              id uuid NOT NULL,
              INCLUDE audit_columns,
              total decimal(18,2) NOT NULL
            );
            {AuditColumns}
            """);

        Schema(schema, "billing").Tables.ShouldHaveSingleItem()
            .Columns.Select(c => c.Name).ShouldBe(["id", "created_at", "updated_at", "total"]);
    }

    [Fact]
    public void Expand_IncludedForeignKeyBindsToTheIncludingTablesSchema()
    {
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            CREATE TABLE billing.tenants (id uuid NOT NULL, CONSTRAINT pk_tenants PRIMARY KEY (id));
            CREATE TABLE billing.invoices (id uuid NOT NULL, INCLUDE tenant_columns);
            TEMPLATE tenant_columns FOR TABLE
            BEGIN
              tenant_id uuid NOT NULL,
              CONSTRAINT fk_tenant FOREIGN KEY (tenant_id) REFERENCES tenants (id)
            END;
            """);

        var invoices = Schema(schema, "billing").Tables.First(t => t.Name == "invoices");
        var fk = invoices.ForeignKeys.ShouldHaveSingleItem();
        fk.ReferencedSchema.ShouldBe("billing");
        fk.ReferencedTable.ShouldBe("tenants");
    }

    [Fact]
    public void Expand_IncludedPrimaryKey_IsSet()
    {
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            CREATE TABLE billing.invoices (INCLUDE id_column);
            TEMPLATE id_column FOR TABLE
            BEGIN
              id uuid NOT NULL,
              CONSTRAINT pk_id PRIMARY KEY (id)
            END;
            """);

        Schema(schema, "billing").Tables.ShouldHaveSingleItem().PrimaryKey.ShouldNotBeNull().Name.ShouldBe("pk_id");
    }

    [Fact]
    public void Expand_IncludedPrimaryKeyConflict_Throws()
        => Should.Throw<InvalidOperationException>(() => Expand(
                """
                CREATE SCHEMA billing;
                CREATE TABLE billing.invoices (id uuid NOT NULL, CONSTRAINT pk PRIMARY KEY (id), INCLUDE id_column);
                TEMPLATE id_column FOR TABLE
                BEGIN
                  surrogate_id uuid NOT NULL,
                  CONSTRAINT pk_id PRIMARY KEY (surrogate_id)
                END;
                """))
            .Message.ShouldContain("already declares one");

    [Fact]
    public void Expand_IncludeInsideSchemaTemplateTable_ResolvesPerInstance()
    {
        // Composition: a schema template's table includes a table template; each instantiated copy resolves the
        // include against its own schema.
        var schema = Expand(
            $"""
            CREATE SCHEMA billing;
            CREATE SCHEMA ordering;
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox (id uuid NOT NULL, INCLUDE audit_columns);
            END;
            APPLY TEMPLATE outbox IN SCHEMA billing, ordering;
            {AuditColumns}
            """);

        foreach (var name in new[] { "billing", "ordering" })
        {
            var outbox = Schema(schema, name).Tables.ShouldHaveSingleItem();
            outbox.Columns.Select(c => c.Name).ShouldBe(["id", "created_at", "updated_at"]);
        }
    }

    [Fact]
    public void Expand_DuplicateColumnFromInclude_Throws()
        => Should.Throw<InvalidOperationException>(() => Expand(
                $"""
                CREATE SCHEMA billing;
                CREATE TABLE billing.invoices (created_at datetimeoffset NOT NULL, INCLUDE audit_columns);
                {AuditColumns}
                """))
            .Message.ShouldContain("already declares it");

    [Fact]
    public void Expand_UnknownInclude_Throws()
        => Should.Throw<InvalidOperationException>(() => Expand(
                """
                CREATE SCHEMA billing;
                CREATE TABLE billing.invoices (id uuid NOT NULL, INCLUDE missing);
                """))
            .Message.ShouldContain("unknown template 'missing'");

    [Fact]
    public void Expand_IncludingASchemaTemplate_Throws()
        => Should.Throw<InvalidOperationException>(() => Expand(
                """
                CREATE SCHEMA billing;
                CREATE TABLE billing.invoices (id uuid NOT NULL, INCLUDE outbox);
                TEMPLATE outbox BEGIN CREATE TABLE outbox (id uuid NOT NULL); END;
                """))
            .Message.ShouldContain("only a FOR TABLE template can be included");

    [Fact]
    public void Expand_ApplyingATableTemplateToSchemas_Throws()
        => Should.Throw<InvalidOperationException>(() => Expand(
                $"""
                CREATE SCHEMA billing;
                APPLY TEMPLATE audit_columns IN SCHEMA billing;
                {AuditColumns}
                """))
            .Message.ShouldContain("is a table template");

    [Fact]
    public void Expand_InstantiatesTemplateMigrationsPerAppliedSchema()
    {
        // Arrange
        var document = DdlReader.Instance.Read(
            """
            CREATE SCHEMA sales;
            CREATE SCHEMA billing;
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL, trace_id text NOT NULL );
              MIGRATION 'backfill trace' FOR ADD COLUMN outbox_events.trace_id AS $$ UPDATE {schema}.outbox_events SET trace_id = ''; $$;
            END;
            APPLY TEMPLATE outbox IN SCHEMA sales, billing;
            """);

        // Act
        var (_, migrations) = TemplateExpander.Expand(document.Schema, document.Templates);

        // Assert — one instance per applied schema, with the schema bound and the {schema} token substituted.
        migrations.Count.ShouldBe(2);
        migrations[0].Path.ShouldBe("sales.outbox_events.trace_id");
        migrations[0].Sql.ShouldBe("UPDATE sales.outbox_events SET trace_id = '';");
        migrations[0].Name.ShouldBe("backfill trace");
        migrations[1].Path.ShouldBe("billing.outbox_events.trace_id");
        migrations[1].Sql.ShouldBe("UPDATE billing.outbox_events SET trace_id = '';");
    }

    [Fact]
    public void Expand_UnappliedTemplate_InstantiatesNoMigrations()
    {
        // Arrange — a declared-but-never-applied template contributes nothing.
        var document = DdlReader.Instance.Read(
            """
            CREATE SCHEMA sales;
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL );
              MIGRATION FOR ADD COLUMN outbox_events.trace_id AS $$ SELECT 1; $$;
            END;
            """);

        // Act
        var (_, migrations) = TemplateExpander.Expand(document.Schema, document.Templates);

        // Assert
        migrations.ShouldBeEmpty();
    }

    [Fact]
    public void Expand_MigrationWithoutToken_KeepsSqlVerbatim()
    {
        // Arrange
        var document = DdlReader.Instance.Read(
            """
            CREATE SCHEMA sales;
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL );
              MIGRATION FOR ADD COLUMN outbox_events.trace_id (run_outside_transaction = true) AS $$ SELECT version(); $$;
            END;
            APPLY TEMPLATE outbox IN SCHEMA sales;
            """);

        // Act
        var migration = TemplateExpander.Expand(document.Schema, document.Templates).Migrations.ShouldHaveSingleItem();

        // Assert — no token, no rewriting; the option carries onto the instance.
        migration.Sql.ShouldBe("SELECT version();");
        migration.RunOutsideTransaction.ShouldBeTrue();
    }
}
