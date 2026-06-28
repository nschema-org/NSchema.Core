using NSchema.Sql.Model;

namespace NSchema.Operations.Apply;

/// <summary>
/// Arguments for applying a computed plan.
/// </summary>
public sealed record ApplyArguments
{
    /// <summary>
    /// The SQL to execute, from a plan operation or a saved plan file. An empty or absent plan executes nothing but
    /// still captures the resulting state.
    /// </summary>
    public required SqlPlan? Sql { get; init; }
}
