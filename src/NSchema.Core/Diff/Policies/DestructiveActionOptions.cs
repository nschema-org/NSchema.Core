namespace NSchema.Diff.Policies;

/// <summary>
/// Configures <see cref="DestructiveActionDiffPolicy"/>.
/// </summary>
public class DestructiveActionOptions
{
    /// <summary>
    /// Specifies the policy to apply when a destructive action is encountered during the migration process.
    /// </summary>
    public DestructiveActionPolicy Policy { get; set; } = DestructiveActionPolicy.Error;
}
