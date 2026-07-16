using NSchema.Model;
using NSchema.Model.Scripts;

namespace NSchema.Project.Domain.Models;

/// <summary>
/// The management directives a project declares.
/// </summary>
public sealed record ProjectDirectives(
    SchemaDirectives? Schemas = null,
    TableDirectives? Tables = null,
    ViewDirectives? Views = null,
    EnumDirectives? Enums = null,
    SequenceDirectives? Sequences = null,
    RoutineDirectives? Routines = null,
    DomainDirectives? Domains = null,
    CompositeTypeDirectives? CompositeTypes = null,
    ExtensionDirectives? Extensions = null,
    IReadOnlyList<DeploymentScript>? DeploymentScripts = null
)
{
    /// <summary>
    /// A project declaring no directives.
    /// </summary>
    public static ProjectDirectives Empty { get; } = new();

    /// <summary>
    /// The schema directives.
    /// </summary>
    public SchemaDirectives Schemas { get; init; } = Schemas ?? new();

    /// <summary>
    /// The table directives, column renames included.
    /// </summary>
    public TableDirectives Tables { get; init; } = Tables ?? new();

    /// <summary>
    /// The view directives.
    /// </summary>
    public ViewDirectives Views { get; init; } = Views ?? new();

    /// <summary>
    /// The enum-type directives.
    /// </summary>
    public EnumDirectives Enums { get; init; } = Enums ?? new();

    /// <summary>
    /// The sequence directives.
    /// </summary>
    public SequenceDirectives Sequences { get; init; } = Sequences ?? new();

    /// <summary>
    /// The routine directives.
    /// </summary>
    public RoutineDirectives Routines { get; init; } = Routines ?? new();

    /// <summary>
    /// The domain directives.
    /// </summary>
    public DomainDirectives Domains { get; init; } = Domains ?? new();

    /// <summary>
    /// The composite-type directives.
    /// </summary>
    public CompositeTypeDirectives CompositeTypes { get; init; } = CompositeTypes ?? new();

    /// <summary>
    /// The extension directives (drops only).
    /// </summary>
    public ExtensionDirectives Extensions { get; init; } = Extensions ?? new();

    /// <summary>
    /// The deployment scripts.
    /// </summary>
    public IReadOnlyList<DeploymentScript> DeploymentScripts { get; init; } = DeploymentScripts ?? [];

    /// <summary>
    /// Restricts the directives to those addressing in-scope schemas.
    /// </summary>
    public ProjectDirectives ScopedTo(PlanningScope scope)
    {
        // A current schema name maps to its declared name through the schema renames.
        var declaredNames = Schemas.Renames.ToDictionary(r => r.From, r => r.To);

        return new ProjectDirectives(
            Schemas with
            {
                Renames = [.. Schemas.Renames.Where(r => scope.Includes(r.From) || scope.Includes(r.To))],
                Drops = [.. Schemas.Drops.Where(scope.Includes)],
                Partials = [.. Schemas.Partials.Where(scope.Includes)],
            },
            Tables with
            {
                Renames = [.. Tables.Renames.Where(r => InScope(r.From.Schema))],
                Drops = [.. Tables.Drops.Where(d => InScope(d.Schema))],
                ColumnRenames = [.. Tables.ColumnRenames.Where(r => InScope(r.From.Schema))],
                ChangeScripts = [.. Tables.ChangeScripts.Where(ScriptInScope)],
            },
            Views with
            {
                Renames = [.. Views.Renames.Where(r => InScope(r.From.Schema))],
                Drops = [.. Views.Drops.Where(d => InScope(d.Schema))],
            },
            Enums with
            {
                Renames = [.. Enums.Renames.Where(r => InScope(r.From.Schema))],
                Drops = [.. Enums.Drops.Where(d => InScope(d.Schema))],
            },
            Sequences with
            {
                Renames = [.. Sequences.Renames.Where(r => InScope(r.From.Schema))],
                Drops = [.. Sequences.Drops.Where(d => InScope(d.Schema))],
            },
            Routines with
            {
                Renames = [.. Routines.Renames.Where(r => InScope(r.From.Schema))],
                Drops = [.. Routines.Drops.Where(d => InScope(d.Schema))],
            },
            Domains with
            {
                Renames = [.. Domains.Renames.Where(r => InScope(r.From.Schema))],
                Drops = [.. Domains.Drops.Where(d => InScope(d.Schema))],
            },
            CompositeTypes with
            {
                Renames = [.. CompositeTypes.Renames.Where(r => InScope(r.From.Schema))],
                Drops = [.. CompositeTypes.Drops.Where(d => InScope(d.Schema))],
            },
            Extensions)
        {
            DeploymentScripts = [.. DeploymentScripts.Where(ScriptInScope)],
        };

        bool ScriptInScope(Script script) =>
            script.ScopeSchema is not { } schema || scope.Includes(schema);

        bool InScope(SqlIdentifier currentSchema) =>
            scope.Includes(currentSchema)
            || (declaredNames.TryGetValue(currentSchema, out var declared) && scope.Includes(declared));
    }
}
