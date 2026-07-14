using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Scripts;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Triggers;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Templates;

namespace NSchema.Project.Domain;

/// <summary>
/// Expands template applications during project assembly: a template instantiates by projecting its
/// statement nodes once per applied schema, with unqualified names binding to that schema at projection —
/// no placeholder rewriting. Includes resolve over the aggregate afterwards, so an instantiated table can
/// itself include a table template.
/// </summary>
internal static class TemplateExpander
{
    /// <summary>
    /// The schema name table-template members project against for include resolution; re-pointed at the
    /// including table's schema when the include merges.
    /// </summary>
    private static readonly SqlIdentifier IncludePlaceholder = SchemaToken.TargetSchemaPlaceholder;

    public static Result<ProjectDefinition> Expand(
        ProjectDefinition project,
        IReadOnlyList<SchemaTemplateStatement> schemaTemplates,
        IReadOnlyList<TableTemplateStatement> tableTemplates,
        IReadOnlyList<ApplyTemplateStatement> applications,
        IReadOnlyList<TemplateInclude> includes
    )
    {
        var diagnostics = new List<Diagnostic>();
        var schema = project.Schema;
        var scripts = project.Scripts.ToList();

        // One name space across both kinds, as ever: a schema template and a table template cannot share a name.
        var byName = new Dictionary<SqlIdentifier, object>();
        foreach (var template in schemaTemplates.Cast<object>().Concat(tableTemplates))
        {
            var name = Name(template);
            if (!byName.TryAdd(name, template))
            {
                diagnostics.Add(TemplateDiagnostics.DuplicateTemplate(name));
            }
        }

        var pendingIncludes = includes.ToList();
        foreach (var application in applications)
        {
            var templateName = new SqlIdentifier(application.TemplateName.Value);
            if (!byName.TryGetValue(templateName, out var template))
            {
                diagnostics.Add(TemplateDiagnostics.UnknownTemplate(templateName));
                continue;
            }
            if (template is not SchemaTemplateStatement schemaTemplate)
            {
                diagnostics.Add(TemplateDiagnostics.AppliedTableTemplate(templateName));
                continue;
            }

            foreach (var schemaNameNode in application.Schemas)
            {
                var schemaName = new SqlIdentifier(schemaNameNode.Value);
                if (schema.Schemas.All(s => s.Name != schemaName))
                {
                    diagnostics.Add(TemplateDiagnostics.UnknownTargetSchema(templateName, schemaName));
                    continue;
                }

                var (instance, instanceIncludes, instanceScripts) = Instantiate(schemaTemplate, schemaName);

                // The merge rejects an object the target schema already declares, exactly as if the
                // instantiated objects had been written in the target schema by hand.
                var combined = SchemaAggregator.Combine(schema, new DatabaseSchema([instance]));
                if (combined.IsFailure)
                {
                    diagnostics.AddRange(combined.Diagnostics.Select(d =>
                        d with { Message = $"APPLY TEMPLATE '{templateName}' IN SCHEMA {schemaName}: {d.Message}" }));
                    continue;
                }
                schema = combined.Require();

                // The instance's own includes resolve with everything else below, so an instantiated table
                // can itself include a template.
                pendingIncludes.AddRange(instanceIncludes);
                scripts.AddRange(instanceScripts);
            }
        }

        schema = ResolveIncludes(schema, byName, pendingIncludes, diagnostics);

        return Result.From(new ProjectDefinition(schema, scripts), diagnostics);
    }

    private static SqlIdentifier Name(object template) => template switch
    {
        SchemaTemplateStatement s => new SqlIdentifier(s.Name.Value),
        TableTemplateStatement t => new SqlIdentifier(t.Name.Value),
        _ => throw new InvalidOperationException(),
    };

    /// <summary>
    /// Instantiates a schema template for one target schema: the body's statements project with the target as
    /// their binding context, then the template's own declarations qualify unqualified type and trigger-function
    /// references (an unqualified name the template does not declare is left alone — a built-in type, or a name
    /// the database resolves by search path), and scripts scope to the instance with the <c>{schema}</c> token
    /// substituted.
    /// </summary>
    private static (SchemaDefinition Instance, IReadOnlyList<TemplateInclude> Includes, IReadOnlyList<Script> Scripts)
        Instantiate(SchemaTemplateStatement template, SqlIdentifier schemaName)
    {
        var body = new SchemaAccumulator();
        var scripts = new List<Script>();
        foreach (var statement in template.Statements)
        {
            DocumentProjector.ProjectStatement(statement, body, scripts, schemaName);
        }

        var fragment = body.Build();
        var instance = Qualify(fragment.Schemas.SingleOrDefault() ?? new SchemaDefinition(schemaName), schemaName);

        var instanceScripts = scripts.Select(script => script with
        {
            Sql = SchemaToken.Instantiate(script.Sql, schemaName),
            Event = script.Event with { ScopeSchema = schemaName },
        }).ToList();

        return (instance, body.Includes, instanceScripts);
    }

    /// <summary>
    /// Qualifies unqualified references to what the instance itself declares: a column, domain, or composite
    /// field whose type the template declares points at the instance schema, as does a trigger function it
    /// declares. Object names already bound at projection.
    /// </summary>
    private static SchemaDefinition Qualify(SchemaDefinition instance, SqlIdentifier schemaName)
    {
        var declaredTypes = instance.Enums.Select(e => e.Name)
            .Concat(instance.Domains.Select(d => d.Name))
            .Concat(instance.CompositeTypes.Select(t => t.Name))
            .ToHashSet();
        var declaredRoutines = instance.Routines.Select(r => r.Name).ToHashSet();

        return instance with
        {
            Tables = instance.Tables.Select(table => table with
            {
                Columns = table.Columns.Select(c => c with { Type = Qualify(c.Type, declaredTypes, schemaName) }).ToList(),
                Triggers = table.Triggers.Select(t => Qualify(t, declaredRoutines, schemaName)).ToList(),
            }).ToList(),
            Domains = instance.Domains.Select(d => d with { DataType = Qualify(d.DataType, declaredTypes, schemaName) }).ToList(),
            CompositeTypes = instance.CompositeTypes
                .Select(t => t with { Fields = t.Fields.Select(f => f with { DataType = Qualify(f.DataType, declaredTypes, schemaName) }).ToList() })
                .ToList(),
        };
    }

    private static SqlType Qualify(SqlType type, HashSet<SqlIdentifier> declaredTypes, SqlIdentifier schemaName)
    {
        if (type.Name.Contains('.'))
        {
            return type; // explicitly qualified — escapes the template
        }

        // A custom type may carry its facets in the name text; only the base name identifies it.
        var paren = type.Name.IndexOf('(');
        var baseName = paren < 0 ? type.Name : type.Name[..paren];
        if (!declaredTypes.Contains(new SqlIdentifier(baseName)))
        {
            return type;
        }

        // A fresh SqlType lower-cases the qualified name through its primary constructor (a with-expression would
        // bypass that normalization); the facet properties carry over.
        return new SqlType($"{schemaName}.{type.Name}") { Length = type.Length, Precision = type.Precision, Scale = type.Scale };
    }

    private static Trigger Qualify(Trigger trigger, HashSet<SqlIdentifier> declaredRoutines, SqlIdentifier schemaName)
        => trigger.Function is { Schema: null } function && declaredRoutines.Contains(function.Name)
            ? trigger with { Function = function with { Schema = schemaName } }
            : trigger;

    private static DatabaseSchema ResolveIncludes(
        DatabaseSchema schema,
        Dictionary<SqlIdentifier, object> templates,
        List<TemplateInclude> includes,
        List<Diagnostic> diagnostics)
    {
        if (includes.Count == 0)
        {
            return schema;
        }

        var byTable = includes
            .GroupBy(i => (i.SchemaName, i.TableName))
            .ToDictionary(g => g.Key, g => g.ToList());

        var members = new Dictionary<SqlIdentifier, Table>();

        var consumed = new HashSet<(SqlIdentifier Schema, SqlIdentifier Table)>();
        var resolved = schema.Schemas
            .Select(definition => definition with
            {
                Tables = definition.Tables
                    .Select(table =>
                    {
                        var key = (definition.Name, table.Name);
                        if (!byTable.TryGetValue(key, out var tableIncludes))
                        {
                            return table;
                        }
                        consumed.Add(key);
                        return MergeIncludes(definition.Name, table, tableIncludes, templates, members, diagnostics);
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

    /// <summary>
    /// Merges each included table template's members into <paramref name="table"/>: columns land at the position
    /// the include was written, foreign keys referencing the include placeholder re-point at the including table's
    /// schema, and everything else appends. A member the table already declares is rejected.
    /// </summary>
    private static Table MergeIncludes(SqlIdentifier schemaName, Table table, List<TemplateInclude> includes,
        Dictionary<SqlIdentifier, object> templates, Dictionary<SqlIdentifier, Table> memberCache, List<Diagnostic> diagnostics)
    {
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
                diagnostics.Add(TemplateDiagnostics.IncludeUnknownTemplate(schemaName, table.Name, include.TemplateName));
                continue;
            }
            if (template is not TableTemplateStatement tableTemplate)
            {
                diagnostics.Add(TemplateDiagnostics.IncludedSchemaTemplate(schemaName, table.Name, include.TemplateName));
                continue;
            }

            if (!memberCache.TryGetValue(include.TemplateName, out var members))
            {
                // The members project once against the placeholder; placeholder references (an unqualified
                // REFERENCES in the body) re-point per including table below.
                (members, _) = DocumentProjector.ProjectTableMembers(IncludePlaceholder, null, tableTemplate.Members);
                memberCache[include.TemplateName] = members;
            }

            // Validate before merging anything, so a conflicted include is skipped whole rather than half-applied.
            var conflicts = new List<Diagnostic>();
            foreach (var column in members.Columns)
            {
                if (columns.Any(c => c.Name == column.Name))
                {
                    conflicts.Add(TemplateDiagnostics.IncludeColumnConflict(include.TemplateName, column.Name, schemaName, table.Name));
                }
            }
            if (members.PrimaryKey is not null && primaryKey is not null)
            {
                conflicts.Add(TemplateDiagnostics.IncludePrimaryKeyConflict(include.TemplateName, schemaName, table.Name));
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
                fk.ReferencedSchema == IncludePlaceholder
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
}
