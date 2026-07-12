using NSchema.Current.Storage.Backends;

namespace NSchema.Current.Storage;

/// <summary>
/// The default <see cref="ISchemaStateManager"/>.
/// </summary>
internal sealed class SchemaStateManager(ISchemaStateSerializer serializer, ISchemaStateStore? store = null) : ISchemaStateManager
{
    private const string Source = "state";

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
            return Result.Failure<StateReadResult>(Diagnostic.Error(Source, ex.Message));
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
            return Result.Failure<StateRawWriteResult>(
                Diagnostic.Error(Source, $"The payload is not a valid state snapshot and was not written. {ex.Message}"));
        }

        await store.Write(arguments.Payload, cancellationToken);
        return new StateRawWriteResult(arguments.Payload.Length);
    }

    private static Result<T> NotConfigured<T>() => Result.Failure<T>(
        Diagnostic.Error(Source, "No state store is configured; register one with UseStateStore or UseFileStateStore."));
}
