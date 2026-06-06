using NSchema.Plan.Model;

namespace NSchema.Hosting;

/// <summary>
/// The default <see cref="IMigrationConfirmation"/>, which approves every migration without prompting.
/// </summary>
internal sealed class AutoApproveConfirmation : IMigrationConfirmation
{
    public ValueTask<bool> Confirm(MigrationPlan plan, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
}
