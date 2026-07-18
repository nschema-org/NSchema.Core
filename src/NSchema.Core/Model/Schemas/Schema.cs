using System.Diagnostics;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Routines;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Views;

namespace NSchema.Model.Schemas;

/// <summary>
/// Represents the definition of a database schema.
/// </summary>
[DebuggerDisplay("{Name,nq} ({Tables.Count} tables)")]
public sealed class Schema : DatabaseElement, IEquatable<Schema>
{
    /// <summary>
    /// Creates a schema, adopting the given objects.
    /// </summary>
    /// <param name="name">The name of the schema.</param>
    /// <param name="tables">A list of tables that are part of the schema.</param>
    /// <param name="grants">A list of grants that define the permissions associated with the schema.</param>
    /// <param name="views">A list of views that are part of the schema.</param>
    /// <param name="enums">A list of enum types that are part of the schema.</param>
    /// <param name="sequences">A list of sequences that are part of the schema.</param>
    /// <param name="routines">A list of routines (functions and procedures) that are part of the schema.</param>
    /// <param name="domains">A list of domains that are part of the schema.</param>
    /// <param name="compositeTypes">A list of composite types that are part of the schema.</param>
    public Schema(
        SqlIdentifier name,
        DatabaseObjectCollection<Table>? tables = null,
        List<SchemaGrant>? grants = null,
        DatabaseObjectCollection<View>? views = null,
        DatabaseObjectCollection<EnumType>? enums = null,
        DatabaseObjectCollection<Sequence>? sequences = null,
        DatabaseObjectCollection<Routine>? routines = null,
        DatabaseObjectCollection<DomainType>? domains = null,
        DatabaseObjectCollection<CompositeType>? compositeTypes = null
    ) : base(name)
    {
        Tables = tables ?? [];
        Grants = grants ?? [];
        Views = views ?? [];
        Enums = enums ?? [];
        Sequences = sequences ?? [];
        Routines = routines ?? [];
        Domains = domains ?? [];
        CompositeTypes = compositeTypes ?? [];
        Tables.Attach(this);
        Views.Attach(this);
        Enums.Attach(this);
        Sequences.Attach(this);
        Routines.Attach(this);
        Domains.Attach(this);
        CompositeTypes.Attach(this);
    }

    /// <summary>
    /// A list of tables that are part of the schema.
    /// </summary>
    public DatabaseObjectCollection<Table> Tables { get; }

    /// <summary>
    /// A list of views that are part of the schema.
    /// </summary>
    public DatabaseObjectCollection<View> Views { get; }

    /// <summary>
    /// A list of enum types that are part of the schema.
    /// </summary>
    public DatabaseObjectCollection<EnumType> Enums { get; }

    /// <summary>
    /// A list of sequences that are part of the schema.
    /// </summary>
    public DatabaseObjectCollection<Sequence> Sequences { get; }

    /// <summary>
    /// A list of routines (functions and procedures) that are part of the schema. Functions and procedures share
    /// one name space, so they live in a single list.
    /// </summary>
    public DatabaseObjectCollection<Routine> Routines { get; }

    /// <summary>
    /// A list of domains that are part of the schema.
    /// </summary>
    public DatabaseObjectCollection<DomainType> Domains { get; }

    /// <summary>
    /// A list of composite types that are part of the schema.
    /// </summary>
    public DatabaseObjectCollection<CompositeType> CompositeTypes { get; }

    /// <summary>
    /// A list of grants that define the permissions associated with the schema.
    /// </summary>
    public List<SchemaGrant> Grants { get; }

    /// <summary>
    /// Every schema-level object the schema contains, in one sequence.
    /// </summary>
    public IEnumerable<DatabaseObject> Objects() =>
        Tables.Cast<DatabaseObject>()
        .Concat(Views)
        .Concat(Enums)
        .Concat(Sequences)
        .Concat(Routines)
        .Concat(Domains)
        .Concat(CompositeTypes);

    /// <inheritdoc/>
    public override Schema Clone() => new(Name,
        [.. Tables.Select(t => t.Clone())],
        [.. Grants],
        [.. Views.Select(v => v.Clone())],
        [.. Enums.Select(e => e.Clone())],
        [.. Sequences.Select(s => s.Clone())],
        [.. Routines.Select(r => r.Clone())],
        [.. Domains.Select(d => d.Clone())],
        [.. CompositeTypes.Select(t => t.Clone())])
    { Comment = Comment };

    /// <summary>
    /// Returns a copy of the schema restricted to the objects whose identity is in the set. Grants ride the
    /// schema and table members ride their table.
    /// </summary>
    public Schema FilteredTo(IdentitySet identities)
    {
        var copy = Clone();
        copy.Tables.RemoveWhere(t => !identities.Contains(t));
        copy.Views.RemoveWhere(v => !identities.Contains(v));
        copy.Enums.RemoveWhere(e => !identities.Contains(e));
        copy.Sequences.RemoveWhere(s => !identities.Contains(s));
        copy.Routines.RemoveWhere(r => !identities.Contains(r));
        copy.Domains.RemoveWhere(d => !identities.Contains(d));
        copy.CompositeTypes.RemoveWhere(t => !identities.Contains(t));
        return copy;
    }

    /// <summary>
    /// Structural equality over the declared contents; the comment is excluded.
    /// </summary>
    public bool Equals(Schema? other) =>
        other is not null
        && Name == other.Name
        && Grants.SequenceEqual(other.Grants)
        && Tables.SequenceEqual(other.Tables)
        && Views.SequenceEqual(other.Views)
        && Enums.SequenceEqual(other.Enums)
        && Sequences.SequenceEqual(other.Sequences)
        && Routines.SequenceEqual(other.Routines)
        && Domains.SequenceEqual(other.Domains)
        && CompositeTypes.SequenceEqual(other.CompositeTypes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Schema other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Tables.Count, Views.Count);
}
