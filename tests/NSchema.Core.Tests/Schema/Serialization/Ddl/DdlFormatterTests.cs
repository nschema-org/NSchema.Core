using NSchema.Schema.Ddl;

namespace NSchema.Tests.Schema.Serialization.Ddl;

public sealed class DdlFormatterTests
{
    private static string Format(string source) => DdlFormatter.Instance.Format(source);

    // -------------------------------------------------------------------------
    // Layout normalisation
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_MessyTable_NormalisesLayoutPreservingContentAndComments()
    {
        const string input =
            """
            create schema app;
            create table app.users(
              id   bigint not null identity,
               email text not null,  -- login
              constraint pk primary key(id))
            ;
            """;

        const string expected =
            """
            create schema app;

            create table app.users (
              id   bigint not null identity,
              email text not null,  -- login
              constraint pk primary key(id)
            );
            """;

        Format(input).ShouldBe(expected + "\n");
    }

    [Fact]
    public void Format_IsIdempotent()
    {
        const string input =
            """
            create schema app;
            create table app.users(
              id   bigint not null identity,
               email text not null,  -- login
              constraint pk primary key(id))
            ;
            """;

        var once = Format(input);
        Format(once).ShouldBe(once);
    }

    [Fact]
    public void Format_PreservesKeywordCasingAndExpressionSpelling()
    {
        // Gentle: content is preserved verbatim (lowercase keywords, the '> 0' spacing inside the CHECK).
        const string input = "create table app.t (\n  qty int not null,\n  constraint c check (qty > 0)\n);";
        Format(input).ShouldBe(input + "\n");
    }

    [Fact]
    public void Format_SeparatesTopLevelStatementsWithOneBlankLine()
        => Format("create schema a;\ncreate schema b;\ncreate schema c;")
            .ShouldBe("create schema a;\n\ncreate schema b;\n\ncreate schema c;\n");

    // -------------------------------------------------------------------------
    // Verbatim (non-breaking) statements keep their bodies
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_MultiLineViewBody_IsPreservedVerbatim()
    {
        const string input =
            """
            create view app.active as
              select id, name
              from app.users
              where active;
            """;

        // A view body never breaks like a table; it is emitted as-is (only the leading indentation is trimmed).
        Format(input).ShouldBe(input + "\n");
    }

    [Fact]
    public void Format_DeploymentScript_KeepsDollarBodyAndInternalSemicolons()
    {
        const string input =
            """
            PRE DEPLOYMENT 'seed' AS $$
            INSERT INTO app.t VALUES (1);
            INSERT INTO app.t VALUES (2);
            $$;
            """;

        Format(input).ShouldBe(input + "\n");
    }

    [Fact]
    public void Format_EnumStaysInline()
        => Format("create enum app.status ('pending', 'shipped');")
            .ShouldBe("create enum app.status ('pending', 'shipped');\n");

    // -------------------------------------------------------------------------
    // Configuration blocks break their attribute list
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_ConfigBlock_BreaksAttributesOnePerLine()
        => Format("NSCHEMA ( dialect = postgres, colour = false );")
            .ShouldBe("NSCHEMA (\n  dialect = postgres,\n  colour = false\n);\n");

    // -------------------------------------------------------------------------
    // Comments
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_LeadingComments_HugTheFollowingStatement()
    {
        const string input = "-- a plain note\n--- a doc comment\ncreate schema app;";
        Format(input).ShouldBe("-- a plain note\n--- a doc comment\ncreate schema app;\n");
    }

    [Fact]
    public void Format_TrailingLineCommentOnStatement_StaysOnItsLine()
        => Format("create schema app; -- the app schema")
            .ShouldBe("create schema app;  -- the app schema\n");

    [Fact]
    public void Format_LineCommentBeforeMemberComma_PutsCommaBeforeTheComment()
    {
        // The ',' must land before the line comment, never inside it (which would comment the ',' out).
        const string input = "create table app.t (\n  a int -- first\n, b int\n);";
        Format(input).ShouldBe("create table app.t (\n  a int,  -- first\n  b int\n);\n");
    }

    [Fact]
    public void Format_OwnLineCommentBetweenMembers_IndentsWithTheMembers()
    {
        const string input = "create table app.t (\n  a int,\n  -- the b column\n  b int\n);";
        Format(input).ShouldBe("create table app.t (\n  a int,\n  -- the b column\n  b int\n);\n");
    }

    // -------------------------------------------------------------------------
    // Edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_EmptyInput_IsEmpty() => Format("").ShouldBe("");

    [Fact]
    public void Format_WhitespaceOnly_IsEmpty() => Format("   \n\n  ").ShouldBe("");

    [Fact]
    public void Format_CommentsOnly_ArePreserved()
        => Format("-- just a note\n").ShouldBe("-- just a note\n");

    [Fact]
    public void Format_Null_Throws()
        => Should.Throw<ArgumentNullException>(() => Format(null!));

    // -------------------------------------------------------------------------
    // Rich snapshot — a deliberately messy document across many constructs
    // -------------------------------------------------------------------------

    [Fact]
    public Task Format_RichDocument_MatchesSnapshot()
    {
        const string input =
            """
            NSCHEMA (   dialect = postgres   );

            create schema app;
            grant usage on schema app to readonly;

            --- The order status.
            create enum app.status ('pending','shipped','delivered');

            create sequence app.order_seq (start 100, increment 1);

            create table app.orders(
                id bigint not null identity,
                  status app.status not null default 'pending',  -- current state
                total numeric not null,
                -- the audit columns
                created_at timestamptz not null default now(),
                constraint orders_pkey primary key (id),
                constraint total_positive check (total > 0))
            ;

            create view app.open_orders as
                select id, total
                from app.orders
                where status = 'pending';

            POST DEPLOYMENT 'reindex' (run_outside_transaction = true) AS $$
            REINDEX TABLE app.orders;
            $$;
            """;

        return Verify(Format(input));
    }

    [Fact]
    public void Format_RichDocument_IsIdempotent()
    {
        const string input =
            """
            NSCHEMA (   dialect = postgres   );

            create schema app;
            grant usage on schema app to readonly;

            --- The order status.
            create enum app.status ('pending','shipped','delivered');

            create sequence app.order_seq (start 100, increment 1);

            create table app.orders(
                id bigint not null identity,
                  status app.status not null default 'pending',  -- current state
                total numeric not null,
                -- the audit columns
                created_at timestamptz not null default now(),
                constraint orders_pkey primary key (id),
                constraint total_positive check (total > 0))
            ;

            create view app.open_orders as
                select id, total
                from app.orders
                where status = 'pending';

            POST DEPLOYMENT 'reindex' (run_outside_transaction = true) AS $$
            REINDEX TABLE app.orders;
            $$;
            """;

        var once = Format(input);
        Format(once).ShouldBe(once);
    }
}
