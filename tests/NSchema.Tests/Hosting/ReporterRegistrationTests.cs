using Microsoft.Extensions.DependencyInjection;
using NSchema.Diff.Model;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Policies;
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
    public void Default_RegistersHumanReporter()
    {
        var resolver = Build(_ => { }).GetRequiredService<IMigrationReporterResolver>();

        resolver.ForFormat("human").ShouldBeOfType<DefaultMigrationReporter>();
        resolver.AvailableFormats.ShouldContain("human");
    }

    [Fact]
    public void AddReporter_Instance_IsResolvable()
    {
        var json = new StubReporter("json");

        var resolver = Build(b => b.AddReporter(json)).GetRequiredService<IMigrationReporterResolver>();

        resolver.ForFormat("json").ShouldBeSameAs(json);
        resolver.AvailableFormats.ShouldBe(["human", "json"], ignoreOrder: true);
    }

    [Fact]
    public void AddReporter_OverridesBuiltInReporter_ForSameFormat()
    {
        // The built-in 'human' reporter is registered first, so a caller's reporter for the same format,
        // added afterwards, replaces it (last registration wins).
        var human = new StubReporter("human");

        var resolver = Build(b => b.AddReporter(human)).GetRequiredService<IMigrationReporterResolver>();

        resolver.ForFormat("human").ShouldBeSameAs(human);
    }

    [Fact]
    public void Current_SelectsReporterForConfiguredOutputFormat()
    {
        var json = new StubReporter("json");

        var resolver = Build(b => b
            .AddReporter(json)
            .WithOutputFormat("json"))
            .GetRequiredService<IMigrationReporterResolver>();

        resolver.Current.ShouldBeSameAs(json);
    }

    [Fact]
    public void Current_DefaultsToHumanReporter()
    {
        var resolver = Build(_ => { }).GetRequiredService<IMigrationReporterResolver>();

        resolver.Current.ShouldBeOfType<DefaultMigrationReporter>();
    }
}
