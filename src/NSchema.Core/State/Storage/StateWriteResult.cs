namespace NSchema.State.Storage;

/// <summary>
/// The outcome of a state write.
/// </summary>
/// <param name="PayloadSize">The size of the written payload, in bytes.</param>
public sealed record StateWriteResult(int PayloadSize);
