using NSchema.Model;
using NSchema.Model.Extensions;
using NSchema.Model.Schemas;

namespace NSchema.Project.Model.Services;

/// <summary>
/// Combines partial database model fragments.
/// </summary>
internal static class DatabaseAggregator
{
    public static Result<Database> Combine(Database first, Database second)
    {
        var diagnostics = new List<Diagnostic>();

        var mergedSchemas = new[] { first, second }
            .SelectMany(db => db.Schemas)
            .GroupBy(s => s.Name)
            .Select(s => AggregateSchemaGroup([.. s], diagnostics))
            .ToList();

        // Extensions are database-global, so they aggregate at the root (not per schema). A name declared by
        // more than one source is a conflict, mirroring how duplicate tables/enums are rejected.
        var extensions = new List<Extension>();
        var seenExtensions = new HashSet<SqlIdentifier>();
        foreach (var extension in first.Extensions.Concat(second.Extensions))
        {
            if (!seenExtensions.Add(extension.Name))
            {
                diagnostics.Add(ProjectDiagnostics.DuplicateExtension(extension.Name));
                continue;
            }

            extensions.Add(extension);
        }

        return Result.From(new Database(mergedSchemas, extensions), diagnostics);
    }

    private static Schema AggregateSchemaGroup(IReadOnlyList<Schema> schemas, List<Diagnostic> diagnostics)
    {
        var schemaName = schemas[0].Name;

        // The winning declarations are copied into the merged schema — their sources keep them; a node is
        // never re-parented.
        var tables = MergeUnique(schemas, s => s.Tables, t => t.Name, t => t.Clone(), schemaName, "table", diagnostics);
        var views = MergeUnique(schemas, s => s.Views, v => v.Name, v => v.Clone(), schemaName, "view", diagnostics);
        var enums = MergeUnique(schemas, s => s.Enums, e => e.Name, e => e.Clone(), schemaName, "enum", diagnostics);
        var sequences = MergeUnique(schemas, s => s.Sequences, q => q.Name, q => q.Clone(), schemaName, "sequence", diagnostics);
        var domains = MergeUnique(schemas, s => s.Domains, d => d.Name, d => d.Clone(), schemaName, "domain", diagnostics);
        var compositeTypes = MergeUnique(schemas, s => s.CompositeTypes, c => c.Name, c => c.Clone(), schemaName, "composite type", diagnostics);

        // Functions and procedures are one routine pool sharing a single name space, as in the database (e.g.
        // Postgres's pg_proc): a function and a procedure with the same name cannot coexist in a schema. Modeling
        // them as one list makes that fall out of a single duplicate check.
        var routines = MergeUnique(schemas, s => s.Routines, r => r.Name, r => r.Clone(), schemaName, "routine", diagnostics,
            ": functions and procedures share one name space");

        var comments = schemas.Select(s => s.Comment).Where(c => c is not null).Distinct().ToList();
        if (comments.Count > 1)
        {
            diagnostics.Add(ProjectDiagnostics.ConflictingComments(schemaName));
        }
        var comment = comments.FirstOrDefault();

        var grants = schemas
            .SelectMany(s => s.Grants)
            .Distinct()
            .ToList();

        return new Schema(schemaName, tables, grants, views, enums, sequences, routines, domains, compositeTypes) { Comment = comment };
    }

    /// <summary>
    /// Concatenates one object kind across the sources, reporting a duplicate name as an error (first
    /// declaration wins).
    /// </summary>
    private static List<T> MergeUnique<T>(
        IReadOnlyList<Schema> schemas,
        Func<Schema, IEnumerable<T>> select,
        Func<T, SqlIdentifier> name,
        Func<T, T> copy,
        SqlIdentifier schemaName,
        string kind,
        List<Diagnostic> diagnostics,
        string suffix = "")
    {
        var result = new List<T>();
        var seen = new HashSet<SqlIdentifier>();
        foreach (var item in schemas.SelectMany(select))
        {
            if (!seen.Add(name(item)))
            {
                diagnostics.Add(ProjectDiagnostics.DuplicateObject(kind, name(item), schemaName, suffix));
                continue;
            }

            result.Add(copy(item));
        }

        return result;
    }
}
