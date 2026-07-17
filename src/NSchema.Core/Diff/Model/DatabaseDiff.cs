using NSchema.Diff.Model.Constraints;
using NSchema.Diff.Model.Extensions;
using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Services;
using NSchema.Diff.Model.Tables;
using NSchema.Diff.Model.Views;
using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.Model.Services;

namespace NSchema.Diff.Model;

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

    /// <summary>
    /// Restricts the diff to <paramref name="scope"/>, adding back what its removals sever beyond it.
    /// </summary>
    /// <param name="scope">The schemas in play.</param>
    /// <param name="current">The whole current database — the closure has to see past the scope to be right.</param>
    public Result<DatabaseDiff> ScopedTo(PlanningScope scope, Database current)
    {
        if (scope.IsUnscoped)
        {
            return this;
        }

        var narrowed = this with { Schemas = [.. Schemas.Where(s => scope.Includes(s.Name))] };

        var graph = new DependencyGraph(current);
        var removals = Removals(narrowed).ToList();

        var severed = graph.AllDependentsOf(removals).Where(OutOfScope).ToList();
        if (severed.Count == 0)
        {
            return Result.Success(narrowed);
        }

        // Everything reached without believing a guess is asserted; the remainder is hedged, because an edge
        // scanned out of a view body can be wrong, and here a wrong edge removes something that need not go.
        var stated = graph.StatedDependentsOf(removals).Where(OutOfScope).ToHashSet();
        var inferred = severed.Where(n => !stated.Contains(n)).ToList();

        List<Diagnostic> diagnostics = [];
        if (stated.Count > 0)
        {
            diagnostics.Add(DiffDiagnostics.SeveredOutOfScope(stated.Select(n => n.Address)));
        }
        if (inferred.Count > 0)
        {
            diagnostics.Add(DiffDiagnostics.InferredSeveredOutOfScope(inferred.Select(n => n.Address)));
        }

        return Result.From(Widen(narrowed, severed), diagnostics);

        bool OutOfScope(DependencyNode node) => node.Address.SchemaName is { } schema && !scope.Includes(schema);
    }

    /// <summary>
    /// The nodes <paramref name="diff"/> removes — the seeds of what its removals cost.
    /// </summary>
    private static IEnumerable<DependencyNode> Removals(DatabaseDiff diff)
    {
        foreach (var schema in diff.Schemas)
        {
            foreach (var table in schema.Tables.Where(t => t.Kind == ChangeKind.Remove))
            {
                yield return new DependencyNode(new ObjectAddress(schema.Name, table.Name), DependencyKind.Table);
            }

            foreach (var view in schema.Views.Where(v => v.Kind == ChangeKind.Remove))
            {
                yield return new DependencyNode(new ObjectAddress(schema.Name, view.Name), DependencyKind.View);
            }
        }
    }

    /// <summary>
    /// Folds the severed nodes back in as changes to the schemas they live in.
    /// </summary>
    /// <remarks>
    /// A severed schema is only ever touched, never dropped, so its <see cref="SchemaDiff.Kind"/> stays null:
    /// the run is not about this schema, it just cannot avoid it.
    /// </remarks>
    private static DatabaseDiff Widen(DatabaseDiff diff, IReadOnlyList<DependencyNode> severed)
    {
        var added = severed
            .Where(n => n.Address.SchemaName is not null)
            .GroupBy(n => n.Address.SchemaName!)
            .Select(bySchema => new SchemaDiff(
                bySchema.Key,
                Tables: [.. SeveredTables(bySchema)],
                Views: [.. bySchema
                    .Where(n => n.Kind == DependencyKind.View)
                    .Select(n => new ViewDiff(bySchema.Key, ((ObjectAddress)n.Address).Name, ChangeKind.Remove))
                    .OrderBy(v => v.Name)]));

        return diff with { Schemas = [.. diff.Schemas, .. added] };
    }

    /// <summary>
    /// Rebuilds each disturbed table as a modification carrying only the constraints that must go.
    /// </summary>
    private static IEnumerable<TableDiff> SeveredTables(IEnumerable<DependencyNode> severed) => severed
        .Where(n => n.Kind == DependencyKind.ForeignKey)
        .Select(n => (MemberAddress)n.Address)
        .GroupBy(a => (a.Schema, a.Object))
        .Select(byTable => new TableDiff(
            byTable.Key.Schema,
            byTable.Key.Object,
            ChangeKind.Modify,
            ForeignKeys: [.. byTable.Select(a => new ForeignKeyDiff(ChangeKind.Remove, a.Member)).OrderBy(f => f.Name)]))
        .OrderBy(t => t.Name);
}
