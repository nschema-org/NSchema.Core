using NSchema.Plan.Model;
using NSchema.Sql.Model;

namespace NSchema.Sql;

/// <summary>
/// The SQL dialect a provider plugs in.
/// </summary>
public interface ISqlDialect
{
    /// <summary>
    /// Renders <paramref name="action"/> as the SQL statement(s) that perform it.
    /// </summary>
    /// <param name="action">The migration action to render.</param>
    /// <returns>The ordered statements performing the action.</returns>
    IReadOnlyList<SqlStatement> Generate(MigrationAction action);
}
