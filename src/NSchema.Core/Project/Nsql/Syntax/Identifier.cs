namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A name as written in the source, casing preserved.
/// </summary>
/// <param name="Value">The written text of the name.</param>
public sealed record Identifier(string Value) : NsqlNode
{
    /// <inheritdoc/>
    public override string ToString() => Value;
};
