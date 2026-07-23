using System.Text.Json.Serialization;

namespace NSchema.Model;

/// <summary>
/// The address of a named item whose container is a schema or the database root.
/// </summary>
/// <param name="Schema">The schema the item is scoped to, or <see langword="null"/> when it is global.</param>
/// <param name="Name">The item's declared name.</param>
public sealed record ScopedAddress(SqlIdentifier? Schema, SqlIdentifier Name) : Address
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Value => Schema == null ? Name.Value : $"{Schema}.{Name}";

    /// <inheritdoc />
    [JsonIgnore]
    public override SqlIdentifier? SchemaName => Schema;

    /// <inheritdoc />
    public override bool Covers(Address other) => Equals(other);
}
