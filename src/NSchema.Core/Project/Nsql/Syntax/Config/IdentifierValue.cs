namespace NSchema.Project.Nsql.Syntax.Config;

/// <summary>
/// A bare identifier value.
/// </summary>
/// <param name="Value">The identifier text.</param>
public sealed record IdentifierValue(string Value) : ConfigValueNode;
