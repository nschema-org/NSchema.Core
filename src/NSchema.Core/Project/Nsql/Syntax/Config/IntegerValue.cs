namespace NSchema.Project.Nsql.Syntax.Config;

/// <summary>
/// A (signed) integer value.
/// </summary>
/// <param name="Value">The integer.</param>
public sealed record IntegerValue(long Value) : ConfigValueNode;