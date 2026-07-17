using NSchema.Model;
using NSchema.Model.Scripts;

namespace NSchema.Project.Model.Directives;

/// <summary>
/// The management directives a project declares.
/// </summary>
public sealed record ProjectDirectives(
    SchemaDirectives? Schemas = null,
    IReadOnlyList<ObjectRenameDirective>? Renames = null,
    IReadOnlyList<ObjectDropDirective>? Drops = null,
    IReadOnlyList<MemberRenameDirective>? ColumnRenames = null,
    IReadOnlyList<ExtensionDropDirective>? ExtensionDrops = null,
    IReadOnlyList<ChangeScript>? ChangeScripts = null,
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
    /// The schema-level object renames, of every kind.
    /// </summary>
    public IReadOnlyList<ObjectRenameDirective> Renames { get; init; } = Renames ?? [];

    /// <summary>
    /// The schema-level objects explicitly declared dropped, of every kind.
    /// </summary>
    public IReadOnlyList<ObjectDropDirective> Drops { get; init; } = Drops ?? [];

    /// <summary>
    /// The declared column renames.
    /// </summary>
    public IReadOnlyList<MemberRenameDirective> ColumnRenames { get; init; } = ColumnRenames ?? [];

    /// <summary>
    /// The extensions explicitly declared dropped (extensions are database-global and never rename).
    /// </summary>
    public IReadOnlyList<ExtensionDropDirective> ExtensionDrops { get; init; } = ExtensionDrops ?? [];

    /// <summary>
    /// The change-event scripts targeting table members.
    /// </summary>
    public IReadOnlyList<ChangeScript> ChangeScripts { get; init; } = ChangeScripts ?? [];

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
                Drops = [.. Schemas.Drops.Where(d => scope.Includes(d.Name))],
                Partials = [.. Schemas.Partials.Where(p => scope.Includes(p.Schema))],
            },
            [.. Renames.Where(r => InScope(r.From.Schema))],
            [.. Drops.Where(d => InScope(d.Address.Schema))],
            [.. ColumnRenames.Where(r => InScope(r.From.Schema))],
            ExtensionDrops,
            [.. ChangeScripts.Where(ScriptInScope)],
            [.. DeploymentScripts.Where(ScriptInScope)]);

        bool ScriptInScope(Script script) =>
            script.ScopeSchema is not { } schema || scope.Includes(schema);

        bool InScope(SqlIdentifier currentSchema) =>
            scope.Includes(currentSchema)
            || (declaredNames.TryGetValue(currentSchema, out var declared) && scope.Includes(declared));
    }
}
