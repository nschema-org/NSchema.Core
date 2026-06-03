namespace NSchema.Migration.Diff;

/// <summary>
/// Options for the default Terraform-style <see cref="TerraformDiffRenderer"/>.
/// </summary>
public class TerraformDiffRendererOptions
{
    /// <summary>
    /// Whether to include ANSI color in the rendered output.
    /// </summary>
    public bool IncludeColour { get; set; }
}
