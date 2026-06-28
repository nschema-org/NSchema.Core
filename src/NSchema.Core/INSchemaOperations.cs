using NSchema.Diagnostics;
using NSchema.Operations;
using NSchema.Operations.Apply;
using NSchema.Operations.Destroy;
using NSchema.Operations.Doctor;
using NSchema.Operations.Drift;
using NSchema.Operations.Import;
using NSchema.Operations.Plan;
using NSchema.Operations.PlanDestroy;
using NSchema.Operations.Refresh;
using NSchema.Operations.Validate;

namespace NSchema;

/// <summary>
/// Provides one discoverable surface over every NSchema workflow operation.
/// </summary>
public interface INSchemaOperations
{
    /// <summary>
    /// Computes the plan without applying it.
    /// </summary>
    Task<Result<PlanResult>> Plan(PlanArguments args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the teardown plan (the plan to drop the managed schema) without applying it.
    /// </summary>
    Task<Result<PlanResult>> PlanDestroy(PlanDestroyArguments args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes a migration under the state lock and returns a handle that owns the lock until disposed — render it,
    /// optionally confirm, then <see cref="IMigrationPlan.Execute"/>.
    /// </summary>
    Task<Result<IMigrationPlan>> BeginApply(ApplyArguments args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the teardown plan under the state lock and returns a handle that owns the lock until disposed.
    /// </summary>
    Task<Result<IMigrationPlan>> BeginDestroy(DestroyArguments args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the live current schema and writes it to the state store.
    /// </summary>
    Task<Result> Refresh(RefreshArguments args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the desired schema and validates it against the configured schema policies.
    /// </summary>
    Task<Result> Validate(ValidateArguments args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares the recorded state against the live database and reports how the live database has drifted.
    /// </summary>
    Task<Result<DriftResult>> Drift(DriftArguments args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the live database schema and writes it as desired-schema source files.
    /// </summary>
    Task<Result> Import(ImportArguments args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs read-only health checks against the configured infrastructure and reports the outcome of each.
    /// </summary>
    Task<Result> Doctor(DoctorArguments args, CancellationToken cancellationToken = default);
}
