using System.Text;
using NSchema.Current.Domain.Models;
using NSchema.Current.Storage;
using NSchema.Project.Ddl;
using NSchema.Project.Ddl.Models;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Schemas;

namespace NSchema.Tests.Schema.Serialization.Ddl;

/// <summary>
/// Covers <see cref="DdlWriter.Write(DdlDocument)"/> — the full-document round-trip (schema + scripts)
/// that the <c>fmt</c> command depends on for losslessness.
/// </summary>
public sealed class DdlDocumentWriterTests
{
    // Canonicalize the schema half via the internal state serializer (independent of the writer under test).
    private static string Canonical(DatabaseSchema schema)
        => Encoding.UTF8.GetString(new SchemaStateSerializer().Serialize(new SchemaState(schema)).Span);

    private static void AssertEquivalent(DdlDocument expected, DdlDocument actual)
    {
        Canonical(actual.Schema).ShouldBe(Canonical(expected.Schema));
        actual.Scripts.ShouldBe(expected.Scripts);
    }

    // Round-trip a source through Read -> Write -> Read, asserting the document survives and that a second
    // Write produces byte-identical output (formatting is idempotent — the property `fmt --check` relies on).
    private static string AssertRoundTrips(string source)
    {
        var document = DdlReader.Instance.Read(source).Require();
        var formatted = DdlWriter.Instance.Write(document);

        var reparsed = DdlReader.Instance.Read(formatted).Require();
        AssertEquivalent(document, reparsed);
        DdlWriter.Instance.Write(reparsed).ShouldBe(formatted);

        return formatted;
    }

    // -------------------------------------------------------------------------
    // Deployment scripts
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_PreAndPostScripts_RoundTrip()
    {
        var formatted = AssertRoundTrips(
            """
            SCRIPT 'enable_citext' RUN ON PRE DEPLOYMENT AS $$
                CREATE EXTENSION IF NOT EXISTS citext;
            $$;

            SCRIPT 'reindex' RUN ON POST DEPLOYMENT (run_outside_transaction = true) AS $$
                CREATE INDEX CONCURRENTLY idx ON app.t (name);
            $$;
            """);

        formatted.ShouldContain("SCRIPT 'enable_citext' RUN ON PRE DEPLOYMENT AS $$");
        formatted.ShouldContain("SCRIPT 'reindex' RUN ON POST DEPLOYMENT (run_outside_transaction = true) AS $$");
    }

    [Fact]
    public void Write_RunOnceScript_RoundTrips()
        => AssertRoundTrips("SCRIPT 'seed' RUN ONCE ON POST DEPLOYMENT AS $$ INSERT INTO app.c VALUES (1); $$;")
            .ShouldContain("SCRIPT 'seed' RUN ONCE ON POST DEPLOYMENT AS $$");

    [Fact]
    public void Write_ScriptBodyContainingDoubleDollar_PicksASafeTag()
    {
        var formatted = AssertRoundTrips("SCRIPT 'x' RUN ON PRE DEPLOYMENT AS $outer$ SELECT '$$' AS v $outer$;");

        // The default $$ delimiter would collide with the body, so the writer must choose a tagged delimiter.
        formatted.ShouldNotContain("AS $$");
        formatted.ShouldContain("$body1$");
    }

    // -------------------------------------------------------------------------
    // Data migrations
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_NamedMigration_RoundTrips()
        => AssertRoundTrips(
            """
            SCRIPT 'backfill_emails' RUN ON ADD COLUMN app.users.email AS $$
                UPDATE app.users SET email = '';
            $$;
            """).ShouldContain("SCRIPT 'backfill_emails' RUN ON ADD COLUMN app.users.email AS $$");

    [Fact]
    public void Write_ConstraintMigration_RoundTrips()
        => AssertRoundTrips(
            """
            SCRIPT 'dedupe' RUN ON ADD CONSTRAINT app.orders.total_positive AS $$
                DELETE FROM app.orders WHERE total <= 0;
            $$;
            """).ShouldContain("SCRIPT 'dedupe' RUN ON ADD CONSTRAINT app.orders.total_positive AS $$");

    [Fact]
    public void Write_MigrationWithRunOutsideTransaction_RoundTrips()
        => AssertRoundTrips(
            "SCRIPT 'retype' RUN ON ALTER COLUMN TYPE app.orders.total (run_outside_transaction = true) AS $$ SELECT 1; $$;")
            .ShouldContain("SCRIPT 'retype' RUN ON ALTER COLUMN TYPE app.orders.total (run_outside_transaction = true) AS $$");

    [Fact]
    public void Write_MigrationBodyContainingDoubleDollar_PicksASafeTag()
    {
        var formatted = AssertRoundTrips("SCRIPT 'x' RUN ON ADD COLUMN app.t.c AS $outer$ SELECT '$$' AS v $outer$;");

        // The default $$ delimiter would collide with the body, so the writer must choose a tagged delimiter.
        formatted.ShouldNotContain("AS $$");
        formatted.ShouldContain("$body1$");
    }

    // -------------------------------------------------------------------------
    // Full document
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_FullDocument_RoundTripsAndIsIdempotent()
        => AssertRoundTrips(
            """
            CREATE SCHEMA app;

            CREATE TABLE app.users
            (
                id bigint NOT NULL IDENTITY,
                name text NOT NULL,
                CONSTRAINT users_pkey PRIMARY KEY (id)
            );

            SCRIPT 'seed' RUN ON POST DEPLOYMENT AS $$
                INSERT INTO app.users (name) VALUES ('root');
            $$;
            """);

    [Fact]
    public void Write_EmitsSchemaThenScripts_RegardlessOfSourceOrder()
    {
        // Source deliberately interleaves: script, then schema.
        var formatted = DdlWriter.Instance.Write(DdlReader.Instance.Read(
            """
            SCRIPT 'seed' RUN ON POST DEPLOYMENT AS $$ SELECT 1 $$;

            CREATE SCHEMA app;
            """).Require());

        var schema = formatted.IndexOf("CREATE SCHEMA app", StringComparison.Ordinal);
        var script = formatted.IndexOf("POST DEPLOYMENT", StringComparison.Ordinal);

        schema.ShouldBeLessThan(script);
    }

    [Fact]
    public void Write_DatabaseSchemaOverload_EmitsNoScripts()
    {
        var ddl = DdlWriter.Instance.Write(new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"))]));

        ddl.ShouldNotContain("DEPLOYMENT");
        ddl.ShouldBe("CREATE SCHEMA app;\n");
    }
}
