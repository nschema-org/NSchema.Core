using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Scripts.Model;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Tests.Hosting;

public sealed class DefaultOperationReporterTests
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly IDiffRenderer _diffRenderer = Substitute.For<IDiffRenderer>();
    private readonly ISchemaRenderer _schemaRenderer = Substitute.For<ISchemaRenderer>();
    private readonly ISqlPlanRenderer _sqlPlanRenderer = Substitute.For<ISqlPlanRenderer>();

    private readonly DefaultOperationReporter _sut;

    public DefaultOperationReporterTests()
    {
        _sut = new DefaultOperationReporter(_diffRenderer, _schemaRenderer, _sqlPlanRenderer, _output, _error);
    }

    [Fact]
    public void Info_WritesToOutput()
    {
        _sut.Info("hello");

        _output.ToString().ShouldBe("hello" + Environment.NewLine);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void ReportException_WritesToError()
    {
        _sut.ReportException(new InvalidOperationException("boom"));

        _error.ToString().ShouldBe("Operation failed: boom" + Environment.NewLine);
        _output.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void ReportException_PolicyViolationException_WritesDetailsToError()
    {
        // Arrange
        var diagnostics = new PolicyDiagnostics
        {
            new PolicyDiagnostic("P1", "all good", PolicyDiagnosticSeverity.Info),
            new PolicyDiagnostic("P2", "be careful", PolicyDiagnosticSeverity.Warning),
            new PolicyDiagnostic("P3", "blocked", PolicyDiagnosticSeverity.Error),
        };
        var exception = new PolicyViolationException(diagnostics);

        // Act
        _sut.ReportException(exception);

        // Assert
        _error.ToString().ShouldContain("P1: all good");
        _error.ToString().ShouldContain("P2: be careful");
        _error.ToString().ShouldContain("P3: blocked");
        _error.ToString().ShouldContain("Policy violated with 3 error(s).");
        _output.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void ReportDiff_WritesRenderedDiffToOutput()
    {
        var diff = new DatabaseDiff([]);
        _diffRenderer.Render(diff).Returns("rendered diff");

        _sut.ReportDiff(diff);

        _output.ToString().ShouldContain("rendered diff");
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void ReportSchema_WritesRenderedSchemaToOutput()
    {
        var schema = new DatabaseSchema();
        _schemaRenderer.Render(schema).Returns("rendered schema");

        _sut.ReportSchema(schema);

        _output.ToString().ShouldContain("rendered schema");
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void ReportPlan_WritesDeploymentScriptNamesToOutput()
    {
        var plan = new MigrationPlan(
            [],
            [new Script("0001_pre", "SELECT 1", ScriptType.PreDeployment)],
            [new Script("0001_post", "SELECT 2", ScriptType.PostDeployment)]
        );

        _sut.ReportPlan(plan);

        var output = _output.ToString();
        output.ShouldContain("Pre-deployment scripts:");
        output.ShouldContain("  - 0001_pre");
        output.ShouldContain("Post-deployment scripts:");
        output.ShouldContain("  - 0001_post");
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void ReportPlan_WithNoScripts_WritesNothing()
    {
        _sut.ReportPlan(new MigrationPlan([new CreateSchema("app")], [], []));

        _output.ToString().ShouldBeEmpty();
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
    public void ReportDiagnostics_RoutesToOutput()
    {
        var diagnostics = new PolicyDiagnostics
        {
            new PolicyDiagnostic("P1", "all good", PolicyDiagnosticSeverity.Info),
            new PolicyDiagnostic("P2", "be careful", PolicyDiagnosticSeverity.Warning),
            new PolicyDiagnostic("P3", "blocked", PolicyDiagnosticSeverity.Error),
        };

        _sut.ReportDiagnostics(diagnostics);

        _output.ToString().ShouldContain("P1: all good");
        _output.ToString().ShouldContain("P2: be careful");
        _output.ToString().ShouldContain("P3: blocked");
    }

    [Fact]
    public void ReportDiagnostics_WithNoDiagnostics_WritesNone()
    {
        _sut.ReportDiagnostics([]);

        _output.ToString().ShouldContain("Nothing to report");
    }
}
