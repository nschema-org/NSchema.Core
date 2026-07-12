using NSchema.Current;
using NSchema.Plan.Domain.Models;

namespace NSchema.Operations.Workflow;

/// <summary>
/// The imperative shell operations use to run the pure planner: it resolves schemas, invokes
/// <see cref="NSchema.Plan.Domain.IMigrationPlanner"/>, surfaces diagnostics, and captures state to the store.
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
    /// <param name="schemas">The schemas to scope to, or <see langword="null"/> to derive scope from the desired schema.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<MigrationPlan>> ComputePlan(SchemaSourceMode currentSource, string[]? schemas, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the teardown plan for the managed schema.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<MigrationPlan>> ComputeTeardown(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the state store from the live schema.
    /// </summary>
    /// <param name="applied">The plan that was just applied, or <see langword="null"/> when nothing ran.</param>
    /// <param name="force">When true, an existing payload that cannot be read is replaced (resetting the run-once
    /// ledger, flagged on the capture); when false, an unreadable payload fails the capture instead.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The capture outcome, or <see langword="null"/> when no state store is configured (nothing to capture).</returns>
    Task<Result<StateCapture>> Refresh(MigrationPlan? applied = null, bool force = false, CancellationToken cancellationToken = default);
}
