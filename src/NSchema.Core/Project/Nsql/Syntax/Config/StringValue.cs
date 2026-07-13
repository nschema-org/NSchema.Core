namespace NSchema.Project.Nsql.Syntax.Config;

/// <summary>
/// A quoted string value.
/// </summary>
/// <param name="Value">The string content.</param>
public sealed record StringValue(string Value) : ConfigValueNode;
