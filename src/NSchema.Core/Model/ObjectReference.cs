namespace NSchema.Model;

/// <summary>
/// The address of a schema-level object.
/// </summary>
/// <param name="Schema">The schema containing the object.</param>
/// <param name="Name">The object's name within that schema.</param>
public sealed record ObjectReference(SqlIdentifier Schema, SqlIdentifier Name)
{
    /// <summary>
    /// The address as written: <c>schema.name</c>.
    /// </summary>
    public override string ToString() => $"{Schema}.{Name}";
}
