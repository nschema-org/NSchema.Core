namespace NSchema.Plan.Domain;

/// <summary>
/// The diagnostics minted by the planner.
/// </summary>
internal static class PlanDiagnostics
{
    private const string Source = "plan";

    /// <summary>
    /// Planning without a registered SQL dialect (the plan's statements cannot be rendered).
    /// </summary>
    public static Diagnostic MissingDialect => Diagnostic.Error(Source, "Planning requires a database provider to render SQL, but none is registered.");
}
