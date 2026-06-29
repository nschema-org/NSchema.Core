using System.Text;
using NSchema.Sql.Model;

namespace NSchema.Sql;

/// <summary>
/// Renders a <see cref="SqlPlan"/> into human-readable text for previewing.
/// Numbers each statement and flags any that run outside the migration transaction.
/// </summary>
public sealed class DefaultSqlPlanRenderer
{
    /// <summary>
    /// A shared, stateless instance of the renderer.
    /// </summary>
    public static DefaultSqlPlanRenderer Default { get; } = new();

    /// <summary>
    /// Renders the SQL plan as text.
    /// </summary>
    public string Render(SqlPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SQL Preview:");

        if (plan.IsEmpty)
        {
            sb.Append("- No statements to execute");
            return sb.ToString();
        }

        for (var i = 0; i < plan.Statements.Count; i++)
        {
            var statement = plan.Statements[i];
            var marker = statement.RunOutsideTransaction ? " (outside transaction)" : string.Empty;
            sb.AppendLine($"-- [{i + 1}/{plan.Statements.Count}]{marker}");
            sb.AppendLine(statement.Sql);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
