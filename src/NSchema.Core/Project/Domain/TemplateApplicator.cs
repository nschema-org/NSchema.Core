using NSchema.Project.Ddl.Models.Templates;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Scripts;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Triggers;

namespace NSchema.Project.Domain;

/// <summary>
/// Applies template applications to a project, merging the instantiated objects and scripts in.
/// </summary>
internal static class TemplateApplicator
{
    /// <summary>
    /// Expands templates into a given schema and returns the result.
    /// </summary>
    public static Result<ProjectDefinition> Apply(ProjectDefinition project, TemplateSet templates)
    {
        var diagnostics = new List<Diagnostic>();
        var schema = project.Schema;
        var scripts = project.Scripts.ToList();

        var byName = new Dictionary<string, TemplateDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in templates.Definitions)
        {
            if (!byName.TryAdd(template.Name, template))
            {
                diagnostics.Add(TemplateDiagnostics.DuplicateTemplate(template.Name));
            }
        }

        var pendingIncludes = templates.Includes.ToList();
        foreach (var application in templates.Applications)
        {
            if (!byName.TryGetValue(application.TemplateName, out var template))
            {
                diagnostics.Add(TemplateDiagnostics.UnknownTemplate(application.TemplateName));
                continue;
            }
            if (template.Kind != TemplateKind.Schema)
            {
                diagnostics.Add(TemplateDiagnostics.AppliedTableTemplate(template.Name));
                continue;
            }

            foreach (var schemaName in application.SchemaNames)
            {
                if (!schema.Schemas.Any(s => string.Equals(s.Name, schemaName, StringComparison.OrdinalIgnoreCase)))
                {
                    diagnostics.Add(TemplateDiagnostics.UnknownTargetSchema(template.Name, schemaName));
                    continue;
                }

                // The merge rejects an object the target schema already declares, exactly as if the
                // instantiated objects had been written in the target schema by hand.
                var combined = SchemaAggregator.Combine(schema, new DatabaseSchema([Apply(template, schemaName)]));
                if (combined.IsFailure)
                {
                    diagnostics.AddRange(combined.Diagnostics.Select(d =>
                        d with { Message = $"APPLY TEMPLATE '{template.Name}' IN SCHEMA {schemaName}: {d.Message}" }));
                    continue;
                }
                schema = combined.Require();

                // The template's own includes re-target from the placeholder to this instance's schema and
                // resolve with everything else below, so an instantiated table can itself include a template.
                pendingIncludes.AddRange(template.Includes.Select(i => i with { SchemaName = schemaName }));

                scripts.AddRange(template.Scripts.Select(s => Instantiate(s, schemaName)));
            }
        }

        schema = ResolveIncludes(schema, byName, pendingIncludes, diagnostics);

        return Result.From(new ProjectDefinition(schema, scripts), diagnostics);
    }

    /// <summary>
    /// Instantiates a template script for <paramref name="schemaName"/>: the <c>{schema}</c> token substitutes in
    /// the name and SQL body, and the instance is scoped to the applied schema (for a change event that also
    /// re-homes its target path — the scope is the target's schema).
    /// </summary>
    private static Script Instantiate(Script script, string schemaName) => script with
    {
        Name = script.Name.Replace("{schema}", schemaName, StringComparison.Ordinal),
        Sql = script.Sql.Replace("{schema}", schemaName, StringComparison.Ordinal),
        Event = script.Event with { ScopeSchema = schemaName },
    };

    private static DatabaseSchema ResolveIncludes(
        DatabaseSchema schema,
        Dictionary<string, TemplateDefinition> templates,
        List<TemplateInclude> includes,
        List<Diagnostic> diagnostics)
    {
        if (includes.Count == 0)
        {
            return schema;
        }

        var byTable = includes
            .GroupBy(i => Key(i.SchemaName, i.TableName))
            .ToDictionary(g => g.Key, g => g.ToList());

        var consumed = new HashSet<string>();
        var resolved = schema.Schemas
            .Select(definition => definition with
            {
                Tables = definition.Tables
                    .Select(table =>
                    {
                        var key = Key(definition.Name, table.Name);
                        if (!byTable.TryGetValue(key, out var tableIncludes))
                        {
                            return table;
                        }
                        consumed.Add(key);
                        return MergeIncludes(definition.Name, table, tableIncludes, templates, diagnostics);
                    })
                    .ToList(),
            })
            .ToList();

        // Parsed includes always name the table whose body they were written in, so a dangling one can only come
        // from a hand-built document; fail rather than drop it silently.
        foreach (var dangling in byTable.Keys.Where(key => !consumed.Contains(key)))
        {
            var include = byTable[dangling][0];
            diagnostics.Add(TemplateDiagnostics.IncludeUnknownTable(include.TemplateName, include.SchemaName, include.TableName));
        }

        return schema with { Schemas = resolved };
    }

    // The NUL character cannot appear in an identifier, so it is a safe composite-key separator.
    private static string Key(string schema, string table) => $"{schema.ToLowerInvariant()}\0{table.ToLowerInvariant()}";

    /// <summary>
    /// Merges each included table template's members into <paramref name="table"/>: columns land at the position
    /// the include was written, foreign keys referencing the placeholder re-point at the including table's schema,
    /// and everything else appends. A member the table already declares is rejected.
    /// </summary>
    private static Table MergeIncludes(string schemaName, Table table, List<TemplateInclude> includes, Dictionary<string, TemplateDefinition> templates, List<Diagnostic> diagnostics)
    {
        var qualified = $"{schemaName}.{table.Name}";
        var columns = table.Columns.ToList();
        var foreignKeys = table.ForeignKeys.ToList();
        var uniqueConstraints = table.UniqueConstraints.ToList();
        var checkConstraints = table.CheckConstraints.ToList();
        var exclusionConstraints = table.ExclusionConstraints.ToList();
        var indexes = table.Indexes.ToList();
        var primaryKey = table.PrimaryKey;

        var offset = 0;
        foreach (var include in includes)
        {
            if (!templates.TryGetValue(include.TemplateName, out var template))
            {
                diagnostics.Add(TemplateDiagnostics.IncludeUnknownTemplate(qualified, include.TemplateName));
                continue;
            }
            if (template.Kind != TemplateKind.Table)
            {
                diagnostics.Add(TemplateDiagnostics.IncludedSchemaTemplate(qualified, template.Name));
                continue;
            }

            var members = template.Objects.Tables.Single();

            // Validate before merging anything, so a conflicted include is skipped whole rather than half-applied.
            var conflicts = new List<Diagnostic>();
            foreach (var column in members.Columns)
            {
                if (columns.Any(c => string.Equals(c.Name, column.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    conflicts.Add(TemplateDiagnostics.IncludeColumnConflict(template.Name, column.Name, qualified));
                }
            }
            if (members.PrimaryKey is not null && primaryKey is not null)
            {
                conflicts.Add(TemplateDiagnostics.IncludePrimaryKeyConflict(template.Name, qualified));
            }
            if (conflicts.Count > 0)
            {
                diagnostics.AddRange(conflicts);
                continue;
            }

            columns.InsertRange(include.ColumnPosition + offset, members.Columns);
            offset += members.Columns.Count;

            if (members.PrimaryKey is not null)
            {
                primaryKey = members.PrimaryKey;
            }

            foreignKeys.AddRange(members.ForeignKeys.Select(fk =>
                fk.ReferencedSchema == TemplateDefinition.TargetSchemaPlaceholder
                    ? fk with { ReferencedSchema = schemaName }
                    : fk));
            uniqueConstraints.AddRange(members.UniqueConstraints);
            checkConstraints.AddRange(members.CheckConstraints);
            exclusionConstraints.AddRange(members.ExclusionConstraints);
            indexes.AddRange(members.Indexes);
        }

        return table with
        {
            Columns = columns,
            PrimaryKey = primaryKey,
            ForeignKeys = foreignKeys,
            UniqueConstraints = uniqueConstraints,
            CheckConstraints = checkConstraints,
            ExclusionConstraints = exclusionConstraints,
            Indexes = indexes,
        };
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
