using NSchema.Project.Domain.Models.Scripts;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Covers the change-event side of the SCRIPT statement — the data-migration declarations
/// (<c>SCRIPT &lt;name&gt; RUN ON &lt;change event&gt; &lt;path&gt; AS $$ … $$;</c>).
/// </summary>
public sealed class NsqlParserMigrationTests
{
    private static IReadOnlyList<ChangeScript> ReadMigrations(string source) =>
        new TestNsqlParser(source).Parse().Directives.Tables.ChangeScripts;

    [Fact]
    public void Parse_AddColumnTrigger_CapturesTriggerAndPathParts()
    {
        var migration = ReadMigrations("SCRIPT backfill RUN ON ADD COLUMN app.users.email AS $$ UPDATE app.users SET email = ''; $$;")
            .ShouldHaveSingleItem();

        migration.Trigger.ShouldBe(ChangeTrigger.AddColumn);
        migration.ScopeSchema.ShouldBe("app");
        migration.TableName.ShouldBe("users");
        migration.MemberName.ShouldBe("email");
        migration.Path.ShouldBe("app.users.email");
    }

    [Fact]
    public void Parse_AlterColumnTypeTrigger_CapturesTriggerAndPathParts()
    {
        var migration = ReadMigrations("SCRIPT retype RUN ON ALTER COLUMN TYPE app.orders.total AS $$ SELECT 1; $$;")
            .ShouldHaveSingleItem();

        migration.Trigger.ShouldBe(ChangeTrigger.AlterColumnType);
        migration.Path.ShouldBe("app.orders.total");
    }

    [Fact]
    public void Parse_AddConstraintTrigger_CapturesTriggerAndPathParts()
    {
        var migration = ReadMigrations("SCRIPT dedupe RUN ON ADD CONSTRAINT app.orders.total_positive AS $$ DELETE FROM app.orders WHERE total <= 0; $$;")
            .ShouldHaveSingleItem();

        migration.Trigger.ShouldBe(ChangeTrigger.AddConstraint);
        migration.Path.ShouldBe("app.orders.total_positive");
    }

    [Fact]
    public void Parse_Migration_CarriesTheName()
        => ReadMigrations("SCRIPT backfill_emails RUN ON ADD COLUMN app.users.email AS $$ SELECT 1; $$;")
            .ShouldHaveSingleItem().Name.ShouldBe("backfill_emails");

    [Fact]
    public void Parse_RunOutsideTransactionOption_IsCaptured()
        => ReadMigrations("SCRIPT x RUN ON ADD COLUMN app.users.email (run_outside_transaction = true) AS $$ SELECT 1; $$;")
            .ShouldHaveSingleItem().RunOutsideTransaction.ShouldBeTrue();

    [Fact]
    public void Parse_WithoutOptions_RunOutsideTransactionDefaultsToFalse()
        => ReadMigrations("SCRIPT x RUN ON ADD COLUMN app.users.email AS $$ SELECT 1; $$;")
            .ShouldHaveSingleItem().RunOutsideTransaction.ShouldBeFalse();

    [Fact]
    public void Parse_Body_PreservesInnerSemicolonsVerbatim()
    {
        // The dollar-quoted body is opaque: inner ';' is part of the migration, not a terminator.
        var migration = ReadMigrations(
            """
            SCRIPT backfill RUN ON ADD COLUMN app.users.email AS $$
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
        var migration = ReadMigrations("SCRIPT x RUN ON ADD COLUMN app.t.c AS $tag$ SELECT $$nested$$; $tag$;")
            .ShouldHaveSingleItem();

        migration.Sql.ShouldBe("SELECT $$nested$$;");
    }

    [Fact]
    public void Parse_UnknownTrigger_Throws()
        => Should.Throw<NsqlSyntaxException>(() => ReadMigrations("SCRIPT x RUN ON ADD INDEX app.users.email AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("Expected 'ADD COLUMN', 'ALTER COLUMN TYPE' or 'ADD CONSTRAINT'.");

    [Fact]
    public void Parse_TwoPartPath_Throws()
        => Should.Throw<NsqlSyntaxException>(() => ReadMigrations("SCRIPT x RUN ON ADD COLUMN app.users AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("'.'");

    [Fact]
    public void Parse_WrongTokenBeforeBody_Throws()
        => Should.Throw<NsqlSyntaxException>(() => ReadMigrations("SCRIPT x RUN ON ADD COLUMN app.users.email WHEN $$ SELECT 1; $$;"))
            .Message.ShouldContain("AS");

    [Fact]
    public void Parse_UnknownOption_Throws()
        => Should.Throw<NsqlSyntaxException>(() => ReadMigrations("SCRIPT x RUN ON ADD COLUMN app.users.email (whoops = true) AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("run_outside_transaction");

    [Fact]
    public void Parse_MissingTerminatingSemicolon_Throws()
        => Should.Throw<NsqlSyntaxException>(() => ReadMigrations("SCRIPT x RUN ON ADD COLUMN app.users.email AS $$ SELECT 1; $$"))
            .Message.ShouldContain("';' to end the script");

    [Fact]
    public void Parse_MigrationInsideTemplate_InstantiatesPerAppliedSchema()
    {
        var read = NSchema.Project.Nsql.NsqlReader.Read(
            """
            CREATE SCHEMA app;
            TEMPLATE t
            BEGIN
              CREATE TABLE users ( id int NOT NULL );
              SCRIPT backfill RUN ON ADD COLUMN users.email (run_outside_transaction = true) AS $$ UPDATE {schema}.users SET email = ''; $$;
            END;
            APPLY TEMPLATE t IN SCHEMA app;
            """);
        read.IsSuccess.ShouldBeTrue();
        var assembled = NSchema.Project.ProjectAssembler.Assemble([read.Value]);
        assembled.IsSuccess.ShouldBeTrue();

        var migration = assembled.Value.AllScripts().ShouldHaveSingleItem();
        migration.Name.ShouldBe("backfill");
        var change = migration.ShouldBeOfType<ChangeScript>();
        change.ScopeSchema.ShouldBe("app");
        change.TableName.ShouldBe("users");
        change.MemberName.ShouldBe("email");
        migration.RunOutsideTransaction.ShouldBeTrue();
        migration.Sql.Value.ShouldBe("UPDATE app.users SET email = '';");
    }

    [Fact]
    public void Parse_QualifiedPathInsideTemplateBody_IsRejected()
        => Should.Throw<NsqlSyntaxException>(() => ReadMigrations(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE users ( id int NOT NULL );
              SCRIPT x RUN ON ADD COLUMN app.users.email AS $$ SELECT 1; $$;
            END;
            """)).Message.ShouldContain("A migration inside a template must use an unqualified 'table.member' path");

    [Fact]
    public void Parse_TemplateMigrationForUndeclaredTable_IsAccepted()
        // The table may come from another template applied to the same schemas (e.g. a migration rolled out
        // to only some instances); whether the change matches is decided at diff time, not parse time.
        => Should.NotThrow(() => new TestNsqlParser(
            """
            TEMPLATE t
            BEGIN
              SCRIPT x RUN ON ADD COLUMN orders.total AS $$ SELECT 1; $$;
            END;
            """).Parse());

    [Fact]
    public void Parse_MigrationInsideTableTemplateBody_IsRejected()
        // A table template's body holds comma-separated members, not statements, so SCRIPT has no home there.
        => Should.Throw<NsqlSyntaxException>(() => ReadMigrations(
            """
            TEMPLATE t FOR TABLE
            BEGIN
              SCRIPT x RUN ON ADD COLUMN users.email AS $$ SELECT 1; $$
            END;
            """));
}
