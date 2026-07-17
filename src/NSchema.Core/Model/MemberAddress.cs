namespace NSchema.Model;

/// <summary>
/// The fully-qualified address of a table member.
/// </summary>
/// <param name="Schema">The schema containing the owning object.</param>
/// <param name="Object">The object that owns the member.</param>
/// <param name="Member">The member's name within that object.</param>
public sealed record MemberAddress(SqlIdentifier Schema, SqlIdentifier Object, SqlIdentifier Member) : Address
{
    /// <inheritdoc />
    public override string Value => $"{Schema}.{Object}.{Member}";

    /// <inheritdoc />
    public override SqlIdentifier? SchemaName => Schema;

    /// <summary>
    /// The address of the object that contains the member.
    /// </summary>
    public ObjectAddress Owner => new(Schema, Object);
}
