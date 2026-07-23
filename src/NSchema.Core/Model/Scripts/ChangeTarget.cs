namespace NSchema.Model.Scripts;

/// <summary>
/// The structural change a change-event script targets.
/// </summary>
public sealed record ChangeTarget(
    SqlIdentifier? Schema,
    SqlIdentifier Table,
    SqlIdentifier Member,
    ChangeTrigger Trigger
)
{
    /// <summary>
    /// The target table, when the target is schema-scoped.
    /// </summary>
    public ObjectAddress? TableAddress => Schema is { } schema ? new ObjectAddress(schema, Table) : null;

    /// <summary>
    /// The schema the run is scoped to, when the target is schema-scoped.
    /// </summary>
    public SchemaAddress? ScopeSchema => Schema != null ? new SchemaAddress(Schema) : null;

    /// <summary>
    /// The fully qualified target path.
    /// </summary>
    public string Path => $"{Schema}.{Table}.{Member}";
}
