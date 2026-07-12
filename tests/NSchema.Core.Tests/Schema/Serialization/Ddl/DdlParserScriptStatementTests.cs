using NSchema.Project.Ddl;
using NSchema.Project.Ddl.Models;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Tests.Schema.Serialization.Ddl;

/// <summary>
/// Covers the unified <c>SCRIPT '&lt;name&gt;' RUN [ALWAYS | ONCE] ON &lt;event&gt; AS $$ … $$;</c> statement —
/// the only form of deployment scripts and data migrations — and the pointed errors raised for the
/// pre-5.0 spellings.
/// </summary>
public sealed class DdlParserScriptStatementTests
{
    private static DdlDocument Read(string source) => DdlReader.Instance.Read(source);

    private static IReadOnlyList<Script> Migrations(DdlDocument document) =>
        [.. document.Scripts.Where(s => s.Event is ChangeEvent)];

    // -------------------------------------------------------------------------
    // Deployment events
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_PreDeploymentEvent_ProducesAScript()
    {
        var script = Read("SCRIPT 'enable_citext' RUN ON PRE DEPLOYMENT AS $$ CREATE EXTENSION IF NOT EXISTS citext; $$;")
            .Scripts.ShouldHaveSingleItem();

        script.Name.ShouldBe("enable_citext");
        script.Event.ShouldBe(new DeploymentEvent(DeploymentPhase.Pre));
        script.Sql.ShouldBe("CREATE EXTENSION IF NOT EXISTS citext;");
        script.RunCondition.ShouldBe(RunCondition.Always);
    }

    [Fact]
    public void Parse_PostDeploymentEvent_ProducesAScript()
        => Read("SCRIPT 'reindex' RUN ON POST DEPLOYMENT AS $$ SELECT 1; $$;")
            .Scripts.ShouldHaveSingleItem().Event.ShouldBe(new DeploymentEvent(DeploymentPhase.Post));

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
        var migration = Migrations(Read("SCRIPT 'backfill emails' RUN ON ADD COLUMN app.users.email AS $$ UPDATE app.users SET email = ''; $$;"))
            .ShouldHaveSingleItem();

        migration.Name.ShouldBe("backfill emails");
        var change = migration.Event.ShouldBeOfType<ChangeEvent>();
        change.Trigger.ShouldBe(ChangeTrigger.AddColumn);
        change.Path.ShouldBe("app.users.email");
        migration.RunCondition.ShouldBe(RunCondition.Always);
    }

    [Theory]
    [InlineData("ALTER COLUMN TYPE app.orders.total", ChangeTrigger.AlterColumnType)]
    [InlineData("ADD CONSTRAINT app.orders.total_positive", ChangeTrigger.AddConstraint)]
    public void Parse_OtherChangeEvents_CarryTheTrigger(string eventText, ChangeTrigger trigger)
        => Migrations(Read($"SCRIPT 'x' RUN ON {eventText} AS $$ SELECT 1; $$;"))
            .ShouldHaveSingleItem().Event.ShouldBeOfType<ChangeEvent>().Trigger.ShouldBe(trigger);

    [Fact]
    public void Parse_RunOnceChangeEvent_SetsTheRunCondition()
        => Migrations(Read("SCRIPT 'x' RUN ONCE ON ADD COLUMN app.users.email AS $$ SELECT 1; $$;"))
            .ShouldHaveSingleItem().RunCondition.ShouldBe(RunCondition.Once);

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
    // Removed pre-5.0 spellings
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_OldDeploymentForm_NoLongerParses()
        => Should.Throw<DdlSyntaxException>(() => Read("POST DEPLOYMENT 'reindex' AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("Unknown statement 'POST'");

    [Fact]
    public void Parse_OldMigrationForm_NoLongerParses()
        => Should.Throw<DdlSyntaxException>(() => Read("MIGRATION 'backfill' FOR ADD COLUMN app.users.email AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("Unknown statement 'MIGRATION'");

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
            """).Templates.Definitions.ShouldHaveSingleItem().Scripts.ShouldHaveSingleItem();

        var change = migration.Event.ShouldBeOfType<ChangeEvent>();
        change.TableName.ShouldBe("outbox_events");
        change.MemberName.ShouldBe("trace_id");
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

        script.Event.ShouldBe(new DeploymentEvent(DeploymentPhase.Post));
        script.RunCondition.ShouldBe(RunCondition.Once);
    }

    [Fact]
    public void Parse_OldMigrationFormInTemplate_IsRejected()
        => Should.Throw<DdlSyntaxException>(() => Read(
            """
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL, trace_id text NOT NULL );
              MIGRATION FOR ADD COLUMN outbox_events.trace_id AS $$ SELECT 1; $$;
            END;
            """)).Message.ShouldContain("Unexpected 'MIGRATION' inside a template");
}
