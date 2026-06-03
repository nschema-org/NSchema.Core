namespace NSchema.Sql;

/// <summary>
/// Renders a <see cref="SqlPlan"/> into human-readable text for previewing.
/// </summary>
public interface ISqlPlanRenderer
{
    /// <summary>
    /// Renders the SQL plan as text.
    /// </summary>
    /// <param name="plan">The SQL plan to render.</param>
    /// <returns>The rendered preview.</returns>
    string Render(SqlPlan plan);
}
