using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.Project.Model.Directives;

namespace NSchema.Diff.Model.Services;

/// <summary>
/// The comparer's view of a project's directives.
/// </summary>
internal sealed class DirectiveLookup(ProjectDirectives directives)
{
    /// <summary>
    /// The index of no directives — what drift and teardown compare under.
    /// </summary>
    public static DirectiveLookup Empty { get; } = new(ProjectDirectives.Empty);

    private readonly Dictionary<(ObjectKind Kind, SqlIdentifier Schema), List<RenamePair>> _renames = directives.Renames
            .GroupBy(r => (r.Kind, r.From.Schema))
            .ToDictionary(g => g.Key, g => g.Select(r => new RenamePair(r.From.Name, r.To)).ToList());

    private readonly Dictionary<(ObjectKind Kind, SqlIdentifier Schema), List<SqlIdentifier>> _drops = directives.Drops
            .GroupBy(d => (d.Kind, d.Address.Schema))
            .ToDictionary(g => g.Key, g => g.Select(d => d.Address.Name).ToList());

    private readonly Dictionary<(SqlIdentifier Schema, SqlIdentifier Table), List<RenamePair>> _columnRenames = directives.ColumnRenames
            .GroupBy(r => (r.From.Schema, r.From.Object))
            .ToDictionary(g => g.Key, g => g.Select(r => new RenamePair(r.From.Member, r.To)).ToList());

    private readonly Dictionary<(SqlIdentifier Schema, SqlIdentifier Table), List<ChangeScript>> _changeScripts = directives.ChangeScripts
            .Where(script => script.ScopeSchema is not null)
            .GroupBy(script => (script.ScopeSchema!, script.TableName))
            .ToDictionary(g => g.Key, g => g.ToList());

    private readonly HashSet<SqlIdentifier> _partials = [.. directives.Schemas.Partials.Select(p => p.Schema)];

    /// <summary>
    /// The schema rename hints.
    /// </summary>
    public IReadOnlyList<RenamePair> SchemaRenames { get; } = [.. directives.Schemas.Renames.Select(r => new RenamePair(r.From, r.To))];

    /// <summary>
    /// The extension drops (extensions are database-global and never rename).
    /// </summary>
    public IReadOnlyList<SqlIdentifier> ExtensionDrops { get; } = [.. directives.ExtensionDrops.Select(d => d.Name)];

    /// <summary>
    /// Whether the declaration of <paramref name="declaredSchema"/> is partial.
    /// </summary>
    public bool IsPartial(SqlIdentifier declaredSchema) => _partials.Contains(declaredSchema);

    /// <summary>
    /// The rename hints for objects of <paramref name="kind"/> in <paramref name="currentSchema"/>.
    /// </summary>
    public IReadOnlyList<RenamePair> Renames(ObjectKind kind, SqlIdentifier currentSchema) =>
        _renames.TryGetValue((kind, currentSchema), out var renames) ? renames : [];

    /// <summary>
    /// The names of objects of <paramref name="kind"/> explicitly declared dropped in <paramref name="currentSchema"/>.
    /// </summary>
    public IReadOnlyList<SqlIdentifier> Drops(ObjectKind kind, SqlIdentifier currentSchema) =>
        _drops.TryGetValue((kind, currentSchema), out var drops) ? drops : [];

    /// <summary>
    /// The change-event scripts targeting the given table, keyed by the schema and table the change lands in
    /// (its desired names — the change event addresses the declared object it accompanies).
    /// </summary>
    public IReadOnlyList<ChangeScript> ChangeScripts(SqlIdentifier schema, SqlIdentifier table) =>
        _changeScripts.TryGetValue((schema, table), out var scripts) ? scripts : [];

    public IReadOnlyList<RenamePair> ColumnRenames(SqlIdentifier currentSchema, SqlIdentifier currentTable) =>
        _columnRenames.TryGetValue((currentSchema, currentTable), out var renames) ? renames : [];
}
