namespace NSchema.Current.Domain.Models;

/// <summary>
/// A recorded script execution.
/// </summary>
/// <param name="Name">The script's declared name.</param>
/// <param name="Hash">The hash of the script body that was executed.</param>
/// <param name="ExecutedUtc">When the execution was recorded.</param>
public sealed record ScriptExecution(string Name, string Hash, DateTimeOffset ExecutedUtc);
