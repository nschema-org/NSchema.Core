using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Plan.Model;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Tests.Hosting;

public sealed class DefaultSqlGeneratorResolverTests
{
    /// <summary>A no-op generator that only carries a dialect, for resolution tests.</summary>
    private sealed class StubGenerator(string dialect) : ISqlGenerator
    {
        public string Dialect => dialect;
        public SqlPlan Generate(MigrationPlan plan) => new([]);
    }

    private static DefaultSqlGeneratorResolver Resolver(string? dialect, params ISqlGenerator[] generators)
        => new(Options.Create(new MigrationRunOptions { Dialect = dialect }), generators);

    // -------------------------------------------------------------------------
    // ForDialect / TryForDialect
    // -------------------------------------------------------------------------

    [Fact]
    public void ForDialect_ReturnsRegisteredGenerator()
    {
        var postgres = new StubGenerator("postgres");
        var sut = Resolver(null, postgres, new StubGenerator("mysql"));

        sut.ForDialect("postgres").ShouldBeSameAs(postgres);
    }

    [Fact]
    public void ForDialect_IsCaseInsensitive()
    {
        var postgres = new StubGenerator("postgres");
        var sut = Resolver(null, postgres);

        sut.ForDialect("POSTGRES").ShouldBeSameAs(postgres);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForDialect_RejectsMissingDialect(string? dialect)
        => Should.Throw<ArgumentException>(() => Resolver(null, new StubGenerator("postgres")).ForDialect(dialect!));

    [Fact]
    public void Constructor_Throws_OnDuplicateDialect()
    {
        var ex = Should.Throw<InvalidOperationException>(
            () => Resolver(null, new StubGenerator("postgres"), new StubGenerator("POSTGRES")));

        ex.Message.ShouldContain("postgres");
    }

    [Fact]
    public void TryForDialect_ReturnsFalse_WhenUnregistered()
    {
        var sut = Resolver(null, new StubGenerator("postgres"));

        sut.TryForDialect("mysql", out var generator).ShouldBeFalse();
        generator.ShouldBeNull();
    }

    [Fact]
    public void AvailableDialects_ListsRegisteredDialects()
    {
        var sut = Resolver(null, new StubGenerator("postgres"), new StubGenerator("mysql"));

        sut.AvailableDialects.ShouldBe(["postgres", "mysql"], ignoreOrder: true);
    }

    // -------------------------------------------------------------------------
    // Current
    // -------------------------------------------------------------------------

    [Fact]
    public void Current_IsNull_WhenNoGeneratorsRegistered()
        => Resolver(null).Current.ShouldBeNull();

    [Fact]
    public void Current_ReturnsSingleGenerator_WhenNoDialectConfigured()
    {
        var postgres = new StubGenerator("postgres");

        Resolver(null, postgres).Current.ShouldBeSameAs(postgres);
    }

    [Fact]
    public void Current_Throws_WhenMultipleRegistered_AndNoDialectConfigured()
    {
        var sut = Resolver(null, new StubGenerator("postgres"), new StubGenerator("mysql"));

        var ex = Should.Throw<InvalidOperationException>(() => sut.Current);

        ex.Message.ShouldContain("postgres");
        ex.Message.ShouldContain("mysql");
    }

    [Fact]
    public void Current_SelectsConfiguredDialect_WhenSet()
    {
        var postgres = new StubGenerator("postgres");
        var mysql = new StubGenerator("mysql");

        Resolver("mysql", postgres, mysql).Current.ShouldBeSameAs(mysql);
    }

    [Fact]
    public void Current_Throws_WhenConfiguredDialectNotRegistered()
    {
        var sut = Resolver("sqlite", new StubGenerator("postgres"));

        Should.Throw<InvalidOperationException>(() => sut.Current);
    }
}
