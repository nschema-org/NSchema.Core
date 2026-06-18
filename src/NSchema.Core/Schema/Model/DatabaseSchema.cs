using System.Diagnostics;

namespace NSchema.Schema.Model;

/// <summary>
/// Represents the overall structure of a database schema.
/// </summary>
/// <param name="Schemas">A list of SchemaDefinition objects, each representing a specific schema within the database.</param>
/// <param name="DroppedSchemas">A list of schema names that have been dropped from the database.</param>
/// <param name="Extensions">A list of database-global extensions.</param>
/// <param name="DroppedExtensions">A list of extension names that have been dropped from the database.</param>
[DebuggerDisplay("{Schemas.Count} schemas")]
public record DatabaseSchema(
    IReadOnlyList<SchemaDefinition>? Schemas = null,
    IReadOnlyList<string>? DroppedSchemas = null,
    IReadOnlyList<Extension>? Extensions = null,
    IReadOnlyList<string>? DroppedExtensions = null
)
{
    /// <summary>
    /// A list of SchemaDefinition objects, each representing a specific schema within the database.
    /// </summary>
    public IReadOnlyList<SchemaDefinition> Schemas { get; init; } = Schemas ?? [];

    /// <summary>
    /// A list of schema names that have been dropped from the database.
    /// </summary>
    public IReadOnlyList<string> DroppedSchemas { get; init; } = DroppedSchemas ?? [];

    /// <summary>
    /// A list of database-global extensions. Extensions are not schema-scoped, so they live at the root of the
    /// database schema rather than inside a <see cref="SchemaDefinition"/>.
    /// </summary>
    public IReadOnlyList<Extension> Extensions { get; init; } = Extensions ?? [];

    /// <summary>
    /// A list of extension names that have been dropped from the database.
    /// </summary>
    public IReadOnlyList<string> DroppedExtensions { get; init; } = DroppedExtensions ?? [];

    /// <summary>
    /// Gets a combined list of all schema names, including both existing schemas and those that have been dropped.
    /// </summary>
    public string[] AllSchemaNames => Schemas
        .Select(s => s.Name)
        .Concat(DroppedSchemas)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// Restricts the schema to the named schemas.
    /// </summary>
    /// <param name="schemaNames">The names of the schemas to include in the filtered result.</param>
    /// <returns>A new <see cref="DatabaseSchema"/> containing only the schemas and dropped schemas that match the provided names.</returns>
    public DatabaseSchema Filter(string[]? schemaNames)
    {
        // Extensions are database-global, not schema-scoped, so they pass through a namespace filter untouched:
        // an extension is a prerequisite of the whole database regardless of which schemas are in scope.
        if (schemaNames is not { Length: > 0 })
        {
            return new DatabaseSchema(Schemas, DroppedSchemas, Extensions, DroppedExtensions);
        }

        var scope = new HashSet<string>(schemaNames, StringComparer.OrdinalIgnoreCase);
        var filtered = Schemas.Where(s => scope.Contains(s.Name)).ToList();
        var filteredDropped = DroppedSchemas.Where(scope.Contains).ToList();
        return new DatabaseSchema(filtered, filteredDropped, Extensions, DroppedExtensions);
    }

    /// <summary>
    /// Combines the current <see cref="DatabaseSchema"/> with another.
    /// </summary>
    /// <param name="schema">The schema to combine with.</param>
    /// <returns></returns>
    public DatabaseSchema Combine(DatabaseSchema schema)
    {
        var mergedSchemas = new[] { this, schema }
            .SelectMany(db => db.Schemas)
            .GroupBy(s => s.Name)
            .Select(s => AggregateSchemaGroup(s.ToList()))
            .ToList();

        var droppedSchemas = DroppedSchemas
            .Concat(schema.DroppedSchemas)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Extensions are database-global, so they aggregate at the root (not per schema). A name declared by
        // more than one source is a conflict, mirroring how duplicate tables/enums are rejected.
        var extensions = new List<Extension>();
        var seenExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in Extensions.Concat(schema.Extensions))
        {
            if (!seenExtensions.Add(extension.Name))
            {
                throw new InvalidOperationException($"Duplicate extension '{extension.Name}' declared.");
            }

            extensions.Add(extension);
        }

        var droppedExtensions = DroppedExtensions
            .Concat(schema.DroppedExtensions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DatabaseSchema(mergedSchemas, droppedSchemas, extensions, droppedExtensions);
    }

    private static SchemaDefinition AggregateSchemaGroup(IReadOnlyList<SchemaDefinition> schemas)
    {
        var tables = new List<Table>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var schemaName = schemas[0].Name;
        foreach (var schema in schemas)
        {
            foreach (var table in schema.Tables)
            {
                if (!seen.Add(table.Name))
                {
                    throw new InvalidOperationException($"Duplicate table '{table.Name}' found in schema '{schemaName}'.");
                }

                tables.Add(table);
            }
        }

        var views = new List<View>();
        var seenViews = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var schema in schemas)
        {
            foreach (var view in schema.Views)
            {
                if (!seenViews.Add(view.Name))
                {
                    throw new InvalidOperationException($"Duplicate view '{view.Name}' found in schema '{schemaName}'.");
                }

                views.Add(view);
            }
        }

        var enums = new List<EnumType>();
        var seenEnums = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var schema in schemas)
        {
            foreach (var enumType in schema.Enums)
            {
                if (!seenEnums.Add(enumType.Name))
                {
                    throw new InvalidOperationException($"Duplicate enum '{enumType.Name}' found in schema '{schemaName}'.");
                }

                enums.Add(enumType);
            }
        }

        var sequences = new List<Sequence>();
        var seenSequences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var schema in schemas)
        {
            foreach (var sequence in schema.Sequences)
            {
                if (!seenSequences.Add(sequence.Name))
                {
                    throw new InvalidOperationException($"Duplicate sequence '{sequence.Name}' found in schema '{schemaName}'.");
                }

                sequences.Add(sequence);
            }
        }

        // Functions and procedures share one name pool, as they do in the database (e.g. Postgres's pg_proc):
        // a function and a procedure with the same name cannot coexist in a schema.
        var functions = new List<Function>();
        var procedures = new List<Procedure>();
        var seenRoutines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var schema in schemas)
        {
            foreach (var function in schema.Functions)
            {
                if (!seenRoutines.Add(function.Name))
                {
                    throw new InvalidOperationException(
                        $"Duplicate routine '{function.Name}' found in schema '{schemaName}': functions and procedures share one name space.");
                }

                functions.Add(function);
            }

            foreach (var procedure in schema.Procedures)
            {
                if (!seenRoutines.Add(procedure.Name))
                {
                    throw new InvalidOperationException(
                        $"Duplicate routine '{procedure.Name}' found in schema '{schemaName}': functions and procedures share one name space.");
                }

                procedures.Add(procedure);
            }
        }

        var comments = schemas.Select(s => s.Comment).Where(c => c is not null).Distinct().ToList();
        if (comments.Count > 1)
        {
            throw new InvalidOperationException($"Conflicting comments specified for schema '{schemaName}'.");
        }
        var comment = comments.FirstOrDefault();

        var isPartial = schemas.Any(s => s.IsPartial);
        var droppedTables = schemas
            .SelectMany(s => s.DroppedTables)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var droppedViews = schemas
            .SelectMany(s => s.DroppedViews)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var droppedEnums = schemas
            .SelectMany(s => s.DroppedEnums)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var droppedSequences = schemas
            .SelectMany(s => s.DroppedSequences)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var droppedFunctions = schemas
            .SelectMany(s => s.DroppedFunctions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var droppedProcedures = schemas
            .SelectMany(s => s.DroppedProcedures)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var oldNames = schemas.Select(s => s.OldName).Where(n => n is not null).Distinct().ToList();
        if (oldNames.Count > 1)
        {
            throw new InvalidOperationException($"Conflicting old names specified for schema '{schemaName}'.");
        }
        var oldName = oldNames.FirstOrDefault();

        var grants = schemas
            .SelectMany(s => s.Grants)
            .Distinct()
            .ToList();

        return new SchemaDefinition(
            schemaName, oldName, isPartial, comment, tables, droppedTables, grants, views, droppedViews,
            enums, droppedEnums, sequences, droppedSequences,
            functions, droppedFunctions, procedures, droppedProcedures);
    }
}
