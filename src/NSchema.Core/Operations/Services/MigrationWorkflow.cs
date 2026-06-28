using NSchema.Diagnostics;
using NSchema.Operations.Progress;
using NSchema.Plan;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.State;

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

        // The findings — including any non-error advisories — are returned as data for the caller to render.
        return planner.Validate(desired.Schema);
    }

    public async Task<Result<PlannedMigration>> ComputePlan(SchemaSourceMode currentSource, bool required, string[]? schemas, CancellationToken cancellationToken = default)
    {
        progress.Report(OperationProgress.Step("Loading desired schema..."));
        var desired = await desiredProvider.GetProject(schemas, cancellationToken);
        var schemasInScope = schemas ?? desired.Schema.AllSchemaNames;
        ReportDesiredDetail(desired);

        progress.Report(OperationProgress.Step($"Migration will be scoped to the following schemas: {string.Join(", ", schemasInScope)}"));

        progress.Report(OperationProgress.Step("Loading provider schema..."));
        var currentSchema = await currentProvider.GetSchema(currentSource, schemasInScope, required, cancellationToken);
        progress.Report(OperationProgress.Detail($"Current schema ({currentSource.ToString().ToLowerInvariant()}): {Census.Describe(currentSchema)}."));

        progress.Report(OperationProgress.Step("Computing migration plan..."));
        return planner.Plan(currentSchema, desired.Schema, desired.Scripts);
    }

    public async Task<Result<PlannedMigration>> ComputeTeardown(CancellationToken cancellationToken = default)
    {
        // The managed schema is what we tear down: recorded state when we have it, otherwise the declared desired schema.
        var managedSchema = store is not null
            ? await currentProvider.GetSchema(SchemaSourceMode.Offline, null, required: true, cancellationToken)
            : (await desiredProvider.GetProject(null, cancellationToken)).Schema;

        progress.Report(OperationProgress.Step("Computing teardown plan..."));
        return planner.PlanTeardown(managedSchema);
    }

    public async Task Refresh(RefreshMode mode, CancellationToken cancellationToken = default)
    {
        if (store is null)
        {
            if (mode == RefreshMode.Required)
            {
                throw new InvalidOperationException("Unable to refresh state without a configured state store.");
            }

            // Optional: nothing to capture.
            return;
        }

        progress.Report(OperationProgress.Step("Updating state store..."));
        var schema = await currentProvider.GetSchema(SchemaSourceMode.Online, null, required: true, cancellationToken);
        var snapshot = stateSerializer.Serialize(schema);
        progress.Report(OperationProgress.Detail($"State snapshot: {Census.Describe(schema)}, {snapshot.Length:N0} bytes."));
        await store.Write(snapshot, cancellationToken);
    }

    /// <summary>
    /// Emits the verbose detail about the loaded desired project: the files it was read from and a census of
    /// what they declared.
    /// </summary>
    private void ReportDesiredDetail(DesiredProject desired)
    {
        if (desired.Files.Count > 0)
        {
            progress.Report(OperationProgress.Detail($"Read {Census.Count(desired.Files.Count, "DDL file")}:"));
            foreach (var file in desired.Files)
            {
                progress.Report(OperationProgress.Detail(file));
            }
        }

        progress.Report(OperationProgress.Detail($"Desired schema: {Census.Describe(desired.Schema)}, {Census.Count(desired.Scripts.Count, "deployment script")}."));
    }
}
