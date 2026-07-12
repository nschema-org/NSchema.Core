using NSchema.Current.Domain.Models;

namespace NSchema.Current.Storage;

/// <summary>
/// Arguments for a state write.
/// </summary>
/// <param name="State">The state to persist, replacing any recorded snapshot.</param>
public sealed record StateWriteArguments(SchemaState State);
