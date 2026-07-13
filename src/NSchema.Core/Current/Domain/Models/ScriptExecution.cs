using NSchema.Project.Domain.Models;

namespace NSchema.Current.Domain.Models;

/// <summary>
/// A recorded script execution.
/// </summary>
/// <param name="Name">The script's declared name.</param>
/// <param name="Hash">The hash of the script body that was executed.</param>
/// <param name="ExecutedUtc">When the execution was recorded.</param>
public sealed record ScriptExecution(SqlIdentifier Name, string Hash, DateTimeOffset ExecutedUtc);
