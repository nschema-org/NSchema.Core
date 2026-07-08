using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Templates;
using NSchema.Schema.Model.Triggers;

namespace NSchema.Schema;

/// <summary>
/// Expands template applications into concrete schema objects.
/// </summary>
internal static class TemplateExpander
{
    /// <summary>
    /// Instantiates every application's template into each of its target schemas, returning the expanded schema.
    /// </summary>
    public static DatabaseSchema Expand(DatabaseSchema schema, IReadOnlyList<TemplateDefinition> templates, IReadOnlyList<TemplateApplication> applications)
    {
        var byName = new Dictionary<string, TemplateDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in templates)
        {
            if (!byName.TryAdd(template.Name, template))
            {
                throw new InvalidOperationException($"Duplicate template '{template.Name}' declared.");
            }
        }

        foreach (var application in applications)
        {
            if (!byName.TryGetValue(application.TemplateName, out var template))
            {
                throw new InvalidOperationException($"APPLY TEMPLATE references unknown template '{application.TemplateName}'.");
            }

            foreach (var schemaName in application.SchemaNames)
            {
                if (!schema.Schemas.Any(s => string.Equals(s.Name, schemaName, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException(
                        $"APPLY TEMPLATE '{template.Name}' targets unknown schema '{schemaName}'; declare it with CREATE SCHEMA.");
                }

                // Combine performs the merge and rejects an object the target schema already declares, exactly as
                // if the instantiated objects had been written in the target schema by hand.
                schema = schema.Combine(new DatabaseSchema([Apply(template, schemaName)]));
            }
        }

        return schema;
    }

    /// <summary>
    /// Re-homes the template's objects into <paramref name="schemaName"/>: references to the placeholder schema
    /// (unqualified names in the body) re-point at the target, and an unqualified user-defined column type or
    /// trigger function qualifies to the target when the template itself declares it. An unqualified name the
    /// template does not declare is left alone — a built-in type, or a name the database resolves by search path.
    /// </summary>
    private static SchemaDefinition Apply(TemplateDefinition template, string schemaName)
    {
        var objects = template.Objects;

        var declaredTypes = objects.Enums.Select(e => e.Name)
            .Concat(objects.Domains.Select(d => d.Name))
            .Concat(objects.CompositeTypes.Select(t => t.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var declaredRoutines = objects.Routines.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return objects with
        {
            Name = schemaName,
            Tables = objects.Tables.Select(table => table with
            {
                Columns = table.Columns.Select(c => c with { Type = Qualify(c.Type, declaredTypes, schemaName) }).ToList(),
                ForeignKeys = table.ForeignKeys
                    .Select(fk => fk.ReferencedSchema == TemplateDefinition.TargetSchemaPlaceholder
                        ? fk with { ReferencedSchema = schemaName }
                        : fk)
                    .ToList(),
                Triggers = table.Triggers.Select(t => Qualify(t, declaredRoutines, schemaName)).ToList(),
            }).ToList(),
            Domains = objects.Domains.Select(d => d with { DataType = Qualify(d.DataType, declaredTypes, schemaName) }).ToList(),
            CompositeTypes = objects.CompositeTypes
                .Select(t => t with { Fields = t.Fields.Select(f => f with { DataType = Qualify(f.DataType, declaredTypes, schemaName) }).ToList() })
                .ToList(),
        };
    }

    private static SqlType Qualify(SqlType type, HashSet<string> declaredTypes, string schemaName)
    {
        if (type.Name.Contains('.'))
        {
            return type; // explicitly qualified — escapes the template
        }

        // A custom type may carry its facets in the name text; only the base name identifies it.
        var paren = type.Name.IndexOf('(');
        var baseName = paren < 0 ? type.Name : type.Name[..paren];
        if (!declaredTypes.Contains(baseName))
        {
            return type;
        }

        // A fresh SqlType lower-cases the qualified name through its primary constructor (a with-expression would
        // bypass that normalization); the facet properties carry over.
        return new SqlType($"{schemaName}.{type.Name}") { Length = type.Length, Precision = type.Precision, Scale = type.Scale };
    }

    private static Trigger Qualify(Trigger trigger, HashSet<string> declaredRoutines, string schemaName)
        => trigger.Function is { } function && !function.Contains('.') && declaredRoutines.Contains(function)
            ? trigger with { Function = $"{schemaName}.{function}" }
            : trigger;
}
