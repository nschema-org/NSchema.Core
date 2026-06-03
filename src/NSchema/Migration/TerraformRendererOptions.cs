namespace NSchema.Migration;

/// <summary>
/// Options for the default Terraform-style <see cref="TerraformMigrationDiffRenderer"/>.
/// </summary>
public class TerraformRendererOptions
{
    /// <summary>
    /// Whether to include ANSI color in the rendered output.
    /// </summary>
    public bool IncludeColour { get; set; }
}
