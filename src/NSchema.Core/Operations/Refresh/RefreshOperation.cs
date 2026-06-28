using NSchema.Diagnostics;
using NSchema.Operations.Services;

namespace NSchema.Operations.Refresh;

/// <summary>
/// Reads the live current schema and writes it to the state store, under the state lock.
/// </summary>
internal sealed class RefreshOperation(IMigrationWorkflow workflow) : IOperation<RefreshArguments, Result>
{
    public async Task<Result> Execute(RefreshArguments args, CancellationToken cancellationToken = default)
    {
        await workflow.Refresh(RefreshMode.Required, cancellationToken);
        return Result.Success();
    }
}
