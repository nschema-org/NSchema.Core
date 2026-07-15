using NSchema.Project.Domain.Models;
using NSchema.Model;
using NSchema.Model.Scripts;

namespace NSchema.Diff.Domain;

/// <summary>
/// The comparer's view of a project's directives.
/// </summary>
internal sealed class DirectiveLookup(ProjectDirectives directives)
{
    /// <summary>
    /// The index of no directives — what drift and teardown compare under.
    /// </summary>
    public static DirectiveLookup Empty { get; } = new(ProjectDirectives.Empty);

    private readonly Dictionary<SqlIdentifier, List<RenamePair>> _tableRenames = GroupRenames(directives.Tables.Renames);
    private readonly Dictionary<SqlIdentifier, List<RenamePair>> _viewRenames = GroupRenames(directives.Views.Renames);
    private readonly Dictionary<SqlIdentifier, List<RenamePair>> _enumRenames = GroupRenames(directives.Enums.Renames);
    private readonly Dictionary<SqlIdentifier, List<RenamePair>> _sequenceRenames = GroupRenames(directives.Sequences.Renames);
    private readonly Dictionary<SqlIdentifier, List<RenamePair>> _routineRenames = GroupRenames(directives.Routines.Renames);
    private readonly Dictionary<SqlIdentifier, List<RenamePair>> _domainRenames = GroupRenames(directives.Domains.Renames);
    private readonly Dictionary<SqlIdentifier, List<RenamePair>> _compositeTypeRenames = GroupRenames(directives.CompositeTypes.Renames);
    private readonly Dictionary<(SqlIdentifier Schema, SqlIdentifier Table), List<RenamePair>> _columnRenames = directives.Tables.ColumnRenames
            .GroupBy(r => (r.From.Schema, r.From.Object))
            .ToDictionary(g => g.Key, g => g.Select(r => new RenamePair(r.From.Member, r.To)).ToList());

    private readonly Dictionary<SqlIdentifier, List<SqlIdentifier>> _tableDrops = GroupDrops(directives.Tables.Drops);
    private readonly Dictionary<SqlIdentifier, List<SqlIdentifier>> _viewDrops = GroupDrops(directives.Views.Drops);
    private readonly Dictionary<SqlIdentifier, List<SqlIdentifier>> _enumDrops = GroupDrops(directives.Enums.Drops);
    private readonly Dictionary<SqlIdentifier, List<SqlIdentifier>> _sequenceDrops = GroupDrops(directives.Sequences.Drops);
    private readonly Dictionary<SqlIdentifier, List<SqlIdentifier>> _routineDrops = GroupDrops(directives.Routines.Drops);
    private readonly Dictionary<SqlIdentifier, List<SqlIdentifier>> _domainDrops = GroupDrops(directives.Domains.Drops);
    private readonly Dictionary<SqlIdentifier, List<SqlIdentifier>> _compositeTypeDrops = GroupDrops(directives.CompositeTypes.Drops);

    private readonly Dictionary<(SqlIdentifier Schema, SqlIdentifier Table), List<ChangeScript>> _changeScripts = directives.Tables.ChangeScripts
            .Where(script => script.ScopeSchema is not null)
            .GroupBy(script => (script.ScopeSchema!, script.TableName))
            .ToDictionary(g => g.Key, g => g.ToList());

    private readonly HashSet<SqlIdentifier> _partials = [.. directives.Schemas.Partials];

    /// <summary>
    /// The schema rename hints.
    /// </summary>
    public IReadOnlyList<RenamePair> SchemaRenames { get; } = [.. directives.Schemas.Renames.Select(r => new RenamePair(r.From, r.To))];

    /// <summary>
    /// The extension drops (extensions are database-global and never rename).
    /// </summary>
    public IReadOnlyList<SqlIdentifier> ExtensionDrops { get; } = directives.Extensions.Drops;

    /// <summary>
    /// Whether the declaration of <paramref name="declaredSchema"/> is partial.
    /// </summary>
    public bool IsPartial(SqlIdentifier declaredSchema) => _partials.Contains(declaredSchema);

    public IReadOnlyList<RenamePair> TableRenames(SqlIdentifier currentSchema) => Find(_tableRenames, currentSchema);

    /// <summary>
    /// The change-event scripts targeting the given table, keyed by the schema and table the change lands in
    /// (its desired names — the change event addresses the declared object it accompanies).
    /// </summary>
    public IReadOnlyList<ChangeScript> ChangeScripts(SqlIdentifier schema, SqlIdentifier table) =>
        _changeScripts.TryGetValue((schema, table), out var scripts) ? scripts : [];

    public IReadOnlyList<RenamePair> ViewRenames(SqlIdentifier currentSchema) => Find(_viewRenames, currentSchema);
    public IReadOnlyList<RenamePair> EnumRenames(SqlIdentifier currentSchema) => Find(_enumRenames, currentSchema);
    public IReadOnlyList<RenamePair> SequenceRenames(SqlIdentifier currentSchema) => Find(_sequenceRenames, currentSchema);
    public IReadOnlyList<RenamePair> RoutineRenames(SqlIdentifier currentSchema) => Find(_routineRenames, currentSchema);
    public IReadOnlyList<RenamePair> DomainRenames(SqlIdentifier currentSchema) => Find(_domainRenames, currentSchema);
    public IReadOnlyList<RenamePair> CompositeTypeRenames(SqlIdentifier currentSchema) => Find(_compositeTypeRenames, currentSchema);

    public IReadOnlyList<RenamePair> ColumnRenames(SqlIdentifier currentSchema, SqlIdentifier currentTable) =>
        _columnRenames.TryGetValue((currentSchema, currentTable), out var renames) ? renames : [];

    public IReadOnlyList<SqlIdentifier> TableDrops(SqlIdentifier currentSchema) => Find(_tableDrops, currentSchema);
    public IReadOnlyList<SqlIdentifier> ViewDrops(SqlIdentifier currentSchema) => Find(_viewDrops, currentSchema);
    public IReadOnlyList<SqlIdentifier> EnumDrops(SqlIdentifier currentSchema) => Find(_enumDrops, currentSchema);
    public IReadOnlyList<SqlIdentifier> SequenceDrops(SqlIdentifier currentSchema) => Find(_sequenceDrops, currentSchema);
    public IReadOnlyList<SqlIdentifier> RoutineDrops(SqlIdentifier currentSchema) => Find(_routineDrops, currentSchema);
    public IReadOnlyList<SqlIdentifier> DomainDrops(SqlIdentifier currentSchema) => Find(_domainDrops, currentSchema);
    public IReadOnlyList<SqlIdentifier> CompositeTypeDrops(SqlIdentifier currentSchema) => Find(_compositeTypeDrops, currentSchema);

    private static List<T> Find<T>(Dictionary<SqlIdentifier, List<T>> lookup, SqlIdentifier schema) =>
        lookup.TryGetValue(schema, out var entries) ? entries : [];

    private static Dictionary<SqlIdentifier, List<RenamePair>> GroupRenames(IReadOnlyList<ObjectRename> renames) =>
        renames.GroupBy(r => r.From.Schema).ToDictionary(g => g.Key, g => g.Select(r => new RenamePair(r.From.Name, r.To)).ToList());

    private static Dictionary<SqlIdentifier, List<SqlIdentifier>> GroupDrops(IReadOnlyList<ObjectReference> drops) =>
        drops.GroupBy(d => d.Schema).ToDictionary(g => g.Key, g => g.Select(d => d.Name).ToList());
}
