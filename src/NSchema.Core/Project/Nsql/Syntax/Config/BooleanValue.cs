namespace NSchema.Project.Nsql.Syntax.Config;

/// <summary>
/// A <c>true</c>/<c>false</c> value.
/// </summary>
/// <param name="Value">The boolean.</param>
public sealed record BooleanValue(bool Value) : ConfigValueNode;
