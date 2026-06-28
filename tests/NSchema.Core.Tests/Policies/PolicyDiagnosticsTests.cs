using NSchema.Policies;
using NSchema.Diagnostics;

namespace NSchema.Tests.Policies;

public sealed class PolicyDiagnosticsTests
{
    private static Diagnostic Diag(DiagnosticSeverity severity, string message = "msg")
        => new("Policy", message, severity);

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    [Fact]
    public void DefaultConstructor_CreatesEmptyCollection()
    {
        var diagnostics = new PolicyDiagnostics();

        diagnostics.Count.ShouldBe(0);
        diagnostics.HasErrors.ShouldBeFalse();
        diagnostics.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void EnumerableConstructor_CopiesAllDiagnostics()
    {
        var source = new[] { Diag(DiagnosticSeverity.Info), Diag(DiagnosticSeverity.Warning) };

        var diagnostics = new PolicyDiagnostics(source);

        diagnostics.ShouldBe(source);
    }

    [Fact]
    public void EnumerableConstructor_MaterializesLazySource()
    {
        // A lazily-evaluated source (as produced by SelectMany over policies) must be enumerated
        // once into the collection, not re-evaluated on every access.
        var evaluations = 0;
        IEnumerable<Diagnostic> Lazy()
        {
            evaluations++;
            yield return Diag(DiagnosticSeverity.Error);
        }

        var diagnostics = new PolicyDiagnostics(Lazy());
        _ = diagnostics.HasErrors;
        _ = diagnostics.Errors.ToList();

        evaluations.ShouldBe(1);
    }

    // -------------------------------------------------------------------------
    // HasErrors
    // -------------------------------------------------------------------------

    [Fact]
    public void HasErrors_True_WhenAnyErrorSeverityPresent()
    {
        var diagnostics = new PolicyDiagnostics(
        [
            Diag(DiagnosticSeverity.Info),
            Diag(DiagnosticSeverity.Warning),
            Diag(DiagnosticSeverity.Error),
        ]);

        diagnostics.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public void HasErrors_False_WhenOnlyInfoAndWarning()
    {
        var diagnostics = new PolicyDiagnostics(
        [
            Diag(DiagnosticSeverity.Info),
            Diag(DiagnosticSeverity.Warning),
        ]);

        diagnostics.HasErrors.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Errors
    // -------------------------------------------------------------------------

    [Fact]
    public void Errors_ReturnsOnlyErrorSeverity()
    {
        var error1 = Diag(DiagnosticSeverity.Error, "first");
        var error2 = Diag(DiagnosticSeverity.Error, "second");
        var diagnostics = new PolicyDiagnostics(
        [
            Diag(DiagnosticSeverity.Info),
            error1,
            Diag(DiagnosticSeverity.Warning),
            error2,
        ]);

        diagnostics.Errors.ShouldBe([error1, error2]);
    }

    // -------------------------------------------------------------------------
    // Mutability — the planner accumulates into the collection.
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_AfterConstruction_IsReflectedInErrors()
    {
        var diagnostics = new PolicyDiagnostics();

        diagnostics.Add(Diag(DiagnosticSeverity.Error));

        diagnostics.HasErrors.ShouldBeTrue();
        diagnostics.Errors.ShouldHaveSingleItem();
    }
}
