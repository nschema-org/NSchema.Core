using NSchema.Schema.Ddl;
using NSchema.Schema.Ddl.Model;
using NSchema.Schema.Model.Migrations;
using NSchema.Schema.Model.Scripts;

namespace NSchema.Tests.Schema.Serialization.Ddl;

/// <summary>
/// Covers the unified <c>SCRIPT '&lt;name&gt;' RUN [ALWAYS | ONCE] ON &lt;event&gt; AS $$ … $$;</c> statement —
/// the canonical form of deployment scripts and data migrations — and the deprecation warnings raised
/// for the pre-SCRIPT spellings.
/// </summary>
public sealed class DdlParserScriptStatementTests
{
    private static DdlDocument Read(string source) => DdlReader.Instance.Read(source);

    // Warnings describe the read, not the document, so they ride on the parser's result.
    private static IReadOnlyList<DdlWarning> Warnings(string source) => new DdlParser(source).Parse().Warnings;

    // -------------------------------------------------------------------------
    // Deployment events
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_PreDeploymentEvent_ProducesAScript()
    {
        var script = Read("SCRIPT 'enable_citext' RUN ON PRE DEPLOYMENT AS $$ CREATE EXTENSION IF NOT EXISTS citext; $$;")
            .Scripts.ShouldHaveSingleItem();

        script.Name.ShouldBe("enable_citext");
        script.Type.ShouldBe(ScriptType.PreDeployment);
        script.Sql.ShouldBe("CREATE EXTENSION IF NOT EXISTS citext;");
        script.RunCondition.ShouldBe(RunCondition.Always);
    }

    [Fact]
    public void Parse_PostDeploymentEvent_ProducesAScript()
        => Read("SCRIPT 'reindex' RUN ON POST DEPLOYMENT AS $$ SELECT 1; $$;")
            .Scripts.ShouldHaveSingleItem().Type.ShouldBe(ScriptType.PostDeployment);

    [Fact]
    public void Parse_RunAlways_IsTheExplicitSpellingOfTheDefault()
        => Read("SCRIPT 'x' RUN ALWAYS ON PRE DEPLOYMENT AS $$ SELECT 1; $$;")
            .Scripts.ShouldHaveSingleItem().RunCondition.ShouldBe(RunCondition.Always);

    [Fact]
    public void Parse_RunOnce_SetsTheRunCondition()
        => Read("SCRIPT 'seed' RUN ONCE ON POST DEPLOYMENT AS $$ INSERT INTO app.currencies VALUES ('GBP'); $$;")
            .Scripts.ShouldHaveSingleItem().RunCondition.ShouldBe(RunCondition.Once);

    [Fact]
    public void Parse_Options_AreSharedWithTheOldForm()
        => Read("SCRIPT 'x' RUN ON POST DEPLOYMENT (run_outside_transaction = true) AS $$ SELECT 1; $$;")
            .Scripts.ShouldHaveSingleItem().RunOutsideTransaction.ShouldBeTrue();

    // -------------------------------------------------------------------------
    // Change events
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_AddColumnEvent_ProducesADataMigration()
    {
        var migration = Read("SCRIPT 'backfill emails' RUN ON ADD COLUMN app.users.email AS $$ UPDATE app.users SET email = ''; $$;")
            .Migrations.ShouldHaveSingleItem();

        migration.Name.ShouldBe("backfill emails");
        migration.Trigger.ShouldBe(DataMigrationTrigger.AddColumn);
        migration.Path.ShouldBe("app.users.email");
        migration.RunCondition.ShouldBe(RunCondition.Always);
    }

    [Theory]
    [InlineData("ALTER COLUMN TYPE app.orders.total", DataMigrationTrigger.AlterColumnType)]
    [InlineData("ADD CONSTRAINT app.orders.total_positive", DataMigrationTrigger.AddConstraint)]
    public void Parse_OtherChangeEvents_CarryTheTrigger(string eventText, DataMigrationTrigger trigger)
        => Read($"SCRIPT 'x' RUN ON {eventText} AS $$ SELECT 1; $$;")
            .Migrations.ShouldHaveSingleItem().Trigger.ShouldBe(trigger);

    [Fact]
    public void Parse_RunOnceChangeEvent_SetsTheRunCondition()
        => Read("SCRIPT 'x' RUN ONCE ON ADD COLUMN app.users.email AS $$ SELECT 1; $$;")
            .Migrations.ShouldHaveSingleItem().RunCondition.ShouldBe(RunCondition.Once);

    // -------------------------------------------------------------------------
    // Errors
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_UnlessExists_IsReserved()
        => Should.Throw<DdlSyntaxException>(() => Read("SCRIPT 'x' RUN UNLESS EXISTS (SELECT 1) ON POST DEPLOYMENT AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("'UNLESS EXISTS' is reserved for a future release");

    [Fact]
    public void Parse_MissingName_Throws()
        => Should.Throw<DdlSyntaxException>(() => Read("SCRIPT RUN ON PRE DEPLOYMENT AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("Expected a quoted script name");

    [Fact]
    public void Parse_MissingRun_Throws()
        => Should.Throw<DdlSyntaxException>(() => Read("SCRIPT 'x' ON PRE DEPLOYMENT AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("Expected 'RUN'");

    [Fact]
    public void Parse_MissingOn_Throws()
        => Should.Throw<DdlSyntaxException>(() => Read("SCRIPT 'x' RUN AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("Expected 'ON'");

    [Fact]
    public void Parse_UnknownEvent_Throws()
        => Should.Throw<DdlSyntaxException>(() => Read("SCRIPT 'x' RUN ON DELETE TABLE app.users AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("Expected a script event");

    // -------------------------------------------------------------------------
    // Deprecation warnings
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_ScriptStatement_ProducesNoWarnings()
        => Warnings("SCRIPT 'x' RUN ONCE ON PRE DEPLOYMENT AS $$ SELECT 1; $$;").ShouldBeEmpty();

    [Fact]
    public void Parse_OldDeploymentForm_WarnsWithTheReplacement()
    {
        var warning = Warnings("POST DEPLOYMENT 'reindex' AS $$ SELECT 1; $$;").ShouldHaveSingleItem();

        warning.Message.ShouldContain("'POST DEPLOYMENT' is deprecated");
        warning.Message.ShouldContain("SCRIPT 'reindex' RUN ON POST DEPLOYMENT");
        warning.Position.Line.ShouldBe(1);
    }

    [Fact]
    public void Parse_OldMigrationForm_WarnsWithTheReplacement()
    {
        var warning = Warnings("MIGRATION 'backfill' FOR ADD COLUMN app.users.email AS $$ SELECT 1; $$;")
            .ShouldHaveSingleItem();

        warning.Message.ShouldContain("'MIGRATION FOR' is deprecated");
        warning.Message.ShouldContain("SCRIPT 'backfill' RUN ON ADD COLUMN app.users.email");
    }

    [Fact]
    public void Parse_OldAnonymousMigrationForm_SuggestsANamePlaceholder()
        => Warnings("MIGRATION FOR ADD COLUMN app.users.email AS $$ SELECT 1; $$;")
            .ShouldHaveSingleItem().Message.ShouldContain("SCRIPT '<name>' RUN ON ADD COLUMN app.users.email");

    // -------------------------------------------------------------------------
    // Template bodies
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_ChangeEventScriptInTemplate_BindsTheSchemaPerApplication()
    {
        var migration = Read(
            """
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL, trace_id text NOT NULL );
              SCRIPT 'backfill {schema} trace' RUN ON ADD COLUMN outbox_events.trace_id AS $$ UPDATE {schema}.outbox_events SET trace_id = ''; $$;
            END;
            """).Templates.Definitions.ShouldHaveSingleItem().Migrations.ShouldHaveSingleItem();

        migration.ObjectName.ShouldBe("outbox_events");
        migration.MemberName.ShouldBe("trace_id");
    }

    [Fact]
    public void Parse_DeploymentScriptInTemplate_LandsOnTheTemplate()
    {
        var script = Read(
            """
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL );
              SCRIPT 'seed {schema}' RUN ONCE ON POST DEPLOYMENT AS $$ INSERT INTO {schema}.outbox_events VALUES (1); $$;
            END;
            """).Templates.Definitions.ShouldHaveSingleItem().Scripts.ShouldHaveSingleItem();

        script.Type.ShouldBe(ScriptType.PostDeployment);
        script.RunCondition.ShouldBe(RunCondition.Once);
    }

    [Fact]
    public void Parse_OldMigrationFormInTemplate_SuggestsTheUnqualifiedPath()
        => Warnings(
            """
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL, trace_id text NOT NULL );
              MIGRATION FOR ADD COLUMN outbox_events.trace_id AS $$ SELECT 1; $$;
            END;
            """).ShouldHaveSingleItem().Message.ShouldContain("RUN ON ADD COLUMN outbox_events.trace_id");
}
