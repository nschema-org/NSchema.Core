using NSchema.Diff.Model.CompositeTypes;
using NSchema.Diff.Model.Constraints;
using NSchema.Diff.Model.Domains;
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
    public IEnumerable<ChangeScript> ChangeScripts() => Schemas
        .SelectMany(schema => schema.Tables)
        .SelectMany(table => table.EnumerateMembers())
        .OfType<IMigratableDiff>()
        .Select(member => member.MigrationScript)
        .OfType<ChangeScript>();

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
    /// Restricts the diff to <paramref name="scope"/>, adding back what its removals sever beyond it. A removal
    /// only a data-bearing column depends on cannot be widened over and fails the result instead.
    /// </summary>
    /// <param name="scope">The schemas in play.</param>
    /// <param name="current">The whole current database — the closure has to see past the scope to be right.</param>
    public Result<DatabaseDiff> ScopedTo(PlanningScope scope, Database current)
    {
        if (scope.IsUnscoped)
        {
            return this;
        }

        var narrowed = this with { Schemas = [.. Schemas.Select(s => s.ScopedTo(scope)).OfType<SchemaDiff>()] };

        var graph = new DependencyGraph(current);
        var removals = Removals(narrowed).ToList();

        var severed = graph.AllDependentsOf(removals).Where(OutOfScope).ToList();
        if (severed.Count == 0)
        {
            return Result.Success(narrowed);
        }

        // Everything reached without believing a guess is asserted; the remainder is hedged, because an edge
        // scanned out of a view body or bound by a bare type name can be wrong, and here a wrong edge removes
        // something that need not go.
        var stated = graph.StatedDependentsOf(removals).Where(OutOfScope).ToHashSet();

        // Closure severs definitions, never data: a constraint, view, domain, or composite type is re-creatable
        // from its declaration, but a column stands for its table's rows — so a column dependent blocks the
        // removal instead of widening the plan.
        var (blocked, severable) = (Columns(severed), Others(severed));

        List<Diagnostic> diagnostics = [];
        if (Others(stated) is { Count: > 0 } statedSeverable)
        {
            diagnostics.Add(DiffDiagnostics.SeveredOutOfScope(statedSeverable.Select(n => n.Address)));
        }
        if (severable.Where(n => !stated.Contains(n)).ToList() is { Count: > 0 } inferredSeverable)
        {
            diagnostics.Add(DiffDiagnostics.InferredSeveredOutOfScope(inferredSeverable.Select(n => n.Address)));
        }
        if (Columns(stated) is { Count: > 0 } statedBlocked)
        {
            diagnostics.Add(DiffDiagnostics.ColumnBlocksRemoval(statedBlocked.Select(n => n.Address)));
        }
        if (blocked.Where(n => !stated.Contains(n)).ToList() is { Count: > 0 } inferredBlocked)
        {
            diagnostics.Add(DiffDiagnostics.InferredColumnMayBlockRemoval(inferredBlocked.Select(n => n.Address)));
        }

        return Result.From(Widen(narrowed, severable), diagnostics);

        bool OutOfScope(DependencyNode node) => node.Address switch
        {
            ObjectAddress address => !scope.Contains(address),
            MemberAddress member => !scope.Contains(member.Owner),
            _ => false,
        };

        static List<DependencyNode> Columns(IEnumerable<DependencyNode> nodes) =>
            [.. nodes.Where(n => n.Kind == DependencyKind.Column)];

        static List<DependencyNode> Others(IEnumerable<DependencyNode> nodes) =>
            [.. nodes.Where(n => n.Kind != DependencyKind.Column)];
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

            foreach (var @enum in schema.Enums.Where(e => e.Kind == ChangeKind.Remove))
            {
                yield return new DependencyNode(new ObjectAddress(schema.Name, @enum.Name), DependencyKind.Enum);
            }

            foreach (var domain in schema.Domains.Where(d => d.Kind == ChangeKind.Remove))
            {
                yield return new DependencyNode(new ObjectAddress(schema.Name, domain.Name), DependencyKind.Domain);
            }

            foreach (var composite in schema.CompositeTypes.Where(c => c.Kind == ChangeKind.Remove))
            {
                yield return new DependencyNode(new ObjectAddress(schema.Name, composite.Name), DependencyKind.CompositeType);
            }
        }
    }

    /// <summary>
    /// Folds the severed nodes back in as changes to the schemas they live in.
    /// </summary>
    /// <remarks>
    /// A severed schema is only ever touched, never dropped, so its <see cref="SchemaDiff.Kind"/> stays null:
    /// the run is not about this schema, it just cannot avoid it. A partially-covered schema may already be
    /// in the diff, in which case the severed changes join its entry.
    /// </remarks>
    private static DatabaseDiff Widen(DatabaseDiff diff, IReadOnlyList<DependencyNode> severed)
    {
        var schemas = diff.Schemas.ToList();

        foreach (var bySchema in severed.Where(n => n.Address.SchemaName is not null).GroupBy(n => n.Address.SchemaName!))
        {
            var addition = new SchemaDiff(
                bySchema.Key,
                Tables: [.. SeveredTables(bySchema)],
                Views: [.. bySchema
                    .Where(n => n.Kind == DependencyKind.View)
                    .Select(n => new ViewDiff(bySchema.Key, ((ObjectAddress)n.Address).Name, ChangeKind.Remove))
                    .OrderBy(v => v.Name)],
                Domains: [.. bySchema
                    .Where(n => n.Kind == DependencyKind.Domain)
                    .Select(n => new DomainDiff(bySchema.Key, ((ObjectAddress)n.Address).Name, ChangeKind.Remove))
                    .OrderBy(d => d.Name)],
                CompositeTypes: [.. bySchema
                    .Where(n => n.Kind == DependencyKind.CompositeType)
                    .Select(n => new CompositeTypeDiff(bySchema.Key, ((ObjectAddress)n.Address).Name, ChangeKind.Remove))
                    .OrderBy(c => c.Name)]);

            var index = schemas.FindIndex(s => s.Name == bySchema.Key);
            if (index < 0)
            {
                schemas.Add(addition);
            }
            else
            {
                schemas[index] = schemas[index] with
                {
                    Tables = [.. schemas[index].Tables, .. addition.Tables],
                    Views = [.. schemas[index].Views, .. addition.Views],
                    Domains = [.. schemas[index].Domains, .. addition.Domains],
                    CompositeTypes = [.. schemas[index].CompositeTypes, .. addition.CompositeTypes],
                };
            }
        }

        return diff with { Schemas = schemas };
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
