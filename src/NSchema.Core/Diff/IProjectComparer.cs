using NSchema.Diagnostics;
using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

/// <summary>
/// Computes the complete difference between what currently exists and the desired project: the structural schema
/// diff, with the pending script runs resolved onto it (deployment scripts that will fire, and change-event
/// scripts matched to their changes).
/// </summary>
internal interface IProjectComparer
{
    /// <summary>
    /// Compares <paramref name="current"/> against <paramref name="desired"/>.
    /// </summary>
    /// <param name="current">What currently exists: the schema plus the recorded script executions.</param>
    /// <param name="desired">The desired project: the schema plus the declared scripts.</param>
    /// <returns>The complete difference — always produced and carried, with any findings raised while computing it.</returns>
    Result<DatabaseDiff> Compare(CurrentState current, Project desired);

    /// <summary>
    /// Computes the teardown difference: everything in <paramref name="currentSchema"/> removed, no scripts.
    /// </summary>
    /// <param name="currentSchema">The managed schema to tear down.</param>
    DatabaseDiff CompareTeardown(DatabaseSchema currentSchema);
}
