using NSchema.Hosting;

namespace NSchema.Diff;

/// <summary>
/// Options for the default Terraform-style <see cref="TerraformDiffRenderer"/>.
/// </summary>
public class TerraformDiffRendererOptions
{
    /// <summary>
    /// Whether to include ANSI color in the rendered output.
    /// </summary>
    public bool IncludeColour { get; set; } = EnvironmentHelpers.SupportsColor;

    /// <summary>
    /// The string to use for indenting nested blocks in the rendered output. Defaults to four spaces.
    /// </summary>
    public string Indent { get; set; } = new(' ', 4);
}
