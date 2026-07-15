using NSchema.Diff.Domain.Models.Extensions;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Model.Scripts;

namespace NSchema.Diff.Domain.Models;

/// <summary>
/// The complete difference between the current and desired states.
/// </summary>
/// <param name="Schemas">The changed schemas.</param>
/// <param name="Extensions">The changed database-global extensions.</param>
public sealed record DatabaseDiff(IReadOnlyList<SchemaDiff>? Schemas = null, IReadOnlyList<ExtensionDiff>? Extensions = null)
{
    /// <summary>
    /// The changed schemas.
    /// </summary>
    public IReadOnlyList<SchemaDiff> Schemas { get; init; } = Schemas ?? [];

    /// <summary>
    /// The changed database-global extensions.
    /// </summary>
    public IReadOnlyList<ExtensionDiff> Extensions { get; init; } = Extensions ?? [];

    /// <summary>
    /// The deployment scripts to run, in declaration order.
    /// </summary>
    public IReadOnlyList<DeploymentScript> DeploymentScripts { get; init; } = [];

    /// <summary>
    /// The change-event scripts attached to the diff's nodes, in walk order.
    /// </summary>
    public IEnumerable<ChangeScript> ChangeScripts()
    {
        foreach (var schema in Schemas)
        {
            foreach (var table in schema.Tables)
            {
                foreach (var column in table.Columns)
                {
                    if (column.MigrationScript is { } s)
                    {
                        yield return s;
                    }
                }
                foreach (var pk in table.PrimaryKey)
                {
                    if (pk.MigrationScript is { } s)
                    {
                        yield return s;
                    }
                }
                foreach (var fk in table.ForeignKeys)
                {
                    if (fk.MigrationScript is { } s)
                    {
                        yield return s;
                    }
                }
                foreach (var uq in table.UniqueConstraints)
                {
                    if (uq.MigrationScript is { } s)
                    {
                        yield return s;
                    }
                }
                foreach (var ck in table.Checks)
                {
                    if (ck.MigrationScript is { } s)
                    {
                        yield return s;
                    }
                }
                foreach (var ex in table.ExclusionConstraints)
                {
                    if (ex.MigrationScript is { } s)
                    {
                        yield return s;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the diff contains no changes at all.
    /// </summary>
    public bool IsEmpty => Schemas.Count == 0 && Extensions.Count == 0 && DeploymentScripts.Count == 0;

    /// <summary>
    /// Gets the aggregate counts of every changed element, grouped by <see cref="ChangeKind"/>.
    /// </summary>
    public DiffSummary GetSummary()
    {
        var added = 0;
        var modified = 0;
        var removed = 0;

        foreach (var extension in Extensions)
        {
            Tally(extension.Kind);
        }

        foreach (var schema in Schemas)
        {
            if (schema.Kind is { } kind)
            {
                Tally(kind);
            }

            // Every changed object counts once, regardless of kind; a table's members then count individually.
            foreach (var obj in schema.EnumerateObjects())
            {
                Tally(obj.Kind);
            }

            foreach (var member in schema.Tables.SelectMany(t => t.EnumerateMembers()))
            {
                Tally(member.Kind);
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
