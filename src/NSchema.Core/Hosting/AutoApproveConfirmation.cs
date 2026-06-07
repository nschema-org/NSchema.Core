using NSchema.Operations;
using NSchema.Plan.Model;

namespace NSchema.Hosting;

/// <summary>
/// The default <see cref="IOperationConfirmation"/>, which approves every migration without prompting.
/// </summary>
internal sealed class AutoApproveConfirmation : IOperationConfirmation
{
    public ValueTask<bool> Confirm(MigrationPlan plan, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
}
