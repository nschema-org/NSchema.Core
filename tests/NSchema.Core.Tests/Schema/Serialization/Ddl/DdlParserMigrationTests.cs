using NSchema.Schema.Ddl;
using NSchema.Schema.Model.Migrations;

namespace NSchema.Tests.Schema.Serialization.Ddl;

public sealed class DdlParserMigrationTests
{
    private static IReadOnlyList<DataMigration> ReadMigrations(string source) => DdlReader.Instance.Read(source).Migrations;

    [Fact]
    public void Parse_AddColumnTrigger_CapturesTriggerAndPathParts()
    {
        var migration = ReadMigrations("MIGRATION FOR ADD COLUMN app.users.email AS $$ UPDATE app.users SET email = ''; $$;")
            .ShouldHaveSingleItem();

        migration.Trigger.ShouldBe(DataMigrationTrigger.AddColumn);
        migration.SchemaName.ShouldBe("app");
        migration.ObjectName.ShouldBe("users");
        migration.MemberName.ShouldBe("email");
        migration.Path.ShouldBe("app.users.email");
    }

    [Fact]
    public void Parse_AlterColumnTypeTrigger_CapturesTriggerAndPathParts()
    {
        var migration = ReadMigrations("MIGRATION FOR ALTER COLUMN TYPE app.orders.total AS $$ SELECT 1; $$;")
            .ShouldHaveSingleItem();

        migration.Trigger.ShouldBe(DataMigrationTrigger.AlterColumnType);
        migration.Path.ShouldBe("app.orders.total");
    }

    [Fact]
    public void Parse_AddConstraintTrigger_CapturesTriggerAndPathParts()
    {
        var migration = ReadMigrations("MIGRATION FOR ADD CONSTRAINT app.orders.total_positive AS $$ DELETE FROM app.orders WHERE total <= 0; $$;")
            .ShouldHaveSingleItem();

        migration.Trigger.ShouldBe(DataMigrationTrigger.AddConstraint);
        migration.Path.ShouldBe("app.orders.total_positive");
    }

    [Fact]
    public void Parse_NamedMigration_CarriesTheName()
    {
        var migration = ReadMigrations("MIGRATION 'backfill emails' FOR ADD COLUMN app.users.email AS $$ SELECT 1; $$;")
            .ShouldHaveSingleItem();

        migration.Name.ShouldBe("backfill emails");
        migration.Description.ShouldBe("backfill emails");
    }

    [Fact]
    public void Parse_AnonymousMigration_HasNullName()
        => ReadMigrations("MIGRATION FOR ADD COLUMN app.users.email AS $$ SELECT 1; $$;")
            .ShouldHaveSingleItem().Name.ShouldBeNull();

    [Fact]
    public void Description_Unnamed_FallsBackToTriggerAndPath()
        => ReadMigrations("MIGRATION FOR ADD COLUMN app.users.email AS $$ SELECT 1; $$;")
            .ShouldHaveSingleItem().Description.ShouldBe("ADD COLUMN app.users.email");

    [Fact]
    public void Parse_RunOutsideTransactionOption_IsCaptured()
        => ReadMigrations("MIGRATION FOR ADD COLUMN app.users.email (run_outside_transaction = true) AS $$ SELECT 1; $$;")
            .ShouldHaveSingleItem().RunOutsideTransaction.ShouldBeTrue();

    [Fact]
    public void Parse_WithoutOptions_RunOutsideTransactionDefaultsToFalse()
        => ReadMigrations("MIGRATION FOR ADD COLUMN app.users.email AS $$ SELECT 1; $$;")
            .ShouldHaveSingleItem().RunOutsideTransaction.ShouldBeFalse();

    [Fact]
    public void Parse_Body_PreservesInnerSemicolonsVerbatim()
    {
        // The dollar-quoted body is opaque: inner ';' is part of the migration, not a terminator.
        var migration = ReadMigrations(
            """
            MIGRATION FOR ADD COLUMN app.users.email AS $$
                UPDATE app.users SET email = 'a;b';
                UPDATE app.users SET email = '';
            $$;
            """).ShouldHaveSingleItem();

        migration.Sql.ShouldBe("UPDATE app.users SET email = 'a;b';\n    UPDATE app.users SET email = '';");
    }

    [Fact]
    public void Parse_CustomDollarTag_PreservesEmbeddedDoubleDollar()
    {
        // A differently-tagged $$ inside the body is just content; only the opening tag closes it.
        var migration = ReadMigrations("MIGRATION FOR ADD COLUMN app.t.c AS $tag$ SELECT $$nested$$; $tag$;")
            .ShouldHaveSingleItem();

        migration.Sql.ShouldBe("SELECT $$nested$$;");
    }

    [Fact]
    public void Parse_UnknownTrigger_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadMigrations("MIGRATION FOR DROP COLUMN app.users.email AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("Expected 'ADD COLUMN', 'ALTER COLUMN TYPE' or 'ADD CONSTRAINT'.");

    [Fact]
    public void Parse_TwoPartPath_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadMigrations("MIGRATION FOR ADD COLUMN app.users AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("'.'");

    [Fact]
    public void Parse_WrongTokenBeforeBody_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadMigrations("MIGRATION FOR ADD COLUMN app.users.email WHEN $$ SELECT 1; $$;"))
            .Message.ShouldContain("AS");

    [Fact]
    public void Parse_UnknownOption_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadMigrations("MIGRATION FOR ADD COLUMN app.users.email (whoops = true) AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("run_outside_transaction");

    [Fact]
    public void Parse_MissingTerminatingSemicolon_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadMigrations("MIGRATION FOR ADD COLUMN app.users.email AS $$ SELECT 1; $$"))
            .Message.ShouldContain("';' to end the migration");

    [Fact]
    public void Parse_MigrationInsideTemplateBody_BindsToThePlaceholderSchema()
    {
        // Arrange — inside a template the path is unqualified (table.member); the schema binds per application.
        var document = DdlReader.Instance.Read(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE users ( id int NOT NULL );
              MIGRATION 'backfill' FOR ADD COLUMN users.email (run_outside_transaction = true) AS $$ UPDATE {schema}.users SET email = ''; $$;
            END;
            """);

        // Assert — the migration rides the definition, not the document's top-level list.
        document.Migrations.ShouldBeEmpty();
        var migration = document.Templates.Definitions.ShouldHaveSingleItem().Migrations.ShouldHaveSingleItem();
        migration.Name.ShouldBe("backfill");
        migration.SchemaName.ShouldBe("<template>");
        migration.ObjectName.ShouldBe("users");
        migration.MemberName.ShouldBe("email");
        migration.RunOutsideTransaction.ShouldBeTrue();
        migration.Sql.ShouldBe("UPDATE {schema}.users SET email = '';");
    }

    [Fact]
    public void Parse_QualifiedPathInsideTemplateBody_IsRejected()
        => Should.Throw<DdlSyntaxException>(() => ReadMigrations(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE users ( id int NOT NULL );
              MIGRATION FOR ADD COLUMN app.users.email AS $$ SELECT 1; $$;
            END;
            """)).Message.ShouldContain("A migration inside a template must use an unqualified 'table.member' path");

    [Fact]
    public void Parse_TemplateMigrationForUndeclaredTable_IsRejected()
        => Should.Throw<DdlSyntaxException>(() => ReadMigrations(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE users ( id int NOT NULL );
              MIGRATION FOR ADD COLUMN orders.total AS $$ SELECT 1; $$;
            END;
            """)).Message.ShouldContain("Template 't' declares a migration for table 'orders', which the template does not declare.");

    [Fact]
    public void Parse_MigrationInsideTableTemplateBody_IsRejected()
        // A table template's body holds comma-separated members, not statements, so MIGRATION has no home there.
        => Should.Throw<DdlSyntaxException>(() => ReadMigrations(
            """
            TEMPLATE t FOR TABLE
            BEGIN
              MIGRATION FOR ADD COLUMN users.email AS $$ SELECT 1; $$
            END;
            """));
}
