using NSchema.Project.Domain.Models;

namespace NSchema.Project.Domain;

/// <summary>
/// Applies a <see cref="SchemaScope"/> to a given project or schema.
/// </summary>
internal static class SchemaFilter
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

        var scripts = project.Scripts
            .Where(s => s.Event.ScopeSchema is not { } schema || scope.Includes(schema))
            .ToList();
        return new ProjectDefinition(Apply(project.Schema, scope), scripts);
    }

    /// <summary>
    /// Restricts <paramref name="schema"/> to the schemas inside <paramref name="scope"/>.
    /// </summary>
    public static DatabaseSchema Apply(DatabaseSchema schema, SchemaScope scope)
    {
        // Extensions are database-global, not schema-scoped, so they pass through a namespace filter untouched:
        // an extension is a prerequisite of the whole database regardless of which schemas are in scope.
        if (scope.IsAll)
        {
            return schema;
        }

        var filtered = schema.Schemas.Where(s => scope.Includes(s.Name)).ToList();
        var filteredDropped = schema.DroppedSchemas.Where(scope.Includes).ToList();
        return new DatabaseSchema(filtered, filteredDropped, schema.Extensions, schema.DroppedExtensions);
    }
}
