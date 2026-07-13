using NSchema.Project.Domain.Models;
using NSchema.Diff.Domain.Models.Extensions;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Diff.Domain.Models;

/// <summary>
/// The complete difference between the current and desired states.
/// </summary>
/// <param name="Schemas">The changed schemas.</param>
/// <param name="Extensions">The changed database-global extensions.</param>
public sealed record DatabaseDiff(
    IReadOnlyList<SchemaDiff>? Schemas = null,
    IReadOnlyList<ExtensionDiff>? Extensions = null
)
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
    /// The scripts that need to be run, in declaration order.
    /// </summary>
    public IReadOnlyList<Script> Scripts { get; init; } = [];

    /// <summary>
    /// Resolves a script on <see cref="Scripts"/> by name.
    /// </summary>
    public Script? FindScript(SqlIdentifier name) => Scripts.FirstOrDefault(s => s.Name == name);

    /// <summary>
    /// Gets a value indicating whether the diff contains no changes at all — no schema changes and no script runs.
    /// </summary>
    public bool IsEmpty => Schemas.Count == 0 && Extensions.Count == 0 && Scripts.Count == 0;

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
