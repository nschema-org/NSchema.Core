using Microsoft.Extensions.DependencyInjection;
using NSchema.Diff.Model;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Policies;
using NSchema.Resolution;
using NSchema.Sql.Model;

namespace NSchema.Tests.Hosting;

public sealed class ReporterRegistrationTests
{
    /// <summary>A no-op reporter that only carries a format, for registration tests.</summary>
    private sealed class StubReporter(string format) : IMigrationReporter
    {
        public string Format => format;
        public void Info(string message) { }
        public void ReportException(Exception exception) { }
        public void ReportDiff(MigrationDiff diff) { }
        public void ReportSqlPlan(SqlPlan plan) { }
        public void ReportDiagnostics(PolicyDiagnostics diagnostics) { }
    }

    private static IServiceProvider Build(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        return builder.Build().Services;
    }

    [Fact]
    public void Default_RegistersDefaultReporter()
    {
        var resolver = Build(_ => { }).GetRequiredService<IKeyedResolver<IMigrationReporter>>();

        resolver.Resolve(DefaultMigrationReporter.FormatName).ShouldBeOfType<DefaultMigrationReporter>();
        resolver.HasCurrent.ShouldBeTrue();
        resolver.Current.ShouldBeOfType<DefaultMigrationReporter>();
    }

    [Fact]
    public void AddReporter_Instance_IsResolvable()
    {
        var json = new StubReporter("json");

        var resolver = Build(b => b.AddReporter(json)).GetRequiredService<IKeyedResolver<IMigrationReporter>>();

        resolver.Resolve("json").ShouldBeSameAs(json);
    }

    [Fact]
    public void AddReporter_DuplicateFormat_KeepsFirst()
    {
        var first = new StubReporter("json");
        var second = new StubReporter("json");

        var resolver = Build(b => b.AddReporter(first).AddReporter(second)).GetRequiredService<IKeyedResolver<IMigrationReporter>>();

        resolver.Resolve("json").ShouldBeSameAs(first);
    }

    [Fact]
    public void UseReporter_OverridesBuiltInReporter_ForSameFormat()
    {
        var human = new StubReporter("human");

        var resolver = Build(b => b.UseReporter(human)).GetRequiredService<IKeyedResolver<IMigrationReporter>>();

        resolver.Resolve("human").ShouldBeSameAs(human);
    }

    [Fact]
    public void Current_SelectsReporterForConfiguredOutputFormat()
    {
        var json = new StubReporter("json");

        var resolver = Build(b => b
            .AddReporter(json)
            .WithOutputFormat("json"))
            .GetRequiredService<IKeyedResolver<IMigrationReporter>>();

        resolver.Current.ShouldBeSameAs(json);
    }

    [Fact]
    public void Current_DefaultsToHumanReporter()
    {
        var resolver = Build(_ => { }).GetRequiredService<IKeyedResolver<IMigrationReporter>>();

        resolver.Current.ShouldBeOfType<DefaultMigrationReporter>();
    }
}
