using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.Project.Model.Directives;
using NSchema.Project.Model.Services;
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
        var database = new Database();
        var schemaTemplates = new List<SchemaTemplateStatement>();
        var tableTemplates = new List<TableTemplateStatement>();
        var applications = new List<ApplyTemplateStatement>();
        var includes = new List<TemplateInclude>();
        var directives = new DirectiveCollector();

        foreach (var document in documents)
        {
            var accumulator = new DatabaseAccumulator();
            var documentDiagnostics = new List<NsqlDiagnostic>();
            foreach (var statement in document.Statements)
            {
                switch (statement)
                {
                    case SchemaTemplateStatement template:
                        // Validating the body (internal duplicates, stray-qualified declarations) at read
                        // time, whether or not the template is ever applied.
                        documentDiagnostics.AddRange(DocumentProjector.ValidateTemplateBody(template));
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
                    case var directive when directives.TryAdd(directive):
                        break;
                    default:
                        DocumentProjector.ProjectStatement(statement, accumulator, context: null);
                        break;
                }
            }

            // A same-file finding (a duplicate declaration, a broken template body) carries its position and
            // file structurally, like a parse error — and the document's healthy statements still aggregate.
            var fragment = accumulator.Build();
            documentDiagnostics.AddRange(accumulator.Diagnostics);
            diagnostics.AddRange(documentDiagnostics.Select(d => d with { File = document.FilePath }));

            var merged = DatabaseAggregator.Combine(database, fragment);
            diagnostics.AddRange(merged.Diagnostics);
            database = merged.Require();
            includes.AddRange(accumulator.Includes);
        }

        // Apply all templates: application failures accumulate the same way. A template instance's directives
        // (its scripts, its object renames/drops) come back scoped to their applied schema and merge with the
        // top-level ones, so the whole project's directives assemble in one collector.
        var expanded = TemplateExpander.Expand(database, schemaTemplates, tableTemplates, applications, includes);
        diagnostics.AddRange(expanded.Diagnostics);
        var (expandedDatabase, instanceDirectives) = expanded.Require();
        directives.Absorb(instanceDirectives);

        var built = directives.Build();
        var project = new ProjectDefinition(expandedDatabase, built);

        // Collisions are project errors, so validate before scoping drops any instance — all of them at once.
        diagnostics.AddRange(ValidateChangeTargets(built.ChangeScripts));
        diagnostics.AddRange(ValidateScriptNames(built));
        diagnostics.AddRange(DirectiveValidator.Validate(project));

        return Result.From(project, diagnostics);
    }

    /// <summary>
    /// Enforces script-address uniqueness per scope.
    /// </summary>
    private static IEnumerable<Diagnostic> ValidateScriptNames(ProjectDirectives directives)
    {
        var references = new HashSet<ScriptReference>();
        foreach (var reference in directives.ChangeScripts.Select(s => s.Reference).Concat(directives.DeploymentScripts.Select(s => s.Reference)))
        {
            if (!references.Add(reference))
            {
                yield return ProjectDiagnostics.DuplicateScriptName(reference);
            }
        }
    }

    /// <summary>
    /// Rejects a second change-event script for the same trigger and path — two blocks preparing the same
    /// change is a conflict, whatever their names.
    /// </summary>
    private static IEnumerable<Diagnostic> ValidateChangeTargets(IReadOnlyList<ChangeScript> scripts)
    {
        var targets = new HashSet<(ChangeTrigger Trigger, SqlIdentifier? Schema, SqlIdentifier Table, SqlIdentifier Member)>();
        foreach (var change in scripts)
        {
            if (!targets.Add((change.Trigger, change.ScopeSchema, change.TableName, change.MemberName)))
            {
                yield return ProjectDiagnostics.DuplicateChangeTarget(change);
            }
        }
    }
}
