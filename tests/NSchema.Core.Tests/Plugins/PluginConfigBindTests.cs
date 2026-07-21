using NSchema.Plugins;
using NSchema.Plugins.Model.Config;

namespace NSchema.Tests.Plugins;

/// <summary>
/// Options binding: a <see cref="PluginConfig"/> binds onto an options object the way IConfiguration binds —
/// snake_case names to properties, dotted keys to nested objects, identifiers to enums — with every finding
/// a diagnostic on the result.
/// </summary>
public sealed class PluginConfigBindTests
{
    private enum TransactionMode { Single, ReadCommitted }

    private sealed class PoolOptions
    {
        public int Max { get; set; } = 8;
    }

    private sealed class DatabaseOptions
    {
        public required string ConnectionString { get; set; }
        public int ConnectionTimeout { get; set; } = 30;
        public bool Ssl { get; set; }
        public TransactionMode TransactionMode { get; set; }
        public TimeSpan? CommandTimeout { get; set; }
        public PoolOptions Pool { get; set; } = new();
    }

    private static PluginConfig Config(params (string Key, ConfigValue Value)[] attributes) =>
        new(null, attributes.ToDictionary(a => new AttributeKey(a.Key), a => a.Value));

    [Fact]
    public void Bind_MapsSnakeCaseAttributesToProperties()
    {
        // Arrange
        var config = Config(
            ("connection_string", ConfigValue.OfString("Host=localhost")),
            ("connection_timeout", ConfigValue.OfInteger(60)),
            ("ssl", ConfigValue.OfBoolean(true)));

        // Act
        var result = config.Bind<DatabaseOptions>();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ConnectionString.ShouldBe("Host=localhost");
        result.Value.ConnectionTimeout.ShouldBe(60);
        result.Value.Ssl.ShouldBeTrue();
    }

    [Fact]
    public void Bind_UnsetAttributes_KeepTheirDefaults()
        => Config(("connection_string", ConfigValue.OfString("x"))).Bind<DatabaseOptions>()
            .Value!.ConnectionTimeout.ShouldBe(30);

    [Fact]
    public void Bind_IdentifierValue_BindsEnum_UnderTheSameNamingConvention()
        => Config(("connection_string", ConfigValue.OfString("x")), ("transaction_mode", ConfigValue.OfIdentifier("read_committed")))
            .Bind<DatabaseOptions>().Value!.TransactionMode.ShouldBe(TransactionMode.ReadCommitted);

    [Fact]
    public void Bind_StringValue_ConvertsThroughTypeConverters()
        => Config(("connection_string", ConfigValue.OfString("x")), ("command_timeout", ConfigValue.OfString("00:00:30")))
            .Bind<DatabaseOptions>().Value!.CommandTimeout.ShouldBe(TimeSpan.FromSeconds(30));

    [Fact]
    public void Bind_DottedKey_BindsNestedOptions()
        => Config(("connection_string", ConfigValue.OfString("x")), ("pool.max", ConfigValue.OfInteger(20)))
            .Bind<DatabaseOptions>().Value!.Pool.Max.ShouldBe(20);

    [Fact]
    public void Bind_MissingRequiredAttribute_IsAnError()
    {
        // Act
        var result = Config(("ssl", ConfigValue.OfBoolean(true))).Bind<DatabaseOptions>();

        // Assert
        result.Errors.ShouldHaveSingleItem()
            .ShouldBe(PluginDiagnostics.MissingRequiredOption("connection_string", typeof(DatabaseOptions)));
    }

    [Fact]
    public void Bind_UnknownAttribute_IsAnError()
    {
        // Act — a typo'd attribute matches no property.
        var result = Config(("connection_string", ConfigValue.OfString("x")), ("connection_sting", ConfigValue.OfString("y")))
            .Bind<DatabaseOptions>();

        // Assert
        result.Errors.ShouldHaveSingleItem()
            .ShouldBe(PluginDiagnostics.UnknownOption("connection_sting", typeof(DatabaseOptions)));
    }

    [Fact]
    public void Bind_ValueThatDoesNotFit_IsAnError()
    {
        // Act — an integer cannot bind a bool property.
        var result = Config(("connection_string", ConfigValue.OfString("x")), ("ssl", ConfigValue.OfInteger(1)))
            .Bind<DatabaseOptions>();

        // Assert
        result.Errors.ShouldHaveSingleItem()
            .ShouldBe(PluginDiagnostics.UnbindableOption("ssl", ConfigValueKind.Integer, typeof(bool)));
    }

    [Fact]
    public void Bind_IntegerOverflow_IsAnError()
        => Config(("connection_string", ConfigValue.OfString("x")), ("connection_timeout", ConfigValue.OfInteger(5_000_000_000)))
            .Bind<DatabaseOptions>().Errors.ShouldHaveSingleItem()
            .ShouldBe(PluginDiagnostics.UnbindableOption("connection_timeout", ConfigValueKind.Integer, typeof(int)));

    [Fact]
    public void Bind_Failure_StillCarriesTheBestEffortInstance()
    {
        // Act
        var result = Config(("ssl", ConfigValue.OfBoolean(true))).Bind<DatabaseOptions>();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value!.Ssl.ShouldBeTrue();
    }
}
