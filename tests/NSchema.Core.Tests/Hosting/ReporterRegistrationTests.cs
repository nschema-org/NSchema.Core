using Microsoft.Extensions.DependencyInjection;
using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Resolution;
using NSchema.Schema.Model;
using NSchema.Sql.Model;

namespace NSchema.Tests.Hosting;

public sealed class ReporterRegistrationTests
{
    /// <summary>A no-op reporter that only carries a format, for registration tests.</summary>
    private sealed class StubReporter : IOperationReporter
    {
        public void Info(string message) { }
        public void ReportException(Exception exception) { }
        public void ReportSchema(DatabaseSchema schema) { }
        public void ReportDiff(DatabaseDiff diff) { }
        public void ReportPlan(MigrationPlan plan) { }
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
        var resolver = Build(_ => { }).GetRequiredService<IKeyedResolver<IOperationReporter>>();

        resolver.Resolve(DefaultOperationReporter.ReporterName).ShouldBeOfType<DefaultOperationReporter>();
        resolver.HasCurrent.ShouldBeTrue();
        resolver.Current.ShouldBeOfType<DefaultOperationReporter>();
    }

    [Fact]
    public void AddReporter_Instance_IsResolvable()
    {
        var json = new StubReporter();

        var resolver = Build(b => b.AddReporter("json", json)).GetRequiredService<IKeyedResolver<IOperationReporter>>();

        resolver.Resolve("json").ShouldBeSameAs(json);
    }

    [Fact]
    public void AddReporter_DuplicateFormat_KeepsLast()
    {
        var first = new StubReporter();
        var second = new StubReporter();

        var resolver = Build(b => b.AddReporter("json", first).AddReporter("json", second)).GetRequiredService<IKeyedResolver<IOperationReporter>>();

        resolver.Resolve("json").ShouldBeSameAs(second);
    }

    [Fact]
    public void Current_SelectsReporterForConfiguredOutputFormat()
    {
        var json = new StubReporter();

        var builder = NSchemaApplication.CreateBuilder(new NSchemaApplicationOptions { Reporter = "json" });
        builder.AddReporter("json", json);
        var resolver = builder.Build().Services.GetRequiredService<IKeyedResolver<IOperationReporter>>();

        resolver.Current.ShouldBeSameAs(json);
    }

    [Fact]
    public void Current_DefaultsToHumanReporter()
    {
        var resolver = Build(_ => { }).GetRequiredService<IKeyedResolver<IOperationReporter>>();

        resolver.Current.ShouldBeOfType<DefaultOperationReporter>();
    }
}
