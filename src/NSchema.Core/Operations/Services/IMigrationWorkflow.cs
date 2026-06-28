using NSchema.Diagnostics;
using NSchema.Plan;
using NSchema.Schema;

namespace NSchema.Operations.Services;

/// <summary>
/// The imperative shell operations use to run the pure planner: it resolves schemas, invokes
/// <see cref="NSchema.Plan.IMigrationPlanner"/>, surfaces diagnostics, and captures state to the store.
/// </summary>
internal interface IMigrationWorkflow
{
    /// <summary>
    /// Validates the desired schema against the schema policies.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result> Validate(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the desired and current schemas and computes the migration plan.
    /// </summary>
    /// <param name="currentSource">Which source to read the current schema from.</param>
    /// <param name="required">Whether <paramref name="currentSource"/> must be available, or may fall back to the other source.</param>
    /// <param name="schemas">The schemas to scope to, or <see langword="null"/> to derive scope from the desired schema.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<MigrationPlanResult> ComputePlan(SchemaSourceMode currentSource, bool required, string[]? schemas, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the teardown plan for the managed schema.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<MigrationPlanResult> ComputeTeardown(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the desired and current schemas, computes the migration plan, reports it, and throws on a policy error.
    /// </summary>
    /// <param name="currentSource">Which source to read the current schema from.</param>
    /// <param name="required">Whether <paramref name="currentSource"/> must be available, or may fall back to the other source.</param>
    /// <param name="schemas">The schemas to scope to, or <see langword="null"/> to derive scope from the desired schema.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<PlannedMigration> Plan(SchemaSourceMode currentSource, bool required, string[]? schemas, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a plan that tears down the managed schema, reporting it and throwing on a policy error.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<PlannedMigration> PlanDestroy(CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures the live schema into the state store.
    /// </summary>
    /// <param name="mode">
    /// <see cref="RefreshMode.Required"/> throws when no state store is configured;
    /// <see cref="RefreshMode.Optional"/> skips silently.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Refresh(RefreshMode mode, CancellationToken cancellationToken = default);
}
