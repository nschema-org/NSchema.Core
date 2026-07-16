using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Triggers;
using NSchema.Project.Domain.Models;
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
    /// One schema template instantiated for one applied schema: the instance objects, the includes its body
    /// carried, and the directives it bound to the applied schema.
    /// </summary>
    private sealed record TemplateInstance(Schema Schema, IReadOnlyList<TemplateInclude> Includes, ProjectDirectives Directives);

    public static Result<(Database Database, ProjectDirectives Directives)> Expand(
        Database database,
        IReadOnlyList<SchemaTemplateStatement> schemaTemplates,
        IReadOnlyList<TableTemplateStatement> tableTemplates,
        IReadOnlyList<ApplyTemplateStatement> applications,
        IReadOnlyList<TemplateInclude> includes
    )
    {
        var diagnostics = new List<Diagnostic>();

        // Each successful instance's directives (its scripts, and object renames/drops) accumulate here,
        // scoped to their applied schema, and ride back for the assembler to merge with the top-level ones.
        var instanceDirectives = new DirectiveCollector();

        // One name space across both kinds, as ever: a schema template and a table template cannot share a name.
        var byName = new Dictionary<SqlIdentifier, TemplateStatement>();
        foreach (var template in schemaTemplates.Cast<TemplateStatement>().Concat(tableTemplates))
        {
            var name = new SqlIdentifier(template.Name.Value);
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
                if (database.Schemas.All(s => s.Name != schemaName))
                {
                    diagnostics.Add(TemplateDiagnostics.UnknownTargetSchema(templateName, schemaName));
                    continue;
                }

                var instance = Instantiate(schemaTemplate, schemaName);

                // The merge rejects an object the target schema already declares, exactly as if the
                // instantiated objects had been written in the target schema by hand.
                var combined = DatabaseAggregator.Combine(database, new Database([instance.Schema]));
                if (combined.IsFailure)
                {
                    diagnostics.AddRange(combined.Diagnostics.Select(d =>
                        d with { Message = $"APPLY TEMPLATE '{templateName}' IN SCHEMA {schemaName}: {d.Message}" }));
                    continue;
                }
                database = combined.Require();

                // The instance's own includes resolve with everything else below, so an instantiated table
                // can itself include a template. Its directives only count once the instance itself lands.
                pendingIncludes.AddRange(instance.Includes);
                instanceDirectives.Absorb(instance.Directives);
            }
        }

        var resolver = new IncludeResolver(byName);
        database = resolver.Resolve(database, pendingIncludes);
        diagnostics.AddRange(resolver.Diagnostics);

        return Result.From<(Database, ProjectDirectives)>((database, instanceDirectives.Build()), diagnostics);
    }

    /// <summary>
    /// Instantiates a schema template for one target schema: the body's declarations project with the target as
    /// their binding context (then unqualified type and trigger-function references the template itself declares
    /// are qualified to it), while its directives — scripts, and object renames/drops — bind to the applied
    /// schema through the collector, the <c>{schema}</c> token substituted in script bodies.
    /// </summary>
    private static TemplateInstance Instantiate(SchemaTemplateStatement template, SqlIdentifier schemaName)
    {
        var body = new DatabaseAccumulator();
        var directives = new DirectiveCollector();
        foreach (var statement in template.Statements)
        {
            // Directives instantiate into the applied schema; everything else is a declaration of the instance.
            if (!directives.TryAdd(statement, schemaName))
            {
                DocumentProjector.ProjectStatement(statement, body, schemaName);
            }
        }

        var fragment = body.Build();
        var instance = Qualify(fragment.Schemas.SingleOrDefault() ?? new Schema(schemaName), schemaName);

        return new TemplateInstance(instance, body.Includes, directives.Build());
    }

    /// <summary>
    /// Qualifies unqualified references to what the instance itself declares: a column, domain, or composite
    /// field whose type the template declares points at the instance schema, as does a trigger function it
    /// declares. Object names already bound at projection.
    /// </summary>
    private static Schema Qualify(Schema instance, SqlIdentifier schemaName)
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
}
