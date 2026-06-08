namespace NSchema.Migration;

/// <summary>
/// Represents options that can be configured for a migration operation.
/// </summary>
public class MigrationOptions
{
    /// <summary>
    /// Specifies the policy to apply when a destructive action is encountered during the migration process.
    /// </summary>
    /// <remarks>
    /// A destructive action is an operation that could potentially lead to data loss, such as dropping a table or column.
    /// The policy determines how the migrator should handle such actions, whether to allow them, block them, or raise an error.
    /// </remarks>
    public DestructiveActionPolicy DestructiveActionPolicy { get; set; } = DestructiveActionPolicy.Error;
}
