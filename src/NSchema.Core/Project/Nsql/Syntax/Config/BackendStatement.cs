namespace NSchema.Project.Nsql.Syntax.Config;

/// <summary>
/// <c>BACKEND [label] ( key = value, … );</c> — selects and configures the state backend.
/// </summary>
/// <param name="Label">The backend label (e.g. <c>file</c>, <c>s3</c>).</param>
/// <param name="Attributes">The attribute list.</param>
public sealed record BackendStatement(Identifier? Label, IReadOnlyList<ConfigAttribute> Attributes) : ConfigStatement(Label, Attributes);