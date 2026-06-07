using NSchema.Operations;
using NSchema.Operations.Confirmation;

namespace NSchema.Hosting;

/// <summary>
/// The default <see cref="IOperationConfirmation"/>, which approves every operation without prompting.
/// </summary>
internal sealed class AutoApproveConfirmation : IOperationConfirmation
{
    public ValueTask<bool> Confirm(OperationConfirmationRequest request, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
}
