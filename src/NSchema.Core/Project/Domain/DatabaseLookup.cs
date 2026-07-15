using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Tables;

namespace NSchema.Project.Domain;

/// <summary>
/// Address → node lookup over a pure schema tree.
/// </summary>
internal sealed class DatabaseLookup(Database schema)
{
    private readonly Dictionary<SqlIdentifier, Schema> _schemas =
        schema.Schemas.ToDictionary(s => s.Name);

    public Schema? FindSchema(SqlIdentifier name) => _schemas.GetValueOrDefault(name);

    public Table? FindTable(ObjectReference address) =>
        FindSchema(address.Schema)?.Tables.FirstOrDefault(t => t.Name == address.Name);

    public Column? FindColumn(MemberReference address) =>
        FindTable(new ObjectReference(address.Schema, address.Object))?.Columns.FirstOrDefault(c => c.Name == address.Member);

    public bool HasView(ObjectReference address) =>
        FindSchema(address.Schema)?.Views.Any(v => v.Name == address.Name) == true;

    public bool HasEnum(ObjectReference address) =>
        FindSchema(address.Schema)?.Enums.Any(e => e.Name == address.Name) == true;

    public bool HasSequence(ObjectReference address) =>
        FindSchema(address.Schema)?.Sequences.Any(s => s.Name == address.Name) == true;

    public bool HasRoutine(ObjectReference address) =>
        FindSchema(address.Schema)?.Routines.Any(r => r.Name == address.Name) == true;

    public bool HasDomain(ObjectReference address) =>
        FindSchema(address.Schema)?.Domains.Any(d => d.Name == address.Name) == true;

    public bool HasCompositeType(ObjectReference address) =>
        FindSchema(address.Schema)?.CompositeTypes.Any(t => t.Name == address.Name) == true;

    public bool HasTable(ObjectReference address) => FindTable(address) is not null;

    public bool HasColumn(MemberReference address) => FindColumn(address) is not null;
}
