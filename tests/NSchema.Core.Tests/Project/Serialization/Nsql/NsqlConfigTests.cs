using NSchema.Plugins;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Config;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// The configuration grammar and its settings translation: config files parse to typed statements, a
/// statement translates to the <see cref="PluginSettings"/> a plugin is handed, and the two grammars never
/// mix in one file.
/// </summary>
public sealed class NsqlConfigTests
{
    private static IReadOnlyList<ConfigStatement> Read(string source)
    {
        var result = NsqlReader.ReadConfig(source);
        result.IsSuccess.ShouldBeTrue();
        return result.Value.Statements;
    }

    private static PluginSettings Settings(string source) => PluginSettings.From(Read(source).ShouldHaveSingleItem());

    [Fact]
    public void ReadConfig_UnlabelledStatement_TranslatesTypeAndAttributes()
    {
        var settings = Settings(
            """
            STATE (
              dialect = 'postgres',
              transaction_mode = 'single'
            );
            """);

        settings.Label.ShouldBeNull();
        settings.Attribute("dialect")!.AsString().ShouldBe("postgres");
        settings.Attribute("transaction_mode")!.AsString().ShouldBe("single");
    }

    [Fact]
    public void ReadConfig_LabelledStatement_TranslatesLabel()
    {
        Read("STATE file ( path = 'state/app.nsstate' );").ShouldHaveSingleItem().ShouldBeOfType<StateStatement>();

        var settings = Settings("STATE file ( path = 'state/app.nsstate' );");
        settings.Label.ShouldBe("file");
        settings.Attribute("path")!.AsString().ShouldBe("state/app.nsstate");
    }

    [Fact]
    public void ReadConfig_KeywordIsCaseInsensitive()
        => Read("Database postgres ( x = 1 );").ShouldHaveSingleItem().ShouldBeOfType<DatabaseStatement>();

    [Fact]
    public void ReadConfig_TranslatesAllValueKinds()
    {
        var settings = Settings(
            """
            DATABASE postgres (
              schema_search_path = 'app',
              connection_timeout = 1000,
              statement_cache = -1,
              prefer_simple = true,
              ssl = false,
              transaction_mode = single
            );
            """);

        settings.Attribute("schema_search_path")!.Kind.ShouldBe(ConfigValueKind.String);
        settings.Attribute("connection_timeout")!.AsInteger().ShouldBe(1000);
        settings.Attribute("statement_cache")!.AsInteger().ShouldBe(-1);
        settings.Attribute("prefer_simple")!.AsBoolean().ShouldBeTrue();
        settings.Attribute("ssl")!.AsBoolean().ShouldBeFalse();
        settings.Attribute("transaction_mode")!.Kind.ShouldBe(ConfigValueKind.Identifier);
    }

    [Fact]
    public void ReadConfig_DottedKey_IsPreservedVerbatim()
        => Settings("DATABASE postgres ( pool.max = 10 );").Attribute("pool.max")!.AsInteger().ShouldBe(10);

    [Fact]
    public void ReadConfig_AttributeLookup_IsCaseInsensitive()
        => Settings("DATABASE postgres ( Dialect = 'postgres' );").Attribute("dialect")!.AsString().ShouldBe("postgres");

    [Fact]
    public void ReadConfig_EmptyAttributeList_IsAllowed()
        => Settings("STATE ();").Attributes.ShouldBeEmpty();

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
}
