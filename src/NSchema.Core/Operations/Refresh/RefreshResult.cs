using NSchema.Schema.Model;

namespace NSchema.Operations.Refresh;

/// <summary>
/// The result of a refresh.
/// </summary>
/// <param name="CapturedSchema">The live schema that was written to the state store.</param>
/// <param name="SnapshotBytes">The size, in bytes, of the serialized state snapshot.</param>
public sealed record RefreshResult(DatabaseSchema CapturedSchema, int SnapshotBytes);
