using NSchema.Model.Columns;
using NSchema.Model.Tables;

namespace NSchema.Model.Services;

/// <summary>
/// A graph representing what requires what across a database.
/// </summary>
/// <remarks>
/// An edge A->B means "A requires B, to exist". Edges are directional, and each answer different questions:
/// what a node needs before it can be created, and what must go before it can be dropped.
/// </remarks>
internal sealed class DependencyGraph
{
    private readonly Dictionary<Address, List<DependencyNode>> _byAddress = [];
    private readonly Dictionary<DependencyNode, List<Edge>> _requires = [];
    private readonly Dictionary<DependencyNode, List<Edge>> _requiredBy = [];
    private readonly ILookup<SqlIdentifier, ObjectAddress> _typesByName;

    private readonly record struct Edge(DependencyNode Node, DependencyCertainty Certainty);

    /// <summary>
    /// Builds the graph of everything <paramref name="database"/> contains.
    /// </summary>
    public DependencyGraph(Database database)
    {
        var allTables = database.Schemas.SelectMany(s => s.Tables.Select(t => (Schema: s.Name, Table: t))).ToList();
        var allViews = database.Schemas.SelectMany(s => s.Views.Select(v => (Schema: s.Name, View: v))).ToList();
        var allTypes = database.Schemas.SelectMany(s =>
            s.Enums.Select(e => (Schema: s.Name, e.Name, Kind: DependencyKind.Enum))
                .Concat(s.Domains.Select(d => (Schema: s.Name, d.Name, Kind: DependencyKind.Domain)))
                .Concat(s.CompositeTypes.Select(c => (Schema: s.Name, c.Name, Kind: DependencyKind.CompositeType)))).ToList();

        _typesByName = allTypes.ToLookup(t => t.Name, t => new ObjectAddress(t.Schema, t.Name));

        // Nodes first: an edge can point at anything, including something declared later.
        foreach (var (schema, table) in allTables)
        {
            Add(new DependencyNode(new ObjectAddress(schema, table.Name), DependencyKind.Table));
            foreach (var foreignKey in table.ForeignKeys)
            {
                Add(ConstraintNode(schema, table.Name, foreignKey));
            }
        }

        foreach (var (schema, view) in allViews)
        {
            Add(new DependencyNode(new ObjectAddress(schema, view.Name), DependencyKind.View));
        }

        foreach (var (schema, name, kind) in allTypes)
        {
            Add(new DependencyNode(new ObjectAddress(schema, name), kind));
        }

        foreach (var (schema, table) in allTables)
        {
            foreach (var foreignKey in table.ForeignKeys)
            {
                // The constraint requires the table it points at — not the table that owns it, which is
                // containment. So dropping the referenced table costs the constraint, and nothing more.
                // The model names that table outright, so the edge is exact.
                Connect(ConstraintNode(schema, table.Name, foreignKey),
                    new ObjectAddress(foreignKey.ReferencedSchema, foreignKey.ReferencedTable),
                    DependencyCertainty.Stated);
            }
        }

        foreach (var (schema, view) in database.Schemas.SelectMany(s => s.Views.Select(v => (Schema: s.Name, View: v))))
        {
            var node = new DependencyNode(new ObjectAddress(schema, view.Name), DependencyKind.View);
            foreach (var dependency in view.DependsOn)
            {
                // A view's dependency is embedded in its body: there is nothing to sever but the view itself.
                // What it reads was scanned out of SQL nobody parsed, so the edge is a guess — a good one for
                // ordering two things already in a plan, not good enough to drag a third into it unannounced.
                Connect(node, new ObjectAddress(dependency.Schema, dependency.Name), DependencyCertainty.Inferred);
            }
        }

        // A declared data type may name a user type: from a column, from a domain's base, or from a composite's
        // field. The dependent of a column edge is the column itself — its table is not required to go, but the
        // column cannot outlive its type.
        foreach (var (schema, table) in allTables)
        {
            foreach (var column in table.Columns)
            {
                ConnectToType(new DependencyNode(new MemberAddress(schema, table.Name, column.Name), DependencyKind.Column), column.Type);
            }
        }

        foreach (var (schema, domain) in database.Schemas.SelectMany(s => s.Domains.Select(d => (Schema: s.Name, Domain: d))))
        {
            ConnectToType(new DependencyNode(new ObjectAddress(schema, domain.Name), DependencyKind.Domain), domain.DataType);
        }

        foreach (var (schema, composite) in database.Schemas.SelectMany(s => s.CompositeTypes.Select(c => (Schema: s.Name, Composite: c))))
        {
            var node = new DependencyNode(new ObjectAddress(schema, composite.Name), DependencyKind.CompositeType);
            foreach (var field in composite.Fields)
            {
                ConnectToType(node, field.DataType);
            }
        }
    }

    /// <summary>
    /// The nodes <paramref name="node"/> requires to exist.
    /// </summary>
    public IReadOnlyCollection<DependencyNode> DependenciesOf(DependencyNode node) => Nodes(_requires, node);

    /// <summary>
    /// The nodes that require <paramref name="node"/> to exist.
    /// </summary>
    public IReadOnlyCollection<DependencyNode> DependentsOf(DependencyNode node) => Nodes(_requiredBy, node);

    /// <summary>
    /// Everything that transitively requires <paramref name="seeds"/>: what else must go before they can.
    /// </summary>
    public IReadOnlyCollection<DependencyNode> AllDependentsOf(IEnumerable<DependencyNode> seeds) =>
        Close(seeds, node => Nodes(_requiredBy, node));

    /// <summary>
    /// The part of <see cref="AllDependentsOf"/> reachable without believing anything NSchema guessed.
    /// </summary>
    /// <remarks>
    /// The difference between the two is the part a caller should hedge on rather than assert.
    /// </remarks>
    public IReadOnlyCollection<DependencyNode> StatedDependentsOf(IEnumerable<DependencyNode> seeds) =>
        Close(seeds, node => Nodes(_requiredBy, node, DependencyCertainty.Stated));

    /// <summary>
    /// Everything <paramref name="seeds"/> transitively require: what must exist before they can.
    /// </summary>
    public IReadOnlyCollection<DependencyNode> AllDependenciesOf(IEnumerable<DependencyNode> seeds) =>
        Close(seeds, node => Nodes(_requires, node));

    /// <summary>
    /// The nodes living at <paramref name="address"/>, of any kind.
    /// </summary>
    public IReadOnlyCollection<DependencyNode> At(Address address) =>
        _byAddress.TryGetValue(address, out var nodes) ? nodes : [];

    /// <summary>
    /// Walks <paramref name="along"/> from every seed until nothing new turns up.
    /// </summary>
    /// <remarks>
    /// The seeds are excluded — a caller asks what its closure costs it, not what it already has — and a node
    /// is visited once, so a cycle terminates rather than needing the edges to be acyclic.
    /// </remarks>
    private static List<DependencyNode> Close(IEnumerable<DependencyNode> seeds, Func<DependencyNode, IReadOnlyCollection<DependencyNode>> along)
    {
        var seen = new HashSet<DependencyNode>(seeds);
        var pending = new Queue<DependencyNode>(seen);
        var closure = new List<DependencyNode>();

        while (pending.TryDequeue(out var node))
        {
            foreach (var next in along(node).Where(seen.Add))
            {
                closure.Add(next);
                pending.Enqueue(next);
            }
        }

        return closure;
    }

    private static DependencyNode ConstraintNode(SqlIdentifier schema, SqlIdentifier table, ForeignKey foreignKey) =>
        new(new MemberAddress(schema, table, foreignKey.Name), DependencyKind.ForeignKey);

    private void ConnectToType(DependencyNode dependent, SqlType type)
    {
        if (ResolveType(type) is not var (address, certainty) || address == dependent.Address)
        {
            return;
        }

        Add(dependent);
        Connect(dependent, address, certainty);
    }

    private (ObjectAddress Address, DependencyCertainty Certainty)? ResolveType(SqlType type)
    {
        if (type.Schema is { } schema)
        {
            return (new ObjectAddress(schema, type.Name), DependencyCertainty.Stated);
        }

        return _typesByName[type.Name].Take(2).ToList() is [var only]
            ? (only, DependencyCertainty.Inferred)
            : null;
    }

    private static IReadOnlyCollection<DependencyNode> Nodes(
        Dictionary<DependencyNode, List<Edge>> edges, DependencyNode node, DependencyCertainty? only = null) =>
        edges.TryGetValue(node, out var found)
            ? [.. found.Where(e => only is null || e.Certainty == only).Select(e => e.Node)]
            : [];

    private void Add(DependencyNode node)
    {
        if (!_byAddress.TryGetValue(node.Address, out var atAddress))
        {
            _byAddress[node.Address] = atAddress = [];
        }
        if (!atAddress.Contains(node))
        {
            atAddress.Add(node);
        }
    }

    /// <summary>
    /// Records that <paramref name="dependent"/> requires whatever lives at <paramref name="address"/>.
    /// </summary>
    /// <remarks>
    /// A dependency on something outside this database — not managed, or simply not here — produces no edge,
    /// the same way the linearizer's sort ignores what it cannot see.
    /// </remarks>
    private void Connect(DependencyNode dependent, Address address, DependencyCertainty certainty)
    {
        foreach (var dependency in At(address))
        {
            Link(_requires, dependent, new Edge(dependency, certainty));
            Link(_requiredBy, dependency, new Edge(dependent, certainty));
        }
    }

    private static void Link(Dictionary<DependencyNode, List<Edge>> edges, DependencyNode from, Edge to)
    {
        if (!edges.TryGetValue(from, out var found))
        {
            edges[from] = found = [];
        }
        if (!found.Contains(to))
        {
            found.Add(to);
        }
    }
}
