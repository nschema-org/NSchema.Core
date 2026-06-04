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
        public void Error(string message) { }
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
    public void AddReporter_OverridingFormat_LastWins()
    {
        var replacement = new StubReporter("human");

        var resolver = Build(b => b.AddReporter(replacement)).GetRequiredService<IMigrationReporterResolver>();

        // The user's later registration shadows the built-in human reporter.
        resolver.ForFormat("human").ShouldBeSameAs(replacement);
    }

    [Fact]
    public void ConsumerFacingReporter_IsTheOneSelectedByOutputFormat()
    {
        var json = new StubReporter("json");

        var services = Build(b => b
            .AddReporter(json)
            .WithOutputFormat("json"));

        // Whatever a consumer injects is the reporter chosen for the configured output format.
        services.GetRequiredService<IMigrationReporter>().ShouldBeSameAs(json);
    }

    [Fact]
    public void ConsumerFacingReporter_DefaultsToHumanReporter()
    {
        var reporter = Build(_ => { }).GetRequiredService<IMigrationReporter>();

        reporter.ShouldBeOfType<DefaultMigrationReporter>();
    }
}
