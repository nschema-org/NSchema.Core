using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;

namespace NSchema.Model.Services;

/// <summary>
/// Address → node lookup over a pure schema tree.
/// </summary>
internal sealed class DatabaseLookup(Database schema)
{
    private readonly Dictionary<SqlIdentifier, Schema> _schemas =
        schema.Schemas.ToDictionary(s => s.Name);

    public Schema? FindSchema(SqlIdentifier name) => _schemas.GetValueOrDefault(name);

    public Table? FindTable(ObjectAddress address) =>
        FindSchema(address.Schema)?.Tables.FirstOrDefault(t => t.Name == address.Name);

    public Column? FindColumn(MemberAddress address) =>
        FindTable(new ObjectAddress(address.Schema, address.Object))?.Columns.FirstOrDefault(c => c.Name == address.Member);

    /// <summary>
    /// Whether the identified object is declared.
    /// </summary>
    public bool Has(ObjectIdentity identity) => Has(identity.Kind, identity.Address);

    /// <summary>
    /// Whether an object of <paramref name="kind"/> is declared at <paramref name="address"/>.
    /// </summary>
    public bool Has(ObjectKind kind, ObjectAddress address) => kind switch
    {
        ObjectKind.Table => FindTable(address) is not null,
        ObjectKind.View => FindSchema(address.Schema)?.Views.Any(v => v.Name == address.Name) == true,
        ObjectKind.Enum => FindSchema(address.Schema)?.Enums.Any(e => e.Name == address.Name) == true,
        ObjectKind.Sequence => FindSchema(address.Schema)?.Sequences.Any(s => s.Name == address.Name) == true,
        ObjectKind.Routine => FindSchema(address.Schema)?.Routines.Any(r => r.Name == address.Name) == true,
        ObjectKind.Domain => FindSchema(address.Schema)?.Domains.Any(d => d.Name == address.Name) == true,
        ObjectKind.CompositeType => FindSchema(address.Schema)?.CompositeTypes.Any(t => t.Name == address.Name) == true,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public bool HasColumn(MemberAddress address) => FindColumn(address) is not null;
}
