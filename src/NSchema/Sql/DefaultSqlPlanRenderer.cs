using System.Text;
using NSchema.Sql.Model;

namespace NSchema.Sql;

/// <summary>
/// Default <see cref="ISqlPlanRenderer"/>.
/// Numbers each statement and flags any that run outside the migration transaction.
/// </summary>
internal sealed class DefaultSqlPlanRenderer : ISqlPlanRenderer
{
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
        }

        return sb.ToString().TrimEnd();
    }
}
