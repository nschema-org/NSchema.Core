using NSchema.Operations.Services;

namespace NSchema.Operations.Refresh;

internal sealed class RefreshOperation(IMigrationWorkflow workflow) : IRefreshOperation
{
    public Task Execute(RefreshArguments arguments, CancellationToken cancellationToken = default) =>
        workflow.Refresh(RefreshMode.Required, cancellationToken);
}
