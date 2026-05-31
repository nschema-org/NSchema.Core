namespace NSchema.Hosting.Operations;

internal sealed class RefreshOperation(IStateCapturer stateCapturer) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (!await stateCapturer.Capture(cancellationToken))
        {
            throw new InvalidOperationException(
                "Refresh requires a state store. Register one via UseStateStore(...) or UseStateStoreFile(...).");
        }
    }
}
