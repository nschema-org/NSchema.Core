using NSchema.Config;
using NSchema.Plugins.Model;
using NSchema.Plugins.Model.Config;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Config;

/// <summary>
/// The configuration assembly layer: parsed documents resolve into a <see cref="ConfigDefinition"/>, with
/// the statement rules (closed attribute sets, single declarations, resolvable labels) validated here — the
/// reader stays pure syntax.
/// </summary>
public sealed class ConfigAssemblerTests
{
    private const string Plugin = "PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.1' );";

    private static NsqlConfigDocument Doc(string source, string? path = null)
    {
        var result = NsqlReader.ReadConfig(source);
        result.IsSuccess.ShouldBeTrue();
        return path is null ? result.Value : result.Value with { FilePath = path };
    }

    [Fact]
    public void Assemble_BuildsTheDefinition()
    {
        // Arrange
        var document = Doc(
            $"""
            ENGINE ( version = '[5.0,6.0)' );
            {Plugin}
            DATABASE pg ( host = 'localhost' );
            STATE file ( path = 'state/app.nsstate' );
            """);

        // Act
        var result = ConfigAssembler.Assemble([document]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plugins.ShouldHaveSingleItem().ShouldBe(new PluginDeclaration("pg", "NSchema.Postgres", VersionRange.Parse("5.0.1")));
        result.Value.Engine.ShouldBe(new EngineConfig(new EngineRequirement(VersionRange.Parse("[5.0,6.0)"))));
        result.Value.Database!.Attribute("host")!.AsString().ShouldBe("localhost");
        result.Value.State!.Label.ShouldBe("file");
    }

    [Fact]
    public void Assemble_ReferenceAcrossDocuments_Resolves()
        => ConfigAssembler.Assemble([Doc("DATABASE pg ( host = 'localhost' );"), Doc(Plugin)]).IsSuccess.ShouldBeTrue();

    [Fact]
    public void Assemble_TranslatesAllValueKinds()
    {
        // Arrange
        var document = Doc(
            $"""
            {Plugin}
            DATABASE pg (
              schema_search_path = 'app',
              connection_timeout = 1000,
              statement_cache = -1,
              prefer_simple = true,
              ssl = false,
              transaction_mode = single,
              pool.max = 10
            );
            """);

        // Act
        var database = ConfigAssembler.Assemble([document]).Value!.Database!;

        // Assert
        database.Attribute("schema_search_path")!.Kind.ShouldBe(ConfigValueKind.String);
        database.Attribute("connection_timeout")!.AsInteger().ShouldBe(1000);
        database.Attribute("statement_cache")!.AsInteger().ShouldBe(-1);
        database.Attribute("prefer_simple")!.AsBoolean().ShouldBeTrue();
        database.Attribute("ssl")!.AsBoolean().ShouldBeFalse();
        database.Attribute("transaction_mode")!.Kind.ShouldBe(ConfigValueKind.Identifier);
        database.Attribute("pool.max")!.AsInteger().ShouldBe(10);
    }

    [Fact]
    public void Assemble_AttributeLookup_IsCaseInsensitive()
    {
        var document = Doc($"{Plugin} DATABASE pg ( Dialect = 'postgres' );");

        ConfigAssembler.Assemble([document]).Value!.Database!.Attribute("dialect")!.AsString().ShouldBe("postgres");
    }

    [Fact]
    public void Assemble_RangeNotation_PassesThroughVerbatim()
    {
        var document = Doc("PLUGIN pg ( source = 'NSchema.Postgres', version = '[5.0,6.0)' );");

        ConfigAssembler.Assemble([document]).Value!.Plugins.Single().Version.ShouldBe(VersionRange.Parse("[5.0,6.0)"));
    }

    [Fact]
    public void Assemble_BareVersion_MeansExact()
    {
        var document = Doc("ENGINE ( version = '5.1.0' );");

        ConfigAssembler.Assemble([document]).Value!.Engine!.Requirement.Version.ShouldBe(VersionRange.Parse("[5.1.0]"));
    }

    [Fact]
    public void Assemble_UnknownLabel_IsAnError()
    {
        var document = Doc("DATABASE pg ( host = 'localhost' );");

        var label = document.Statements.Single().Label!;
        ConfigAssembler.Assemble([document]).Errors.ShouldHaveSingleItem()
            .ShouldBe(ConfigDiagnostics.UnknownPluginLabel("DATABASE", "pg", label.Position));
    }

    [Fact]
    public void Assemble_UnlabelledReference_IsAnError()
    {
        var document = Doc("STATE ( path = 'x' );");

        ConfigAssembler.Assemble([document]).Errors.ShouldHaveSingleItem()
            .ShouldBe(ConfigDiagnostics.UnlabelledReference("STATE", document.Statements.Single().Position));
    }

    [Fact]
    public void Assemble_DuplicatePluginLabel_IsAnError()
    {
        var document = Doc($"{Plugin} PLUGIN pg ( source = 'NSchema.Sqlite', version = '5.0.1' );");

        ConfigAssembler.Assemble([document]).Errors.ShouldHaveSingleItem()
            .ShouldBe(ConfigDiagnostics.DuplicatePluginLabel("pg", document.Statements[1].Position));
    }

    [Fact]
    public void Assemble_DuplicatePluginSource_IsAnError()
    {
        var document = Doc($"{Plugin} PLUGIN pg2 ( source = 'NSchema.Postgres', version = '5.0.2' );");

        ConfigAssembler.Assemble([document]).Errors.ShouldHaveSingleItem()
            .ShouldBe(ConfigDiagnostics.DuplicatePluginSource("NSchema.Postgres", document.Statements[1].Position));
    }

    [Theory]
    [InlineData("ENGINE ( version = '5.0.1' ); ENGINE ( version = '5.0.2' );", "ENGINE")]
    [InlineData("PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.1' ); DATABASE pg ( a = 1 ); DATABASE pg ( b = 2 );", "DATABASE")]
    public void Assemble_SecondSingletonStatement_IsAnError(string source, string keyword)
    {
        var document = Doc(source);

        ConfigAssembler.Assemble([document]).Errors.ShouldHaveSingleItem()
            .ShouldBe(ConfigDiagnostics.DuplicateStatement(keyword, document.Statements[^1].Position));
    }

    [Fact]
    public void Assemble_MissingPluginAttributes_ReportsEach()
    {
        var document = Doc("PLUGIN pg ();");

        var statement = document.Statements.ShouldHaveSingleItem();
        ConfigAssembler.Assemble([document]).Errors.ShouldBe([
            ConfigDiagnostics.RequiredAttribute("PLUGIN", "source", statement.Position),
            ConfigDiagnostics.RequiredAttribute("PLUGIN", "version", statement.Position),
        ]);
    }

    [Fact]
    public void Assemble_UnknownPluginAttribute_IsAnError()
    {
        var document = Doc("PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.1', color = 'red' );");

        var attribute = document.Statements.Single().Attributes.Single(a => a.Key == "color");
        ConfigAssembler.Assemble([document]).Errors.ShouldHaveSingleItem()
            .ShouldBe(ConfigDiagnostics.UnknownAttribute("PLUGIN", "color", "'source' and 'version'", attribute.Position));
    }

    [Fact]
    public void Assemble_NonStringVersion_IsAnError()
    {
        var document = Doc("PLUGIN pg ( source = 'NSchema.Postgres', version = 5 );");

        var attribute = document.Statements.Single().Attributes.Single(a => a.Key == "version");
        ConfigAssembler.Assemble([document]).Errors.ShouldHaveSingleItem()
            .ShouldBe(ConfigDiagnostics.AttributeMustBeString("PLUGIN", "version", attribute.Position));
    }

    [Fact]
    public void Assemble_InvalidVersionRange_IsAnError()
    {
        var document = Doc("PLUGIN pg ( source = 'NSchema.Postgres', version = 'banana' );");

        var attribute = document.Statements.Single().Attributes.Single(a => a.Key == "version");
        ConfigAssembler.Assemble([document]).Errors.ShouldHaveSingleItem()
            .ShouldBe(ConfigDiagnostics.InvalidVersionRange("banana", attribute.Position));
    }

    [Fact]
    public void Assemble_InvalidSource_IsAnError()
    {
        var document = Doc("PLUGIN pg ( source = 'not a package', version = '5.0.1' );");

        var attribute = document.Statements.Single().Attributes.Single(a => a.Key == "source");
        ConfigAssembler.Assemble([document]).Errors.ShouldHaveSingleItem()
            .ShouldBe(ConfigDiagnostics.InvalidPackageId("not a package", attribute.Position));
    }

    [Fact]
    public void Assemble_MissingEngineVersion_IsAnError()
    {
        var document = Doc("ENGINE ();");

        ConfigAssembler.Assemble([document]).Errors.ShouldHaveSingleItem()
            .ShouldBe(ConfigDiagnostics.RequiredAttribute("ENGINE", "version", document.Statements.Single().Position));
    }

    [Fact]
    public void Assemble_InvalidStatement_IsExcludedFromTheDefinition()
    {
        // Arrange
        var document = Doc("PLUGIN pg ( source = 'NSchema.Postgres', version = 'banana' );");

        // Act
        var result = ConfigAssembler.Assemble([document]);

        // Assert — the failure still carries the best-effort definition.
        result.IsFailure.ShouldBeTrue();
        result.Value!.Plugins.ShouldBeEmpty();
    }

    [Fact]
    public void Assemble_StampsTheDocumentFile()
    {
        var document = Doc("DATABASE pg ( host = 'localhost' );", "config.env.prod.sql");

        ConfigAssembler.Assemble([document]).Errors.ShouldHaveSingleItem().File.ShouldBe("config.env.prod.sql");
    }
}
