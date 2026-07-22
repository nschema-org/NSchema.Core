using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Blocks;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Reading a configuration file: it parses to blocks carrying their labels and attributes, and the
/// configuration and project grammars never mix in one file. Binding into the configuration domain is the
/// assembler's job, covered by its own tests.
/// </summary>
public sealed class NsqlConfigurationTests
{
    private static IReadOnlyList<BlockStatement> Read(string source)
    {
        var result = NsqlReader.ReadConfiguration(source);
        result.IsSuccess.ShouldBeTrue();
        return [.. result.Value.Statements.OfType<BlockStatement>()];
    }

    [Fact]
    public void ReadConfiguration_UnlabelledStatement_ParsesTypeAndAttributes()
    {
        var statement = Read(
            """
            STATE (
              dialect = 'postgres',
              transaction_mode = 'single'
            );
            """).ShouldHaveSingleItem();

        statement.Keyword.ShouldBe(BlockKeyword.State);
        statement.Label.ShouldBeNull();
        statement.Attributes.Select(a => a.Key).ShouldBe(["dialect", "transaction_mode"]);
        statement.Attributes[0].Value.ShouldBe("postgres");
    }

    [Fact]
    public void ReadConfiguration_LabelledStatement_ParsesLabel()
    {
        var statement = Read("STATE file ( path = 'state/app.nsstate' );").ShouldHaveSingleItem();

        statement.Keyword.ShouldBe(BlockKeyword.State);
        statement.Label!.Value.ShouldBe("file");
    }

    [Fact]
    public void ReadConfiguration_KeywordIsCaseInsensitive()
        => Read("Database postgres ( x = 1 );").ShouldHaveSingleItem().Keyword.ShouldBe(BlockKeyword.Database);

    [Fact]
    public void ReadConfiguration_ParsesAllValueKinds()
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

        attributes.Single(a => a.Key == "schema_search_path").Value.ShouldBe("app");
        attributes.Single(a => a.Key == "connection_timeout").Value.ShouldBe("1000");
        attributes.Single(a => a.Key == "statement_cache").Value.ShouldBe("-1");
        attributes.Single(a => a.Key == "prefer_simple").Value.ShouldBe("true");
        attributes.Single(a => a.Key == "ssl").Value.ShouldBe("false");
        attributes.Single(a => a.Key == "transaction_mode").Value.ShouldBe("single");
    }

    [Fact]
    public void ReadConfiguration_DottedKey_IsPreservedVerbatim()
        => Read("DATABASE postgres ( pool.max = 10 );").Single().Attributes.ShouldHaveSingleItem().Key.ShouldBe("pool.max");

    [Fact]
    public void ReadConfiguration_EmptyAttributeList_IsAllowed()
        => Read("STATE ();").ShouldHaveSingleItem().Attributes.ShouldBeEmpty();

    [Fact]
    public void ReadConfiguration_MultipleStatements_KeepDeclarationOrder()
    {
        var statements = Read(
            """
            STATE file ( path = 'state/app.nsstate' );
            DATABASE postgres ( schema_search_path = 'app' );
            STATE s3 ( bucket = 'state' );
            """);

        statements.Select(s => s.Keyword).ShouldBe([BlockKeyword.State, BlockKeyword.Database, BlockKeyword.State]);
    }

    [Fact]
    public void ReadConfiguration_DuplicateAttribute_IsAnError()
        => NsqlReader.ReadConfiguration("STATE file ( path = 'a', PATH = 'b' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("more than once");

    [Fact]
    public void ReadConfiguration_UnknownStatement_IsAnError()
        => NsqlReader.ReadConfiguration("WORKSPACE staging ( region = 'eu' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("Unknown statement 'WORKSPACE'");

    [Fact]
    public void ReadConfiguration_ProjectStatement_IsAnError()
        => NsqlReader.ReadConfiguration("CREATE SCHEMA app;")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("A configuration file holds only PLUGIN, ENGINE, DATABASE, and STATE statements.");

    // -------------------------------------------------------------------------
    // PLUGIN
    // -------------------------------------------------------------------------

    [Fact]
    public void ReadConfiguration_PluginStatement_Parses()
    {
        var statement = Read("PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.1' );")
            .ShouldHaveSingleItem();

        statement.Keyword.ShouldBe(BlockKeyword.Plugin);
        statement.Label!.Value.ShouldBe("pg");
        statement.Attributes.Select(a => a.Key).ShouldBe(["source", "version"]);
    }

    [Fact]
    public void ReadConfiguration_PluginStatement_WithoutLabel_IsAnError()
        => NsqlReader.ReadConfiguration("PLUGIN ( source = 'NSchema.Postgres', version = '5.0.1' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("requires a label");

    // -------------------------------------------------------------------------
    // ENGINE
    // -------------------------------------------------------------------------

    [Fact]
    public void ReadConfiguration_EngineStatement_Parses()
    {
        var statement = Read("ENGINE ( version = '[5.0,6.0)' );").ShouldHaveSingleItem();

        statement.Keyword.ShouldBe(BlockKeyword.Engine);
        statement.Label.ShouldBeNull();
        statement.Attributes.ShouldHaveSingleItem().Key.ShouldBe("version");
    }

    [Fact]
    public void ReadConfiguration_EngineStatement_WithLabel_IsAnError()
        => NsqlReader.ReadConfiguration("ENGINE prod ( version = '5.0.1' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("takes no label");
}
