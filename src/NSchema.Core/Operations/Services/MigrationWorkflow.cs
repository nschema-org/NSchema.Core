using NSchema.Diagnostics;
using NSchema.Operations.Progress;
using NSchema.Plan;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Sql.Model;
using NSchema.State;
using NSchema.State.Model;

namespace NSchema.Operations.Services;

internal sealed class MigrationWorkflow(
    IMigrationPlanner planner,
    IProgress<OperationProgress> progress,
    ICurrentSchemaProvider currentProvider,
    IDesiredSchemaProvider desiredProvider,
    ISchemaStateSerializer stateSerializer,
    ISchemaStateStore? store = null
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
        if (store is null && HasRunOnceScripts(desired.Project))
        {
            readDiagnostics.Add(Diagnostic.Warning("run-once", "Run-once scripts require a state store to record executions; without one they run on every apply."));
        }

        progress.Report(OperationProgress.Step("Computing migration plan..."));

        var snapshot = await store!.Read(cancellationToken);
        var state = snapshot is null ? SchemaState.Empty : stateSerializer.Deserialize(snapshot.Value);
        var scriptHashes = state.ExecutedScripts.Select(e => new ScriptHash(e.Name, e.Hash)).ToList();
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

    private static bool HasRunOnceScripts(DesiredProject project) => project.Scripts
        .Concat<IScriptDeclaration>(project.Migrations).Any(s => s.RunCondition == RunCondition.Once);

    public async Task<Result<PlannedMigration>> ComputeTeardown(CancellationToken cancellationToken = default)
    {
        // The managed schema is what we tear down: recorded state when we have it, otherwise the declared desired schema.
        var managedSchema = store is not null
            ? await currentProvider.GetSchema(SchemaSourceMode.Offline, null, required: true, cancellationToken)
            : (await desiredProvider.GetProject(null, cancellationToken)).Project.Schema;

        progress.Report(OperationProgress.Step("Computing teardown plan..."));
        return planner.PlanTeardown(managedSchema);
    }

    public async Task<StateCapture?> Refresh(SqlPlan? applied = null, CancellationToken cancellationToken = default)
    {
        if (store is null)
        {
            return null;
        }

        progress.Report(OperationProgress.Step("Updating state store..."));
        var schema = await currentProvider.GetSchema(SchemaSourceMode.Online, null, required: true, cancellationToken);

        // Fetch the current state, apply this run's changes over it, and write the result back.
        var snapshot = await store.Read(cancellationToken);
        var state = snapshot is null ? SchemaState.Empty : stateSerializer.Deserialize(snapshot.Value);

        state = state with { Schema = schema };
        if (applied is not null)
        {
            state = state.RecordExecutions(applied.Scripts, DateTimeOffset.UtcNow);
        }

        snapshot = stateSerializer.Serialize(state);
        progress.Report(OperationProgress.Detail($"State snapshot: {StatusHelpers.Describe(schema)}, {snapshot.Value.Length:N0} bytes."));
        await store.Write(snapshot.Value, cancellationToken);
        return new StateCapture(schema, snapshot.Value.Length);
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
