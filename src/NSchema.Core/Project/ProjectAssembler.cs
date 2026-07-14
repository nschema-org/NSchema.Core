using NSchema.Project.Ddl;
using NSchema.Project.Ddl.Models.Templates;
using NSchema.Project.Domain;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Templates;

namespace NSchema.Project;

/// <summary>
/// Assembles parsed documents into a cohesive <see cref="ProjectDefinition"/>.
/// </summary>
internal static class ProjectAssembler
{
    public static Result<ProjectDefinition> Assemble(IReadOnlyList<NsqlDocument> documents)
    {
        var diagnostics = new List<Diagnostic>();
        var schema = new DatabaseSchema();
        var scripts = new List<Script>();
        var schemaTemplates = new List<SchemaTemplateStatement>();
        var tableTemplates = new List<TableTemplateStatement>();
        var applications = new List<ApplyTemplateStatement>();
        var includes = new List<TemplateInclude>();

        foreach (var document in documents)
        {
            var accumulator = new SchemaAccumulator();
            var documentScripts = new List<Script>();
            try
            {
                foreach (var statement in document.Statements)
                {
                    switch (statement)
                    {
                        case SchemaTemplateStatement template:
                            // Projecting the body validates it (internal duplicates, stray-qualified
                            // declarations) at read time, whether or not the template is ever applied.
                            DocumentProjector.ProjectSchemaTemplate(template);
                            schemaTemplates.Add(template);
                            break;
                        case TableTemplateStatement template:
                            tableTemplates.Add(template);
                            break;
                        case ApplyTemplateStatement application:
                            applications.Add(application);
                            break;
                        case Nsql.Syntax.Config.ConfigStatement:
                            // Configuration is not project content; the config read seam interprets it.
                            break;
                        default:
                            DocumentProjector.ProjectStatement(statement, accumulator, documentScripts, context: null);
                            break;
                    }
                }
            }
            catch (DdlSyntaxException error)
            {
                // A same-file finding (a duplicate declaration, a broken template body) carries its position
                // and file structurally, like a parse error; the document's remainder is skipped.
                diagnostics.Add(NsqlDiagnostics.Syntax(error) with { File = document.FilePath });
                continue;
            }

            var merged = SchemaAggregator.Combine(schema, accumulator.Build());
            diagnostics.AddRange(merged.Diagnostics);
            schema = merged.Require();
            scripts.AddRange(documentScripts);
            includes.AddRange(accumulator.Includes);
        }

        // Apply all templates: application failures accumulate the same way.
        var expanded = TemplateExpander.Expand(new ProjectDefinition(schema, scripts), schemaTemplates, tableTemplates, applications, includes);
        diagnostics.AddRange(expanded.Diagnostics);
        var project = expanded.Require();

        // Collisions are project errors, so validate before scoping drops any instance — all of them at once.
        diagnostics.AddRange(ValidateChangeTargets(project.Scripts));
        diagnostics.AddRange(ValidateScriptNames(project.Scripts));

        return Result.From(project, diagnostics);
    }

    /// <summary>
    /// Enforces project-wide script-name uniqueness (after template expansion, so instantiated names count).
    /// Names identify scripts in run-once tracking and diagnostics, so a collision is an error, not a merge.
    /// </summary>
    private static IEnumerable<Diagnostic> ValidateScriptNames(IEnumerable<Script> scripts)
    {
        var names = new HashSet<SqlIdentifier>();
        foreach (var name in scripts.Select(s => s.Name))
        {
            if (!names.Add(name))
            {
                yield return ProjectDiagnostics.DuplicateScriptName(name);
            }
        }
    }

    /// <summary>
    /// Rejects a second change-event script for the same trigger and path — two blocks preparing the same
    /// change is a conflict, whatever their names.
    /// </summary>
    private static IEnumerable<Diagnostic> ValidateChangeTargets(IEnumerable<Script> scripts)
    {
        var targets = new HashSet<(ChangeTrigger Trigger, SqlIdentifier? Schema, SqlIdentifier Table, SqlIdentifier Member)>();
        foreach (var change in scripts.Select(s => s.Event).OfType<ChangeEvent>())
        {
            if (!targets.Add((change.Trigger, change.ScopeSchema, change.TableName, change.MemberName)))
            {
                yield return ProjectDiagnostics.DuplicateChangeTarget(change);
            }
        }
    }
}
