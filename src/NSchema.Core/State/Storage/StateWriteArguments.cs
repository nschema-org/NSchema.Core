using NSchema.State.Model;

namespace NSchema.State.Storage;

/// <summary>
/// Arguments for a state write.
/// </summary>
/// <param name="State">The state to persist, replacing any recorded snapshot.</param>
public sealed record StateWriteArguments(SchemaState State);
