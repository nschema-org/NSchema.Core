using NSchema.Sql.Model;

namespace NSchema.Operations.Apply;

/// <summary>
/// Arguments for applying a computed plan.
/// </summary>
public sealed record ApplyArguments
{
    /// <summary>
    /// The SQL to execute, from a plan operation or a saved plan file.
    /// </summary>
    public required SqlPlan Sql { get; init; }
}
