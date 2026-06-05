using NSchema.Plan.Model;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Tests.Helpers;

/// <summary>Deterministic stand-in for a dialect: one SQL statement per migration action.</summary>
internal sealed class StubSqlGenerator : ISqlGenerator
{
    public const string DialectName = "stub";

    public string Dialect => DialectName;

    public SqlPlan Generate(MigrationPlan plan)
        => new([.. plan.Actions.Select(a => new SqlStatement($"-- {a.GetType().Name}"))]);
}
