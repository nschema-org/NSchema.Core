namespace NSchema.Diff.Policies;

/// <summary>
/// Defines policies for handling changes that can fail on existing data during database schema migrations.
/// </summary>
public enum DataHazardPolicy
{
    /// <summary>
    /// Indicates that data-hazardous changes are not allowed and will result in an error if encountered during migration.
    /// </summary>
    Error,

    /// <summary>
    /// Indicates that data-hazardous changes are allowed but will trigger a warning when encountered, allowing the migration to proceed while notifying the user of the risk.
    /// </summary>
    Warn,

    /// <summary>
    /// Indicates that data-hazardous changes are allowed without warnings, reporting them as informational only.
    /// </summary>
    Allow,
}
