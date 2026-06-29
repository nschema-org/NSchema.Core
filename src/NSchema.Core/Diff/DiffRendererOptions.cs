namespace NSchema.Diff;

/// <summary>
/// Options for the <see cref="DiffRenderer"/>.
/// </summary>
public class DiffRendererOptions
{
    /// <summary>
    /// The string to use for indenting nested blocks in the rendered output. Defaults to four spaces.
    /// </summary>
    public string Indent { get; set; } = new(' ', 4);
}
