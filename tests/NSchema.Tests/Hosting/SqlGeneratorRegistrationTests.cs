using Microsoft.Extensions.DependencyInjection;
using NSchema.Plan.Model;
using NSchema.Sql;
using NSchema.Sql.Model;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Hosting;

public sealed class SqlGeneratorRegistrationTests
{
    private sealed class MySqlStubGenerator : ISqlGenerator
    {
        public string Dialect => "mysql";
        public SqlPlan Generate(MigrationPlan plan) => new([]);
    }

    private static ISqlGeneratorResolver Resolve(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        return builder.Build().Services.GetRequiredService<ISqlGeneratorResolver>();
    }

    [Fact]
    public void Default_HasNoGenerators()
    {
        var resolver = Resolve(_ => { });

        resolver.AvailableDialects.ShouldBeEmpty();
        resolver.Current.ShouldBeNull();
    }

    [Fact]
    public void UseSqlGenerator_RegistersResolvableGenerator()
    {
        var resolver = Resolve(b => b.AddSqlGenerator<StubSqlGenerator>());

        resolver.AvailableDialects.ShouldBe([StubSqlGenerator.DialectName]);
        resolver.Current.ShouldBeOfType<StubSqlGenerator>();
    }

    [Fact]
    public void WithDialect_SelectsAmongMultipleGenerators()
    {
        var resolver = Resolve(b => b
            .AddSqlGenerator<StubSqlGenerator>()
            .AddSqlGenerator<MySqlStubGenerator>()
            .WithDialect("mysql"));

        resolver.Current.ShouldBeOfType<MySqlStubGenerator>();
    }

    [Fact]
    public void MultipleGenerators_WithoutDialect_ThrowsOnCurrent()
    {
        var resolver = Resolve(b => b
            .AddSqlGenerator<StubSqlGenerator>()
            .AddSqlGenerator<MySqlStubGenerator>());

        Should.Throw<InvalidOperationException>(() => resolver.Current);
    }
}
