using NSchema.Schema.Ddl;
using NSchema.Schema.Model.Scripts;

namespace NSchema.Tests.Schema.Serialization.Ddl;

public sealed class DdlParserScriptTests
{
    private static IReadOnlyList<Script> ReadScripts(string source) => DdlReader.Instance.Read(source).Scripts;

    [Fact]
    public void Parse_PreDeployment_CapturesNameBodyAndType()
    {
        var script = ReadScripts("PRE DEPLOYMENT 'enable_citext' AS $$ CREATE EXTENSION IF NOT EXISTS citext; $$;")
            .ShouldHaveSingleItem();

        script.Name.ShouldBe("enable_citext");
        script.Type.ShouldBe(ScriptType.PreDeployment);
        script.Sql.ShouldBe("CREATE EXTENSION IF NOT EXISTS citext;");
        script.RunOutsideTransaction.ShouldBeFalse();
    }

    [Fact]
    public void Parse_PostDeployment_CapturesType()
        => ReadScripts("POST DEPLOYMENT 'backfill' AS $$ UPDATE app.t SET x = 1; $$;")
            .ShouldHaveSingleItem().Type.ShouldBe(ScriptType.PostDeployment);

    [Fact]
    public void Parse_Body_PreservesInnerSemicolonsAndQuotes()
    {
        // The dollar-quoted body is opaque: inner ';' and single quotes are part of the script, not terminators.
        var script = ReadScripts(
            """
            POST DEPLOYMENT 'seed' AS $$
                INSERT INTO app.t (name) VALUES ('a;b');
                UPDATE app.t SET name = '';
            $$;
            """).ShouldHaveSingleItem();

        script.Sql.ShouldBe("INSERT INTO app.t (name) VALUES ('a;b');\n    UPDATE app.t SET name = '';");
    }

    [Fact]
    public void Parse_BodyOnNextLine_IsAccepted()
        => ReadScripts(
            """
            PRE DEPLOYMENT 'x' AS
            $$ SELECT 1; $$;
            """).ShouldHaveSingleItem().Sql.ShouldBe("SELECT 1;");

    [Fact]
    public void Parse_RunOutsideTransactionOption_IsCaptured()
    {
        var script = ReadScripts(
            "POST DEPLOYMENT 'concurrent_index' (run_outside_transaction = true) AS $$ CREATE INDEX CONCURRENTLY i ON app.t (c); $$;")
            .ShouldHaveSingleItem();

        script.RunOutsideTransaction.ShouldBeTrue();
    }

    [Fact]
    public void Parse_CustomDollarTag_IsHonoured()
    {
        // A differently-tagged $$ inside the body is just content; only the opening tag closes it.
        var script = ReadScripts("PRE DEPLOYMENT 'x' AS $body$ SELECT $$nested$$; $body$;").ShouldHaveSingleItem();

        script.Sql.ShouldBe("SELECT $$nested$$;");
    }

    [Fact]
    public void Parse_ScriptsAndSchema_Coexist()
    {
        var document = DdlReader.Instance.Read(
            """
            PRE DEPLOYMENT 'pre' AS $$ SELECT 1; $$;
            CREATE SCHEMA app;
            POST DEPLOYMENT 'post' AS $$ SELECT 2; $$;
            """);

        document.Schema.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
        document.Scripts.Select(s => (s.Name, s.Type)).ShouldBe(
            [("pre", ScriptType.PreDeployment), ("post", ScriptType.PostDeployment)]);
    }

    [Fact]
    public void Parse_DeploymentScripts_AreNotPartOfTheSchema()
        => DdlReader.Instance.Read("PRE DEPLOYMENT 'x' AS $$ SELECT 1; $$;").Schema.Schemas.ShouldBeEmpty();

    [Fact]
    public void Parse_MissingDeploymentKeyword_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadScripts("PRE 'x' AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("DEPLOYMENT");

    [Fact]
    public void Parse_WrongTokenBeforeBody_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadScripts("PRE DEPLOYMENT 'x' WHEN $$ SELECT 1; $$;"))
            .Message.ShouldContain("AS");

    [Fact]
    public void Parse_BodyWithoutAs_Throws()
        // With no 'AS' anchor the body's '$' is reached as an unexpected character — still rejected.
        => Should.Throw<DdlSyntaxException>(() => ReadScripts("PRE DEPLOYMENT 'x' $$ SELECT 1; $$;"));

    [Fact]
    public void Parse_UnknownOption_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadScripts("PRE DEPLOYMENT 'x' (whoops = true) AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("run_outside_transaction");

    [Fact]
    public void Parse_UnterminatedBody_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadScripts("PRE DEPLOYMENT 'x' AS $$ SELECT 1;"))
            .Message.ShouldContain("Unterminated");
}
