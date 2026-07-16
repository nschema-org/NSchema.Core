using Microsoft.Extensions.DependencyInjection;
using NSchema.Plan.Backends;
using NSchema.Plan.Model;

namespace NSchema.Tests.Hosting;

public sealed class SqlDialectRegistrationTests
{
    private sealed class MyStubDialect : ISqlDialect
    {
        public IReadOnlyList<SqlStatement> Generate(MigrationAction action) => [];
    }

    private static ISqlDialect? Resolve(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        return builder.Build().Services.GetService<ISqlDialect>();
    }

    [Fact]
    public void Default_RegistersNoDialect()
    {
        Resolve(_ => { }).ShouldBeNull();
    }

    [Fact]
    public void UseSqlDialect_RegistersTheDialect()
    {
        Resolve(b => b.UseSqlDialect<StubSqlDialect>()).ShouldBeOfType<StubSqlDialect>();
    }

    [Fact]
    public void UseSqlDialect_CalledTwice_KeepsLast()
    {
        Resolve(b => b
            .UseSqlDialect<StubSqlDialect>()
            .UseSqlDialect<MyStubDialect>())
            .ShouldBeOfType<MyStubDialect>();
    }
}
