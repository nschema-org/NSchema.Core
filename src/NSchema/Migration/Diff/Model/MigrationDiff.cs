namespace NSchema.Migration.Diff.Model;

/// <summary>
/// A structured, hierarchical view of the changes between two schemas, arranged as schemas
/// containing tables containing columns, indexes, constraints and grants. This is produced directly by
/// <see cref="Migration.ISchemaComparer"/>, consumed by <see cref="IDiffRenderer"/> for display, and
/// linearized into an executable <see cref="Plan.MigrationPlan"/> by <see cref="Migration.IMigrationLinearizer"/>.
/// </summary>
/// <param name="Schemas">The changed schemas, ordered by name.</param>
/// <param name="PreDeploymentScripts">Names of pre-deployment scripts to run, in plan order.</param>
/// <param name="PostDeploymentScripts">Names of post-deployment scripts to run, in plan order.</param>
public sealed record MigrationDiff(
    IReadOnlyList<SchemaDiff> Schemas,
    IReadOnlyList<string> PreDeploymentScripts,
    IReadOnlyList<string> PostDeploymentScripts)
{
    /// <summary>
    /// Gets a value indicating whether the diff contains no changes at all.
    /// </summary>
    public bool IsEmpty => Schemas.Count == 0 && PreDeploymentScripts.Count == 0 && PostDeploymentScripts.Count == 0;

    /// <summary>
    /// Gets the aggregate counts of changed schemas and tables, grouped by <see cref="ChangeKind"/>.
    /// </summary>
    public DiffSummary Summary
    {
        get
        {
            var added = 0;
            var modified = 0;
            var removed = 0;

            void Tally(ChangeKind kind)
            {
                switch (kind)
                {
                    case ChangeKind.Add: added++; break;
                    case ChangeKind.Modify: modified++; break;
                    case ChangeKind.Remove: removed++; break;
                }
            }

            foreach (var schema in Schemas)
            {
                if (schema.Kind is { } kind)
                {
                    Tally(kind);
                }

                foreach (var table in schema.Tables)
                {
                    Tally(table.Kind);
                }
            }

            return new DiffSummary(added, modified, removed);
        }
    }
}
