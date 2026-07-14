using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Project.Domain;

/// <summary>
/// Applies a <see cref="SchemaScope"/> to a given project or schema.
/// </summary>
internal static class ScopeFilter
{
    /// <summary>
    /// Restricts a project to a given scope.
    /// </summary>
    public static ProjectDefinition Apply(ProjectDefinition project, SchemaScope scope)
    {
        if (scope.IsAll)
        {
            return project;
        }

        var database = Apply(project.Database, scope);
        var scripts = Apply(project.Scripts, scope);
        var directives = Apply(project.Directives, scope);
        return new ProjectDefinition(database, scripts, directives);
    }

    /// <summary>
    /// Restricts <paramref name="database"/> to the schemas inside <paramref name="scope"/>.
    /// </summary>
    public static Database Apply(Database database, SchemaScope scope)
    {
        // Extensions are database-global, not schema-scoped, so they pass through a namespace filter untouched:
        // an extension is a prerequisite of the whole database regardless of which schemas are in scope.
        if (scope.IsAll)
        {
            return database;
        }

        var filtered = database.Schemas.Where(s => scope.Includes(s.Name)).ToList();
        return new Database(filtered, database.Extensions);
    }

    private static IReadOnlyList<Script> Apply(IEnumerable<Script> scripts, SchemaScope scope)
    {
        return scripts
            .Where(s => s.Event.ScopeSchema is not { } schema || scope.Includes(schema))
            .ToList();
    }

    /// <summary>
    /// Restricts the directives to those addressing in-scope schemas.
    /// </summary>
    private static ProjectDirectives Apply(ProjectDirectives directives, SchemaScope scope)
    {
        // A current schema name maps to its declared name through the schema renames.
        var declaredNames = directives.Schemas.Renames.ToDictionary(r => r.From, r => r.To);
        bool InScope(SqlIdentifier currentSchema) =>
            scope.Includes(currentSchema)
            || (declaredNames.TryGetValue(currentSchema, out var declared) && scope.Includes(declared));

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
            directives.Extensions);
    }
}
