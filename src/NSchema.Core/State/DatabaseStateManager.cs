using NSchema.State.Backends;

namespace NSchema.State;

/// <summary>
/// The default <see cref="IDatabaseStateManager"/>.
/// </summary>
internal sealed class DatabaseStateManager(IDatabaseStateSerializer serializer, IDatabaseStateStore? store = null) : IDatabaseStateManager
{

    public bool IsConfigured => store is not null;

    public async Task<Result<StateReadResult>> Read(StateReadArguments arguments, CancellationToken cancellationToken = default)
    {
        if (store is null)
        {
            return NotConfigured<StateReadResult>();
        }

        var snapshot = await store.Read(cancellationToken);
        if (snapshot is null)
        {
            return new StateReadResult(null);
        }

        try
        {
            return new StateReadResult(serializer.Deserialize(snapshot.Value));
        }
        catch (Exception ex) when (ex is StateDeserializationException or NotSupportedException)
        {
            return Result.Failure<StateReadResult>(StateDiagnostics.UnreadablePayload(ex));
        }
    }

    public async Task<Result<StateWriteResult>> Write(StateWriteArguments arguments, CancellationToken cancellationToken = default)
    {
        if (store is null)
        {
            return NotConfigured<StateWriteResult>();
        }

        var payload = serializer.Serialize(arguments.State);
        await store.Write(payload, cancellationToken);
        return new StateWriteResult(payload.Length);
    }

    public async Task<Result<StateRawReadResult>> ReadRaw(StateRawReadArguments arguments, CancellationToken cancellationToken = default)
    {
        if (store is null)
        {
            return NotConfigured<StateRawReadResult>();
        }

        return new StateRawReadResult(await store.Read(cancellationToken));
    }

    public async Task<Result<StateRawWriteResult>> WriteRaw(StateRawWriteArguments arguments, CancellationToken cancellationToken = default)
    {
        if (store is null)
        {
            return NotConfigured<StateRawWriteResult>();
        }

        try
        {
            serializer.Deserialize(arguments.Payload);
        }
        catch (Exception ex) when (ex is StateDeserializationException or NotSupportedException)
        {
            return Result.Failure<StateRawWriteResult>(StateDiagnostics.InvalidRawPayload(ex));
        }

        await store.Write(arguments.Payload, cancellationToken);
        return new StateRawWriteResult(arguments.Payload.Length);
    }

    private static Result<T> NotConfigured<T>() => Result.Failure<T>(StateDiagnostics.NotConfigured);
}
