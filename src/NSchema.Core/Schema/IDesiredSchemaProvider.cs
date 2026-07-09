namespace NSchema.Schema;

/// <summary>
/// Reads the desired state, schema and deployment scripts.
/// </summary>
internal interface IDesiredSchemaProvider
{
    /// <summary>
    /// Reads and aggregates the desired project, optionally scoping the schema to the given <paramref name="schemaNames"/>.
    /// </summary>
    ValueTask<DesiredProjectResult> GetProject(string[]? schemaNames = null, CancellationToken cancellationToken = default);
}
