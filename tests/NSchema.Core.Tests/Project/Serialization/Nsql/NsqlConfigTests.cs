using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Config;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// The configuration grammar: config files parse to typed statements carrying their labels and attribute
/// value nodes, and the two grammars never mix in one file. Translation into the configuration domain is
/// the assembler's, covered by its own tests.
/// </summary>
public sealed class NsqlConfigTests
{
    private static IReadOnlyList<ConfigStatement> Read(string source)
    {
        var result = NsqlReader.ReadConfig(source);
        result.IsSuccess.ShouldBeTrue();
        return result.Value.Statements;
    }

    [Fact]
    public void ReadConfig_UnlabelledStatement_ParsesTypeAndAttributes()
    {
        var statement = Read(
            """
            STATE (
              dialect = 'postgres',
              transaction_mode = 'single'
            );
            """).ShouldHaveSingleItem().ShouldBeOfType<StateStatement>();

        statement.Label.ShouldBeNull();
        statement.Attributes.Select(a => a.Key).ShouldBe(["dialect", "transaction_mode"]);
        statement.Attributes[0].Value.ShouldBeOfType<StringValue>().Value.ShouldBe("postgres");
    }

    [Fact]
    public void ReadConfig_LabelledStatement_ParsesLabel()
    {
        var statement = Read("STATE file ( path = 'state/app.nsstate' );").ShouldHaveSingleItem().ShouldBeOfType<StateStatement>();

        statement.Label!.Value.ShouldBe("file");
    }

    [Fact]
    public void ReadConfig_KeywordIsCaseInsensitive()
        => Read("Database postgres ( x = 1 );").ShouldHaveSingleItem().ShouldBeOfType<DatabaseStatement>();

    [Fact]
    public void ReadConfig_ParsesAllValueKinds()
    {
        var attributes = Read(
            """
            DATABASE postgres (
              schema_search_path = 'app',
              connection_timeout = 1000,
              statement_cache = -1,
              prefer_simple = true,
              ssl = false,
              transaction_mode = single
            );
            """).Single().Attributes;

        attributes.Single(a => a.Key == "schema_search_path").Value.ShouldBeOfType<StringValue>().Value.ShouldBe("app");
        attributes.Single(a => a.Key == "connection_timeout").Value.ShouldBeOfType<IntegerValue>().Value.ShouldBe(1000);
        attributes.Single(a => a.Key == "statement_cache").Value.ShouldBeOfType<IntegerValue>().Value.ShouldBe(-1);
        attributes.Single(a => a.Key == "prefer_simple").Value.ShouldBeOfType<BooleanValue>().Value.ShouldBeTrue();
        attributes.Single(a => a.Key == "ssl").Value.ShouldBeOfType<BooleanValue>().Value.ShouldBeFalse();
        attributes.Single(a => a.Key == "transaction_mode").Value.ShouldBeOfType<IdentifierValue>().Value.ShouldBe("single");
    }

    [Fact]
    public void ReadConfig_DottedKey_IsPreservedVerbatim()
        => Read("DATABASE postgres ( pool.max = 10 );").Single().Attributes.ShouldHaveSingleItem().Key.ShouldBe("pool.max");

    [Fact]
    public void ReadConfig_EmptyAttributeList_IsAllowed()
        => Read("STATE ();").ShouldHaveSingleItem().Attributes.ShouldBeEmpty();

    [Fact]
    public void ReadConfig_MultipleStatements_KeepDeclarationOrder()
    {
        var statements = Read(
            """
            STATE file ( path = 'state/app.nsstate' );
            DATABASE postgres ( schema_search_path = 'app' );
            STATE s3 ( bucket = 'state' );
            """);

        statements.Select(s => s.GetType().Name).ShouldBe(["StateStatement", "DatabaseStatement", "StateStatement"]);
    }

    [Fact]
    public void ReadConfig_DuplicateAttribute_IsAnError()
        => NsqlReader.ReadConfig("STATE file ( path = 'a', PATH = 'b' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("more than once");

    [Fact]
    public void ReadConfig_UnknownStatement_IsAnError()
        => NsqlReader.ReadConfig("WORKSPACE staging ( region = 'eu' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("Unknown configuration statement 'WORKSPACE'");

    [Fact]
    public void ReadConfig_ProjectStatement_IsAnError()
        => NsqlReader.ReadConfig("CREATE SCHEMA app;")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("Unknown configuration statement 'CREATE'");

    // -------------------------------------------------------------------------
    // PLUGIN
    // -------------------------------------------------------------------------

    [Fact]
    public void ReadConfig_PluginStatement_Parses()
    {
        var statement = Read("PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.1' );")
            .ShouldHaveSingleItem().ShouldBeOfType<PluginStatement>();

        statement.Label!.Value.ShouldBe("pg");
        statement.Attributes.Select(a => a.Key).ShouldBe(["source", "version"]);
    }

    [Fact]
    public void ReadConfig_PluginStatement_WithoutLabel_IsAnError()
        => NsqlReader.ReadConfig("PLUGIN ( source = 'NSchema.Postgres', version = '5.0.1' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("requires a label");

    // -------------------------------------------------------------------------
    // ENGINE
    // -------------------------------------------------------------------------

    [Fact]
    public void ReadConfig_EngineStatement_Parses()
    {
        var statement = Read("ENGINE ( version = '[5.0,6.0)' );").ShouldHaveSingleItem().ShouldBeOfType<EngineStatement>();

        statement.Label.ShouldBeNull();
        statement.Attributes.ShouldHaveSingleItem().Key.ShouldBe("version");
    }

    [Fact]
    public void ReadConfig_EngineStatement_WithLabel_IsAnError()
        => NsqlReader.ReadConfig("ENGINE prod ( version = '5.0.1' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("takes no label");
}
