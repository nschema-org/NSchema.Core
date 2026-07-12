using NSchema.Current;
using NSchema.Current.Domain.Models;
using NSchema.Current.Storage;
using NSchema.Diff.Domain;
using NSchema.Operations.Progress;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Models;
using NSchema.Project;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Operations.Workflow;

internal sealed class MigrationWorkflow(
    IMigrationPlanner planner,
    IProgress<OperationProgress> progress,
    ICurrentSchemaProvider currentProvider,
    IProjectProvider desiredProvider,
    ISchemaStateManager stateManager
) : IMigrationWorkflow
{
    public async Task<Result> Validate(CancellationToken cancellationToken = default)
    {
        progress.Report(OperationProgress.Step("Loading desired schema..."));
        var desired = await desiredProvider.GetProject(null, cancellationToken);
        if (desired.Value is not { } project)
        {
            return Result.From(desired.Diagnostics);
        }
        ReportDesiredDetail(project);

        progress.Report(OperationProgress.Step("Validating schema..."));

        // The findings — including any non-error advisories and findings raised while reading the DDL — are
        // returned as data for the caller to render.
        return Result.From(desired.Diagnostics.Concat(planner.Validate(project).Diagnostics));
    }

    public async Task<Result<MigrationPlan>> ComputePlan(SchemaSourceMode currentSource, string[]? schemas, CancellationToken cancellationToken = default)
    {
        // The diff is computed against CurrentState — the schema plus the run-once ledger — so planning without
        // a store would plan against knowingly incomplete current state.
        if (!stateManager.IsConfigured)
        {
            return Result.Failure<MigrationPlan>(WorkflowDiagnostics.StoreRequiredForPlanning);
        }

        progress.Report(OperationProgress.Step("Loading desired schema..."));
        var desired = await desiredProvider.GetProject(schemas, cancellationToken);
        if (desired.Value is not { } project)
        {
            return Result.Failure<MigrationPlan>(desired.Diagnostics);
        }
        var schemasInScope = schemas ?? project.Schema.AllSchemaNames;
        ReportDesiredDetail(project);

        progress.Report(OperationProgress.Step($"Migration will be scoped to the following schemas: {string.Join(", ", schemasInScope)}"));

        progress.Report(OperationProgress.Step("Loading provider schema..."));
        var currentSchema = await currentProvider.GetSchema(currentSource, schemasInScope, required: true, cancellationToken);
        progress.Report(OperationProgress.Detail($"Current schema ({currentSource.ToString().ToLowerInvariant()}): {StatusHelpers.Describe(currentSchema)}."));

        var readDiagnostics = new List<Diagnostic>(desired.Diagnostics);

        progress.Report(OperationProgress.Step("Computing migration plan..."));

        var read = await stateManager.Read(new StateReadArguments(), cancellationToken);
        if (read.IsFailure)
        {
            return Result.Failure<MigrationPlan>(readDiagnostics.Concat(read.Diagnostics));
        }

        readDiagnostics.AddRange(read.Diagnostics);
        var state = read.Value.State ?? SchemaState.Empty;

        var current = new CurrentState(currentSchema, state.Scripts);
        var plan = planner.Plan(current, project);

        if (readDiagnostics.Count == 0)
        {
            return plan;
        }

        var diagnostics = readDiagnostics.Concat(plan.Diagnostics);
        return plan.Value is { } value
            ? Result.From(value, diagnostics)
            : Result.Failure<MigrationPlan>(diagnostics);
    }

    public async Task<Result<MigrationPlan>> ComputeTeardown(CancellationToken cancellationToken = default)
    {
        if (!stateManager.IsConfigured)
        {
            return Result.Failure<MigrationPlan>(WorkflowDiagnostics.StoreRequiredForPlanning);
        }

        // The managed schema is the recorded state — state is the record of what NSchema manages, so a teardown
        // reads it and nothing else. An empty record means nothing is managed, and the teardown is empty.
        var managedSchema = await currentProvider.GetSchema(SchemaSourceMode.Offline, null, required: true, cancellationToken);

        progress.Report(OperationProgress.Step("Computing teardown plan..."));
        return planner.PlanTeardown(managedSchema);
    }

    public async Task<Result<StateCapture>> Refresh(MigrationPlan? applied = null, bool force = false, CancellationToken cancellationToken = default)
    {
        if (!stateManager.IsConfigured)
        {
            return Result.Failure<StateCapture>(WorkflowDiagnostics.StoreRequiredForRefresh);
        }

        progress.Report(OperationProgress.Step("Updating state store..."));
        var schema = await currentProvider.GetSchema(SchemaSourceMode.Online, null, required: true, cancellationToken);

        // Fetch the current state, apply this run's changes over it, and write the result back. A forced refresh
        // is the recovery path for an unreadable payload — it replaces it — but the replacement resets the
        // run-once ledger, so it must be asked for, and the capture flags it for the caller to surface.
        var read = await stateManager.Read(new StateReadArguments(), cancellationToken);
        if (read.IsFailure && !force)
        {
            return Result.Failure<StateCapture>(read.Diagnostics.Append(WorkflowDiagnostics.StateNotReplaced));
        }

        var state = read.Value?.State ?? SchemaState.Empty;

        state = state with { Schema = schema };
        if (applied is not null)
        {
            // Only run-once executions enter the ledger; the plan's diff carries its scripts whole, so the
            // execution records derive here at the state boundary.
            var executed = applied.Diff.Scripts
                .Where(s => s.RunCondition == RunCondition.Once)
                .Select(s => new ScriptExecution(s.Name, s.Hash, DateTimeOffset.UtcNow))
                .ToList();
            state = state.RecordExecution(executed);
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
    /// Emits the verbose census of what the loaded project declares.
    /// </summary>
    private void ReportDesiredDetail(ProjectDefinition project)
    {
        progress.Report(OperationProgress.Detail($"Desired schema: {StatusHelpers.Describe(project.Schema)}, " +
            $"{StatusHelpers.Count(project.Scripts.Count, "script")}."));
    }
}
