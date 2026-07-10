namespace NSchema.State.Model;

/// <summary>
/// A recorded script execution, persisted in the state envelope.
/// </summary>
/// <param name="Name">The script's declared name — the identity the execution is recorded under.</param>
/// <param name="Hash">The hash of the script body that was executed.</param>
/// <param name="ExecutedUtc">When the execution was recorded.</param>
internal sealed record ScriptExecutionRecord(string Name, string Hash, DateTimeOffset ExecutedUtc);
