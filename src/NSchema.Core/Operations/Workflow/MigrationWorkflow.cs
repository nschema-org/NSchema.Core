using NSchema.Deployment;
using NSchema.Diff.Model.Services;
using NSchema.Model;
using NSchema.Operations.Progress;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Services;
using NSchema.Project;
using NSchema.Project.Model.Directives;
using NSchema.State;
using NSchema.State.Model;

namespace NSchema.Operations.Workflow;

internal sealed class MigrationWorkflow(
    IMigrationPlanner planner,
    IProgress<OperationProgress> progress,
    IDatabaseProvider databaseProvider,
    IProjectProvider projectProvider,
    IDatabaseStateManager stateManager
) : IMigrationWorkflow
{
    public async Task<Result> Validate(CancellationToken cancellationToken = default)
    {
        progress.Report(OperationProgress.Step("Loading desired schema..."));
        var projectResult = await projectProvider.GetProject(PlanningScope.All, cancellationToken);
        if (projectResult.Value is not { } project)
        {
            return Result.From(projectResult.Diagnostics);
        }
        ReportDesiredDetail(project);

        progress.Report(OperationProgress.Step("Validating schema..."));

        // The findings — including any non-error advisories and findings raised while reading the DDL — are
        // returned as data for the caller to render.
        return Result.From(projectResult.Diagnostics.Concat(planner.Validate(project).Diagnostics));
    }

    public async Task<Result<MigrationPlan>> ComputePlan(PlanTarget target, PlanningScope scope, CancellationToken cancellationToken = default)
    {
        // The diff is computed against CurrentState — the schema plus the run-once ledger — so planning without
        // a store would plan against knowingly incomplete current state.
        if (!stateManager.IsConfigured)
        {
            return Result.Failure<MigrationPlan>(WorkflowDiagnostics.StoreRequiredForPlanning);
        }

        var desired = await ResolveDesired(target, scope, cancellationToken);
        if (desired.Value is not { } project)
        {
            return Result.Failure<MigrationPlan>(desired.Diagnostics);
        }

        // An unrestricted run derives its scope from what it manages. A teardown declares nothing, so it stays
        // unrestricted and covers everything recorded.
        var scopeInEffect = scope.IsAll ? PlanningScope.Of(project.ManagedSchemaNames) : scope;

        progress.Report(OperationProgress.Step($"Migration will be scoped to the following schemas: {Describe(scopeInEffect)}"));

        // One state read serves both halves of the current side: the recorded database and the run-once ledger.
        var read = await stateManager.Read(new StateReadArguments(), cancellationToken);
        if (read.IsFailure)
        {
            return Result.Failure<MigrationPlan>(desired.Diagnostics.Concat(read.Diagnostics));
        }
        var state = read.Value.State ?? DatabaseState.Empty;

        // The current side is not narrowed here: scope applies to the difference, once computed. Filtering it
        // away first would hide the out-of-scope objects a scoped run may still disturb.
        // TODO: Move progress reporting down into the planner where the scoping is done?
        progress.Report(OperationProgress.Detail($"Current schema: {StatusHelpers.Describe(state.Database.ScopedTo(scopeInEffect))}."));

        var readDiagnostics = new List<Diagnostic>(desired.Diagnostics.Concat(read.Diagnostics));

        progress.Report(OperationProgress.Step("Computing migration plan..."));

        var current = new CurrentState(state.Database, state.Scripts);
        var plan = planner.Plan(current, project, scopeInEffect);

        if (readDiagnostics.Count == 0)
        {
            return plan;
        }

        var diagnostics = readDiagnostics.Concat(plan.Diagnostics);
        return plan.Value is { } value
            ? Result.From(value, diagnostics)
            : Result.Failure<MigrationPlan>(diagnostics);
    }

    public async Task<Result<StateCapture>> Refresh(MigrationPlan? applied = null, bool force = false, CancellationToken cancellationToken = default)
    {
        if (!stateManager.IsConfigured)
        {
            return Result.Failure<StateCapture>(WorkflowDiagnostics.StoreRequiredForRefresh);
        }

        progress.Report(OperationProgress.Step("Updating state store..."));
        var live = await databaseProvider.GetDatabase(PlanningScope.All, cancellationToken);
        if (live.Value is not { } schema)
        {
            return Result.Failure<StateCapture>(live.Diagnostics);
        }

        // Fetch the current state, apply this run's changes over it, and write the result back. A forced refresh
        // is the recovery path for an unreadable payload — it replaces it — but the replacement resets the
        // run-once ledger, so it must be asked for, and the capture flags it for the caller to surface.
        var read = await stateManager.Read(new StateReadArguments(), cancellationToken);
        if (read.IsFailure && !force)
        {
            return Result.Failure<StateCapture>(read.Diagnostics.Append(WorkflowDiagnostics.StateNotReplaced));
        }

        var state = read.Value?.State ?? DatabaseState.Empty;

        state = state with { Database = schema };
        if (applied is not null)
        {
            // Recording the run-once ledger is a state-domain job; the shell just supplies what ran and when.
            state = state.RecordRunOnce(applied.Diff.DeploymentScripts, DateTimeOffset.UtcNow);
        }

        var written = await stateManager.Write(new StateWriteArguments(state), cancellationToken);
        if (written.Value is not { } write)
        {
            return Result.Failure<StateCapture>(written.Diagnostics);
        }
        progress.Report(OperationProgress.Detail($"State snapshot: {StatusHelpers.Describe(schema)}, {write.PayloadSize:N0} bytes."));

        var diagnostics = Enumerable.Empty<Diagnostic>();
        if (read.IsFailure)
        {
            // The read errors explain what was wrong with the replaced payload, but the forced capture succeeded,
            // so they ride along demoted to warnings — error severity would flip the result to a failure.
            diagnostics = read.Diagnostics
                .Select(d => d with { Severity = DiagnosticSeverity.Warning })
                .Append(WorkflowDiagnostics.LedgerReset);
        }

        return Result.Success(new StateCapture(schema, write.PayloadSize), diagnostics);
    }

    /// <summary>
    /// Resolves the state being planned towards: the declared project, or nothing for a teardown.
    /// </summary>
    private async Task<Result<ProjectDefinition>> ResolveDesired(PlanTarget target, PlanningScope scope, CancellationToken cancellationToken)
    {
        if (target == PlanTarget.Empty)
        {
            return Result.Success(new ProjectDefinition(new Database()));
        }

        progress.Report(OperationProgress.Step("Loading desired schema..."));
        var desired = await projectProvider.GetProject(scope, cancellationToken);
        if (desired.Value is { } project)
        {
            ReportDesiredDetail(project);
        }

        return desired;
    }

    private static string Describe(PlanningScope scope) =>
        scope.SchemaNames is { } names ? string.Join(", ", names) : "(all)";

    /// <summary>
    /// Emits the verbose census of what the loaded project declares.
    /// </summary>
    private void ReportDesiredDetail(ProjectDefinition project)
    {
        var scriptCount = project.Directives.DeploymentScripts.Count + project.Directives.Tables.ChangeScripts.Count;
        progress.Report(OperationProgress.Detail($"Desired schema: {StatusHelpers.Describe(project.Database)}, " +
            $"{StatusHelpers.Count(scriptCount, "script")}."));
    }
}
