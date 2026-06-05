using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Hosting;
using NSchema.Policies;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Tests.Hosting;

public sealed class DefaultMigrationReporterTests
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly IDiffRenderer _diffRenderer = Substitute.For<IDiffRenderer>();
    private readonly ISqlPlanRenderer _sqlPlanRenderer = Substitute.For<ISqlPlanRenderer>();

    private readonly DefaultMigrationReporter _sut;

    public DefaultMigrationReporterTests()
    {
        _sut = new DefaultMigrationReporter(_diffRenderer, _sqlPlanRenderer, _output, _error);
    }

    [Fact]
    public void Info_WritesToOutput()
    {
        _sut.Info("hello");

        _output.ToString().ShouldBe("hello" + Environment.NewLine);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Error_WritesToError()
    {
        _sut.Error("boom");

        _error.ToString().ShouldBe("boom" + Environment.NewLine);
        _output.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void ReportDiff_WritesRenderedDiffToOutput()
    {
        var diff = new MigrationDiff([], [], []);
        _diffRenderer.Render(diff).Returns("rendered diff");

        _sut.ReportDiff(diff);

        _output.ToString().ShouldContain("rendered diff");
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void ReportSqlPlan_WritesRenderedPlanToOutput()
    {
        var plan = new SqlPlan([new SqlStatement("CREATE SCHEMA app")]);
        _sqlPlanRenderer.Render(plan).Returns("rendered sql");

        _sut.ReportSqlPlan(plan);

        _output.ToString().ShouldContain("rendered sql");
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void ReportDiagnostics_RoutesWarningsAndErrorsToError()
    {
        var diagnostics = new PolicyDiagnostics
        {
            new PolicyDiagnostic("P1", "all good", PolicyDiagnosticSeverity.Info),
            new PolicyDiagnostic("P2", "be careful", PolicyDiagnosticSeverity.Warning),
            new PolicyDiagnostic("P3", "blocked", PolicyDiagnosticSeverity.Error),
        };

        _sut.ReportDiagnostics(diagnostics);

        _output.ToString().ShouldContain("P1: all good");
        _error.ToString().ShouldContain("P2: be careful");
        _error.ToString().ShouldContain("P3: blocked");
        _output.ToString().ShouldNotContain("P3: blocked");
    }

    [Fact]
    public void ReportDiagnostics_WithNoDiagnostics_WritesNone()
    {
        _sut.ReportDiagnostics(new PolicyDiagnostics());

        _output.ToString().ShouldContain("Nothing to report");
    }
}
