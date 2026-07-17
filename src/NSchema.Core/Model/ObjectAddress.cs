namespace NSchema.Model;

/// <summary>
/// The address of a schema-level object.
/// </summary>
/// <param name="Schema">The schema containing the object.</param>
/// <param name="Name">The object's name within that schema.</param>
public sealed record ObjectAddress(SqlIdentifier Schema, SqlIdentifier Name) : Address
{
    /// <inheritdoc />
    public override string Value => $"{Schema}.{Name}";

    /// <inheritdoc />
    public override SqlIdentifier? SchemaName => Schema;
}
