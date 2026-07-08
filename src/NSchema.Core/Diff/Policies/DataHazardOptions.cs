namespace NSchema.Diff.Policies;

/// <summary>
/// Configures <see cref="DataHazardDiffPolicy"/>.
/// </summary>
public class DataHazardOptions
{
    /// <summary>
    /// Specifies the policy to apply when a change that can fail on existing data is encountered during the migration process.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="DataHazardPolicy.Warn"/>: whether a hazardous change actually fails depends on the data in the table.
    /// </remarks>
    public DataHazardPolicy Policy { get; set; } = DataHazardPolicy.Warn;
}
