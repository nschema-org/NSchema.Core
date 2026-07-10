namespace NSchema.Sql.Model;

/// <summary>
/// Identifies a run-once script pending in a plan.
/// </summary>
/// <param name="Name">The script's declared name.</param>
/// <param name="Hash">The hash of the script's SQL body.</param>
public sealed record ScriptHash(string Name, string Hash);
