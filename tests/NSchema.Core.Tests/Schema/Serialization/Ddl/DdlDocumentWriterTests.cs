using System.Text;
using NSchema.Schema.Ddl;
using NSchema.Schema.Ddl.Model;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Schemas;
using NSchema.State;

namespace NSchema.Tests.Schema.Serialization.Ddl;

/// <summary>
/// Covers <see cref="DdlWriter.Write(DdlDocument)"/> — the full-document round-trip (config blocks + schema +
/// deployment scripts) that the <c>fmt</c> command depends on for losslessness.
/// </summary>
public sealed class DdlDocumentWriterTests
{
    // Canonicalize the schema half via the internal state serializer (independent of the writer under test).
    private static string Canonical(DatabaseSchema schema)
        => Encoding.UTF8.GetString(new SchemaStateSerializer().Serialize(schema).Span);

    private static void AssertEquivalent(DdlDocument expected, DdlDocument actual)
    {
        Canonical(actual.Schema).ShouldBe(Canonical(expected.Schema));

        actual.Config.Count.ShouldBe(expected.Config.Count);
        for (var i = 0; i < expected.Config.Count; i++)
        {
            var (e, a) = (expected.Config[i], actual.Config[i]);
            a.Type.ShouldBe(e.Type);
            a.Label.ShouldBe(e.Label);
            a.Attributes.Count.ShouldBe(e.Attributes.Count);
            foreach (var (key, value) in e.Attributes)
            {
                a.Attribute(key).ShouldBe(value);
            }
        }

        actual.Scripts.ShouldBe(expected.Scripts);
        actual.Migrations.ShouldBe(expected.Migrations);
    }

    // Round-trip a source through Read -> Write -> Read, asserting the document survives and that a second
    // Write produces byte-identical output (formatting is idempotent — the property `fmt --check` relies on).
    private static string AssertRoundTrips(string source)
    {
        var document = DdlReader.Instance.Read(source);
        var formatted = DdlWriter.Instance.Write(document);

        var reparsed = DdlReader.Instance.Read(formatted);
        AssertEquivalent(document, reparsed);
        DdlWriter.Instance.Write(reparsed).ShouldBe(formatted);

        return formatted;
    }

    // -------------------------------------------------------------------------
    // Configuration blocks
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_ConfigBlock_RoundTripsAllValueKinds()
    {
        var formatted = AssertRoundTrips(
            """
            NSCHEMA (
              dialect = 'postgres',
              transaction_mode = single,
              retries = 3,
              verbose = true,
              pool.max = -1,
              note = 'it''s fine'
            );
            """);

        formatted.ShouldContain("NSCHEMA (");
        formatted.ShouldContain("dialect = 'postgres'");
        formatted.ShouldContain("transaction_mode = single");
        formatted.ShouldContain("retries = 3");
        formatted.ShouldContain("verbose = true");
        formatted.ShouldContain("pool.max = -1");
        formatted.ShouldContain("note = 'it''s fine'");
    }

    [Fact]
    public void Write_ConfigBlock_PreservesKeywordAndLabel()
        => AssertRoundTrips("BACKEND file (\n  path = 'state/app.nsstate'\n);")
            .ShouldContain("BACKEND file (");

    [Fact]
    public void Write_ConfigBlock_WithNoAttributes_EmitsEmptyParens()
        => AssertRoundTrips("PROVIDER postgres ();")
            .ShouldContain("PROVIDER postgres ();");

    // -------------------------------------------------------------------------
    // Deployment scripts
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_PreAndPostScripts_RoundTrip()
    {
        var formatted = AssertRoundTrips(
            """
            PRE DEPLOYMENT 'enable_citext' AS $$
                CREATE EXTENSION IF NOT EXISTS citext;
            $$;

            POST DEPLOYMENT 'reindex' (run_outside_transaction = true) AS $$
                CREATE INDEX CONCURRENTLY idx ON app.t (name);
            $$;
            """);

        // The writer emits the canonical SCRIPT form, whichever spelling was read.
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
        var formatted = AssertRoundTrips("PRE DEPLOYMENT 'x' AS $outer$ SELECT '$$' AS v $outer$;");

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
            MIGRATION 'backfill_emails' FOR ADD COLUMN app.users.email AS $$
                UPDATE app.users SET email = '';
            $$;
            """).ShouldContain("SCRIPT 'backfill_emails' RUN ON ADD COLUMN app.users.email AS $$");

    [Fact]
    public void Write_AnonymousMigration_RoundTrips()
        => AssertRoundTrips(
            """
            MIGRATION FOR ADD CONSTRAINT app.orders.total_positive AS $$
                DELETE FROM app.orders WHERE total <= 0;
            $$;
            """).ShouldContain("MIGRATION FOR ADD CONSTRAINT app.orders.total_positive AS $$");

    [Fact]
    public void Write_MigrationWithRunOutsideTransaction_RoundTrips()
        => AssertRoundTrips(
            "MIGRATION FOR ALTER COLUMN TYPE app.orders.total (run_outside_transaction = true) AS $$ SELECT 1; $$;")
            .ShouldContain("MIGRATION FOR ALTER COLUMN TYPE app.orders.total (run_outside_transaction = true) AS $$");

    [Fact]
    public void Write_MigrationBodyContainingDoubleDollar_PicksASafeTag()
    {
        var formatted = AssertRoundTrips("MIGRATION FOR ADD COLUMN app.t.c AS $outer$ SELECT '$$' AS v $outer$;");

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
            NSCHEMA (
              dialect = 'postgres'
            );

            CREATE SCHEMA app;

            CREATE TABLE app.users
            (
                id bigint NOT NULL IDENTITY,
                name text NOT NULL,
                CONSTRAINT users_pkey PRIMARY KEY (id)
            );

            POST DEPLOYMENT 'seed' AS $$
                INSERT INTO app.users (name) VALUES ('root');
            $$;
            """);

    [Fact]
    public void Write_EmitsConfigThenSchemaThenScripts_RegardlessOfSourceOrder()
    {
        // Source deliberately interleaves: script, schema, config.
        var formatted = DdlWriter.Instance.Write(DdlReader.Instance.Read(
            """
            POST DEPLOYMENT 'seed' AS $$ SELECT 1 $$;

            CREATE SCHEMA app;

            NSCHEMA (
              dialect = 'postgres'
            );
            """));

        var config = formatted.IndexOf("NSCHEMA", StringComparison.Ordinal);
        var schema = formatted.IndexOf("CREATE SCHEMA app", StringComparison.Ordinal);
        var script = formatted.IndexOf("POST DEPLOYMENT", StringComparison.Ordinal);

        config.ShouldBeLessThan(schema);
        schema.ShouldBeLessThan(script);
    }

    [Fact]
    public void Write_DatabaseSchemaOverload_EmitsNoConfigOrScripts()
    {
        var ddl = DdlWriter.Instance.Write(new DatabaseSchema([new SchemaDefinition("app")]));

        ddl.ShouldNotContain("NSCHEMA");
        ddl.ShouldNotContain("DEPLOYMENT");
        ddl.ShouldBe("CREATE SCHEMA app;\n");
    }
}
