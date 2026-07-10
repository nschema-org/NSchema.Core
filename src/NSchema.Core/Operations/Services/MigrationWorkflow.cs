using NSchema.Diagnostics;
using NSchema.Operations.Progress;
using NSchema.Plan;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Sql.Model;
using NSchema.State.Model;
using NSchema.State.Storage;

namespace NSchema.Operations.Services;

internal sealed class MigrationWorkflow(
    IMigrationPlanner planner,
    IProgress<OperationProgress> progress,
    ICurrentSchemaProvider currentProvider,
    IDesiredSchemaProvider desiredProvider,
    ISchemaStateManager stateManager
) : IMigrationWorkflow
{
    public async Task<PolicyDiagnostics> Validate(CancellationToken cancellationToken = default)
    {
        progress.Report(OperationProgress.Step("Loading desired schema..."));
        var desired = await desiredProvider.GetProject(null, cancellationToken);
        ReportDesiredDetail(desired);

        progress.Report(OperationProgress.Step("Validating schema..."));

        // The findings — including any non-error advisories and findings raised while reading the DDL — are
        // returned as data for the caller to render.
        return new PolicyDiagnostics(desired.Diagnostics.Concat(planner.Validate(desired.Project.Schema)));
    }

    public async Task<Result<PlannedMigration>> ComputePlan(SchemaSourceMode currentSource, bool required, string[]? schemas, CancellationToken cancellationToken = default)
    {
        progress.Report(OperationProgress.Step("Loading desired schema..."));
        var desired = await desiredProvider.GetProject(schemas, cancellationToken);
        var schemasInScope = schemas ?? desired.Project.Schema.AllSchemaNames;
        ReportDesiredDetail(desired);

        progress.Report(OperationProgress.Step($"Migration will be scoped to the following schemas: {string.Join(", ", schemasInScope)}"));

        progress.Report(OperationProgress.Step("Loading provider schema..."));
        var currentSchema = await currentProvider.GetSchema(currentSource, schemasInScope, required, cancellationToken);
        progress.Report(OperationProgress.Detail($"Current schema ({currentSource.ToString().ToLowerInvariant()}): {StatusHelpers.Describe(currentSchema)}."));

        var readDiagnostics = new List<Diagnostic>(desired.Diagnostics);
        if (!stateManager.IsConfigured && desired.Project.HasRunOnceScripts)
        {
            readDiagnostics.Add(Diagnostic.Warning("run-once", "Run-once scripts require a state store to record executions; without one they run on every apply."));
        }

        progress.Report(OperationProgress.Step("Computing migration plan..."));

        var state = SchemaState.Empty;
        if (stateManager.IsConfigured)
        {
            var read = await stateManager.Read(new StateReadArguments(), cancellationToken);
            if (read.IsFailure)
            {
                return Result.Failure<PlannedMigration>(readDiagnostics.Concat(read.Diagnostics));
            }

            readDiagnostics.AddRange(read.Diagnostics);
            state = read.Value.State ?? SchemaState.Empty;
        }

        var scriptHashes = state.Scripts.Select(s => new ScriptHash(s.Name, s.Hash)).ToList();
        var current = new CurrentState(currentSchema, scriptHashes);
        var plan = planner.Plan(current, desired.Project);

        if (readDiagnostics.Count == 0)
        {
            return plan;
        }

        var diagnostics = readDiagnostics.Concat(plan.Diagnostics);
        return plan.Value is { } value
            ? Result.From(value, diagnostics)
            : Result.Failure<PlannedMigration>(diagnostics);
    }

    public async Task<Result<PlannedMigration>> ComputeTeardown(CancellationToken cancellationToken = default)
    {
        // The managed schema is what we tear down: recorded state when we have it, otherwise the declared desired schema.
        var managedSchema = stateManager.IsConfigured
            ? await currentProvider.GetSchema(SchemaSourceMode.Offline, null, required: true, cancellationToken)
            : (await desiredProvider.GetProject(null, cancellationToken)).Project.Schema;

        progress.Report(OperationProgress.Step("Computing teardown plan..."));
        return planner.PlanTeardown(managedSchema);
    }

    public async Task<Result<StateCapture>?> Refresh(SqlPlan? applied = null, bool force = false, CancellationToken cancellationToken = default)
    {
        if (!stateManager.IsConfigured)
        {
            return null;
        }

        progress.Report(OperationProgress.Step("Updating state store..."));
        var schema = await currentProvider.GetSchema(SchemaSourceMode.Online, null, required: true, cancellationToken);

        // Fetch the current state, apply this run's changes over it, and write the result back. A forced refresh
        // is the recovery path for an unreadable payload — it replaces it — but the replacement resets the
        // run-once ledger, so it must be asked for, and the capture flags it for the caller to surface.
        var read = await stateManager.Read(new StateReadArguments(), cancellationToken);
        if (read.IsFailure && !force)
        {
            return Result.Failure<StateCapture>(read.Diagnostics.Append(Diagnostic.Error("state",
                "The existing state payload was not replaced. Repair it with state pull/push, or re-run the " +
                "refresh with force to replace it and reset the run-once script ledger.")));
        }

        var state = read.Value?.State ?? SchemaState.Empty;

        state = state with { Schema = schema };
        if (applied is not null)
        {
            state = state.RecordScripts(applied.Scripts, DateTimeOffset.UtcNow);
        }

        var written = await stateManager.Write(new StateWriteArguments(state), cancellationToken);
        progress.Report(OperationProgress.Detail($"State snapshot: {StatusHelpers.Describe(schema)}, {written.Value!.PayloadSize:N0} bytes."));

        var diagnostics = Enumerable.Empty<Diagnostic>();
        if (read.IsFailure)
        {
            // The read errors explain what was wrong with the replaced payload, but the forced capture succeeded,
            // so they ride along demoted to warnings — error severity would flip the result to a failure.
            diagnostics = read.Diagnostics
                .Select(d => d with { Severity = DiagnosticSeverity.Warning })
                .Append(Diagnostic.Warning("state",
                    "The existing state payload could not be read and has been replaced; the run-once script ledger was " +
                    "reset. Untaint any run-once scripts that have already run, or they will run again on the next apply."
                ));
        }

        return Result.Success(new StateCapture(schema, written.Value.PayloadSize), diagnostics);
    }

    /// <summary>
    /// Emits the verbose detail about the loaded desired project: the files it was read from and a census of
    /// what they declared.
    /// </summary>
    private void ReportDesiredDetail(DesiredProjectResult desired)
    {
        if (desired.Files.Count > 0)
        {
            progress.Report(OperationProgress.Detail($"Read {StatusHelpers.Count(desired.Files.Count, "DDL file")}:"));
            foreach (var file in desired.Files)
            {
                progress.Report(OperationProgress.Detail(file));
            }
        }

        progress.Report(OperationProgress.Detail($"Desired schema: {StatusHelpers.Describe(desired.Project.Schema)}, " +
            $"{StatusHelpers.Count(desired.Project.Scripts.Count, "deployment script")}, {StatusHelpers.Count(desired.Project.Migrations.Count, "data migration")}."));
    }
}
