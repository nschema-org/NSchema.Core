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
        public SqlPlan Generate(MigrationPlan plan) => new([]);
    }

    private static ISqlGenerator? Resolve(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        return builder.Build().Services.GetService<ISqlGenerator>();
    }

    [Fact]
    public void Default_RegistersNoGenerator()
    {
        Resolve(_ => { }).ShouldBeNull();
    }

    [Fact]
    public void UseSqlGenerator_RegistersTheGenerator()
    {
        Resolve(b => b.UseSqlGenerator<StubSqlGenerator>()).ShouldBeOfType<StubSqlGenerator>();
    }

    [Fact]
    public void UseSqlGenerator_CalledTwice_KeepsLast()
    {
        Resolve(b => b
            .UseSqlGenerator<StubSqlGenerator>()
            .UseSqlGenerator<MySqlStubGenerator>())
            .ShouldBeOfType<MySqlStubGenerator>();
    }
}
