using Microsoft.Extensions.DependencyInjection;
using NSchema.Plan.Model;
using NSchema.Resolution;
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

    private static IKeyedResolver<ISqlGenerator> Resolve(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        return builder.Build().Services.GetRequiredService<IKeyedResolver<ISqlGenerator>>();
    }

    [Fact]
    public void Default_HasNoCurrentGenerator()
    {
        var resolver = Resolve(_ => { });

        resolver.HasCurrent.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => resolver.Current);
    }

    [Fact]
    public void AddSqlGenerator_IsResolvableByKey()
    {
        var resolver = Resolve(b => b.AddSqlGenerator<StubSqlGenerator>(StubSqlGenerator.DialectName));

        resolver.Resolve(StubSqlGenerator.DialectName).ShouldBeOfType<StubSqlGenerator>();
    }

    [Fact]
    public void WithDialect_SetsCurrent()
    {
        var resolver = Resolve(b => b
            .AddSqlGenerator<StubSqlGenerator>(StubSqlGenerator.DialectName)
            .WithDialect(StubSqlGenerator.DialectName));

        resolver.HasCurrent.ShouldBeTrue();
        resolver.Current.ShouldBeOfType<StubSqlGenerator>();
    }

    [Fact]
    public void WithDialect_SelectsAmongMultipleGenerators()
    {
        var resolver = Resolve(b => b
            .AddSqlGenerator<StubSqlGenerator>(StubSqlGenerator.DialectName)
            .AddSqlGenerator<MySqlStubGenerator>("mysql")
            .WithDialect("mysql"));

        resolver.Current.ShouldBeOfType<MySqlStubGenerator>();
    }

    [Fact]
    public void WithoutDialect_MultipleGenerators_HasNoCurrentGenerator()
    {
        var resolver = Resolve(b => b
            .AddSqlGenerator<StubSqlGenerator>(StubSqlGenerator.DialectName)
            .AddSqlGenerator<MySqlStubGenerator>("mysql"));

        resolver.HasCurrent.ShouldBeFalse();
    }
}
