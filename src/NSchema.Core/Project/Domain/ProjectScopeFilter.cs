using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.Project.Domain.Models;

namespace NSchema.Project.Domain;

/// <summary>
/// Applies a <see cref="DatabaseScope"/> to a project: its database, and the directives that steer it.
/// </summary>
internal static class ProjectScopeFilter
{
    /// <summary>
    /// Restricts a project to a given scope.
    /// </summary>
    public static ProjectDefinition Apply(ProjectDefinition project, DatabaseScope scope)
    {
        if (scope.IsAll)
        {
            return project;
        }

        var database = scope.Apply(project.Database);
        var directives = Apply(project.Directives, scope);
        return new ProjectDefinition(database, directives);
    }

    /// <summary>
    /// Restricts the directives to those addressing in-scope schemas.
    /// </summary>
    private static ProjectDirectives Apply(ProjectDirectives directives, DatabaseScope scope)
    {
        // A current schema name maps to its declared name through the schema renames.
        var declaredNames = directives.Schemas.Renames.ToDictionary(r => r.From, r => r.To);
        bool InScope(SqlIdentifier currentSchema) =>
            scope.Includes(currentSchema)
            || (declaredNames.TryGetValue(currentSchema, out var declared) && scope.Includes(declared));

        bool ScriptInScope(Script script) =>
            script.ScopeSchema is not { } schema || scope.Includes(schema);

        return new ProjectDirectives(
            directives.Schemas with
            {
                Renames = [.. directives.Schemas.Renames.Where(r => scope.Includes(r.From) || scope.Includes(r.To))],
                Drops = [.. directives.Schemas.Drops.Where(scope.Includes)],
                Partials = [.. directives.Schemas.Partials.Where(scope.Includes)],
            },
            directives.Tables with
            {
                Renames = [.. directives.Tables.Renames.Where(r => InScope(r.From.Schema))],
                Drops = [.. directives.Tables.Drops.Where(d => InScope(d.Schema))],
                ColumnRenames = [.. directives.Tables.ColumnRenames.Where(r => InScope(r.From.Schema))],
                ChangeScripts = [.. directives.Tables.ChangeScripts.Where(ScriptInScope)],
            },
            directives.Views with
            {
                Renames = [.. directives.Views.Renames.Where(r => InScope(r.From.Schema))],
                Drops = [.. directives.Views.Drops.Where(d => InScope(d.Schema))],
            },
            directives.Enums with
            {
                Renames = [.. directives.Enums.Renames.Where(r => InScope(r.From.Schema))],
                Drops = [.. directives.Enums.Drops.Where(d => InScope(d.Schema))],
            },
            directives.Sequences with
            {
                Renames = [.. directives.Sequences.Renames.Where(r => InScope(r.From.Schema))],
                Drops = [.. directives.Sequences.Drops.Where(d => InScope(d.Schema))],
            },
            directives.Routines with
            {
                Renames = [.. directives.Routines.Renames.Where(r => InScope(r.From.Schema))],
                Drops = [.. directives.Routines.Drops.Where(d => InScope(d.Schema))],
            },
            directives.Domains with
            {
                Renames = [.. directives.Domains.Renames.Where(r => InScope(r.From.Schema))],
                Drops = [.. directives.Domains.Drops.Where(d => InScope(d.Schema))],
            },
            directives.CompositeTypes with
            {
                Renames = [.. directives.CompositeTypes.Renames.Where(r => InScope(r.From.Schema))],
                Drops = [.. directives.CompositeTypes.Drops.Where(d => InScope(d.Schema))],
            },
            directives.Extensions)
        {
            DeploymentScripts = [.. directives.DeploymentScripts.Where(ScriptInScope)],
        };
    }
}
