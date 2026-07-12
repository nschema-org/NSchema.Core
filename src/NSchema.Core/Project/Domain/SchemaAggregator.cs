using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Extensions;
using NSchema.Project.Domain.Models.Schemas;

namespace NSchema.Project.Domain;

/// <summary>
/// Merges declared schemas from multiple sources into one. A name declared by more than one source is an
/// authoring mistake, reported as an error diagnostic (first declaration wins) — every collision at once,
/// with the best-effort merge still carried.
/// </summary>
internal static class SchemaAggregator
{
    public static Result<DatabaseSchema> Combine(DatabaseSchema first, DatabaseSchema second)
    {
        var diagnostics = new List<Diagnostic>();

        var mergedSchemas = new[] { first, second }
            .SelectMany(db => db.Schemas)
            .GroupBy(s => s.Name)
            .Select(s => AggregateSchemaGroup(s.ToList(), diagnostics))
            .ToList();

        var droppedSchemas = first.DroppedSchemas
            .Concat(second.DroppedSchemas)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Extensions are database-global, so they aggregate at the root (not per schema). A name declared by
        // more than one source is a conflict, mirroring how duplicate tables/enums are rejected.
        var extensions = new List<Extension>();
        var seenExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in first.Extensions.Concat(second.Extensions))
        {
            if (!seenExtensions.Add(extension.Name))
            {
                diagnostics.Add(Diagnostic.Error("project", $"Duplicate extension '{extension.Name}' declared."));
                continue;
            }

            extensions.Add(extension);
        }

        var droppedExtensions = first.DroppedExtensions
            .Concat(second.DroppedExtensions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result.From(new DatabaseSchema(mergedSchemas, droppedSchemas, extensions, droppedExtensions), diagnostics);
    }

    private static SchemaDefinition AggregateSchemaGroup(IReadOnlyList<SchemaDefinition> schemas, List<Diagnostic> diagnostics)
    {
        var schemaName = schemas[0].Name;

        var tables = MergeUnique(schemas, s => s.Tables, t => t.Name, schemaName, "table", diagnostics);
        var views = MergeUnique(schemas, s => s.Views, v => v.Name, schemaName, "view", diagnostics);
        var enums = MergeUnique(schemas, s => s.Enums, e => e.Name, schemaName, "enum", diagnostics);
        var sequences = MergeUnique(schemas, s => s.Sequences, q => q.Name, schemaName, "sequence", diagnostics);
        var domains = MergeUnique(schemas, s => s.Domains, d => d.Name, schemaName, "domain", diagnostics);
        var compositeTypes = MergeUnique(schemas, s => s.CompositeTypes, c => c.Name, schemaName, "composite type", diagnostics);

        // Functions and procedures are one routine pool sharing a single name space, as in the database (e.g.
        // Postgres's pg_proc): a function and a procedure with the same name cannot coexist in a schema. Modeling
        // them as one list makes that fall out of a single duplicate check.
        var routines = MergeUnique(schemas, s => s.Routines, r => r.Name, schemaName, "routine", diagnostics,
            ": functions and procedures share one name space");

        var comments = schemas.Select(s => s.Comment).Where(c => c is not null).Distinct().ToList();
        if (comments.Count > 1)
        {
            diagnostics.Add(Diagnostic.Error("project", $"Conflicting comments specified for schema '{schemaName}'."));
        }
        var comment = comments.FirstOrDefault();

        var oldNames = schemas.Select(s => s.OldName).Where(n => n is not null).Distinct().ToList();
        if (oldNames.Count > 1)
        {
            diagnostics.Add(Diagnostic.Error("project", $"Conflicting old names specified for schema '{schemaName}'."));
        }
        var oldName = oldNames.FirstOrDefault();

        var isPartial = schemas.Any(s => s.IsPartial);
        var droppedTables = MergeDropped(schemas, s => s.DroppedTables);
        var droppedViews = MergeDropped(schemas, s => s.DroppedViews);
        var droppedEnums = MergeDropped(schemas, s => s.DroppedEnums);
        var droppedSequences = MergeDropped(schemas, s => s.DroppedSequences);
        var droppedRoutines = MergeDropped(schemas, s => s.DroppedRoutines);
        var droppedDomains = MergeDropped(schemas, s => s.DroppedDomains);
        var droppedCompositeTypes = MergeDropped(schemas, s => s.DroppedCompositeTypes);

        var grants = schemas
            .SelectMany(s => s.Grants)
            .Distinct()
            .ToList();

        return new SchemaDefinition(
            schemaName, oldName, isPartial, comment, tables, droppedTables, grants, views, droppedViews,
            enums, droppedEnums, sequences, droppedSequences,
            routines, droppedRoutines, domains, droppedDomains, compositeTypes, droppedCompositeTypes);
    }

    /// <summary>
    /// Concatenates one object kind across the sources, reporting a duplicate name as an error (first
    /// declaration wins).
    /// </summary>
    private static List<T> MergeUnique<T>(
        IReadOnlyList<SchemaDefinition> schemas,
        Func<SchemaDefinition, IEnumerable<T>> select,
        Func<T, string> name,
        string schemaName,
        string kind,
        List<Diagnostic> diagnostics,
        string suffix = "")
    {
        var result = new List<T>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in schemas.SelectMany(select))
        {
            if (!seen.Add(name(item)))
            {
                diagnostics.Add(Diagnostic.Error("project", $"Duplicate {kind} '{name(item)}' found in schema '{schemaName}'{suffix}."));
                continue;
            }

            result.Add(item);
        }

        return result;
    }

    private static List<string> MergeDropped(IReadOnlyList<SchemaDefinition> schemas, Func<SchemaDefinition, IEnumerable<string>> select) =>
        schemas.SelectMany(select).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
