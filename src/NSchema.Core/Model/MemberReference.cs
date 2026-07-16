namespace NSchema.Model;

/// <summary>
/// The fully-qualified address of a table member.
/// </summary>
/// <param name="Schema">The schema containing the owning object.</param>
/// <param name="Object">The object that owns the member.</param>
/// <param name="Member">The member's name within that object.</param>
public sealed record MemberReference(SqlIdentifier Schema, SqlIdentifier Object, SqlIdentifier Member) : Address
{
    /// <inheritdoc />
    public override string Value => $"{Schema}.{Object}.{Member}";
}
