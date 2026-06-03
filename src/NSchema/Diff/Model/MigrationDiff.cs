using NSchema.Schema.Model;

namespace NSchema.Diff.Model;

/// <summary>
/// A representation of the changes between two schemas.
/// </summary>
/// <param name="Schemas">The changed schemas.</param>
/// <param name="PreDeploymentScripts">Names of pre-deployment scripts to run.</param>
/// <param name="PostDeploymentScripts">Names of post-deployment scripts to run.</param>
public sealed record MigrationDiff(
    IReadOnlyList<SchemaDiff> Schemas,
    IReadOnlyList<Script> PreDeploymentScripts,
    IReadOnlyList<Script> PostDeploymentScripts
)
{
    /// <summary>
    /// Gets a value indicating whether the diff contains no changes at all.
    /// </summary>
    public bool IsEmpty => Schemas.Count == 0 && PreDeploymentScripts.Count == 0 && PostDeploymentScripts.Count == 0;

    /// <summary>
    /// Gets the aggregate counts of changed schemas and tables, grouped by <see cref="ChangeKind"/>.
    /// </summary>
    public DiffSummary GetSummary()
    {
        var added = 0;
        var modified = 0;
        var removed = 0;

        foreach (var schema in Schemas)
        {
            if (schema.Kind is { } kind)
            {
                Tally(kind);
            }

            foreach (var table in schema.Tables)
            {
                Tally(table.Kind);
                foreach (var column in table.Columns)
                {
                    Tally(column.Kind);
                }

                foreach (var index in table.Indexes)
                {
                    Tally(index.Kind);
                }

                foreach (var constraint in table.Constraints)
                {
                    Tally(constraint.Kind);
                }
            }
        }

        return new DiffSummary(added, modified, removed);

        void Tally(ChangeKind kind)
        {
            switch (kind)
            {
                case ChangeKind.Add: added++; break;
                case ChangeKind.Modify: modified++; break;
                case ChangeKind.Remove: removed++; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }
}
