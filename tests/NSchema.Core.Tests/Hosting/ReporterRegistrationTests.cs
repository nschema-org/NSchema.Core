using Microsoft.Extensions.DependencyInjection;
using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema.Model;
using NSchema.Sql.Model;

namespace NSchema.Tests.Hosting;

public sealed class ReporterRegistrationTests
{
    /// <summary>A no-op reporter, for registration tests.</summary>
    private sealed class StubReporter : IOperationReporter
    {
        public void Report(MessageKind kind, string message) { }
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
        Build(_ => { }).GetRequiredService<IOperationReporter>().ShouldBeOfType<DefaultOperationReporter>();
    }

    [Fact]
    public void UseReporter_Instance_ReplacesDefault()
    {
        var json = new StubReporter();

        Build(b => b.UseReporter(json)).GetRequiredService<IOperationReporter>().ShouldBeSameAs(json);
    }

    [Fact]
    public void UseReporter_Type_ReplacesDefault()
    {
        Build(b => b.UseReporter<StubReporter>()).GetRequiredService<IOperationReporter>().ShouldBeOfType<StubReporter>();
    }

    [Fact]
    public void UseReporter_CalledTwice_KeepsLast()
    {
        var first = new StubReporter();
        var second = new StubReporter();

        Build(b => b.UseReporter(first).UseReporter(second)).GetRequiredService<IOperationReporter>().ShouldBeSameAs(second);
    }
}
