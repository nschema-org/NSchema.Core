namespace NSchema.Model;

/// <summary>
/// A verbatim fragment of SQL that NSchema carries but does not interpret.
/// </summary>
public sealed record SqlText : ValueObject<string>
{
    /// <summary>
    /// Wraps the verbatim SQL text.
    /// </summary>
    public SqlText(string value) : base(value)
    {
    }
}
