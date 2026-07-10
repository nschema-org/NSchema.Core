using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Plan;

internal static class SqlGeneratorExtensions
{
    extension(ISqlGenerator generator)
    {
        /// <summary>
        /// Generates the complete executable <see cref="SqlPlan"/> for a planned migration.
        /// </summary>
        /// <remarks>
        /// In the next major the provider contract shrinks to producing statements, and constructing the
        /// executable artifact becomes exclusively this seam's job.
        /// </remarks>
        public SqlPlan Generate(PlannedMigration planned) => generator.Generate(planned.Plan) with { Scripts = planned.Scripts };
    }
}
