using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;

namespace NSchema.Tests.Hosting;

public sealed class DefaultMigrationReporterTests
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly IMigrationPlanRenderer _planRenderer = Substitute.For<IMigrationPlanRenderer>();

    private readonly DefaultMigrationReporter _sut;

    public DefaultMigrationReporterTests()
    {
        _sut = new DefaultMigrationReporter(_output, _error, _planRenderer);
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
    public void ReportPlan_WritesRenderedPlanToOutput()
    {
        var plan = new MigrationPlan([], DatabaseSchema.Create([]));
        _planRenderer.Render(plan).Returns("rendered diff");

        _sut.ReportPlan(plan);

        _output.ToString().ShouldContain("rendered diff");
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void ReportPreview_WritesEachStatementToOutput()
    {
        _sut.ReportPreview(["CREATE SCHEMA app", "CREATE TABLE app.users (id int)"]);

        var lines = _output.ToString();
        lines.ShouldContain("SQL Preview:");
        lines.ShouldContain("CREATE SCHEMA app");
        lines.ShouldContain("CREATE TABLE app.users (id int)");
    }

    [Fact]
    public void ReportDiagnostics_RoutesWarningsAndErrorsToError()
    {
        var diagnostics = new[]
        {
            new PolicyError("P1", "all good", PolicySeverity.Info),
            new PolicyError("P2", "be careful", PolicySeverity.Warning),
            new PolicyError("P3", "blocked", PolicySeverity.Error),
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
        _sut.ReportDiagnostics([]);

        _output.ToString().ShouldContain("None");
    }
}
