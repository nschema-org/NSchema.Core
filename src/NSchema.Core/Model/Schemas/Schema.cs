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
/// <remarks>
/// The schema adopts the objects it is given, whether through the constructor or an object initializer: an
/// unparented object is wired to this schema, and an object that already belongs to another schema is cloned
/// first.
/// </remarks>
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
        IReadOnlyList<Table>? tables = null,
        IReadOnlyList<SchemaGrant>? grants = null,
        IReadOnlyList<View>? views = null,
        IReadOnlyList<EnumType>? enums = null,
        IReadOnlyList<Sequence>? sequences = null,
        IReadOnlyList<Routine>? routines = null,
        IReadOnlyList<DomainType>? domains = null,
        IReadOnlyList<CompositeType>? compositeTypes = null
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
    }

    /// <summary>
    /// A list of tables that are part of the schema.
    /// </summary>
    public IReadOnlyList<Table> Tables { get; init => field = value.ForEach(f => f.Schema = this); }

    /// <summary>
    /// A list of views that are part of the schema.
    /// </summary>
    public IReadOnlyList<View> Views { get; init => field = value.ForEach(f => f.Schema = this); }

    /// <summary>
    /// A list of enum types that are part of the schema.
    /// </summary>
    public IReadOnlyList<EnumType> Enums { get; init => field = value.ForEach(f => f.Schema = this); }

    /// <summary>
    /// A list of sequences that are part of the schema.
    /// </summary>
    public IReadOnlyList<Sequence> Sequences { get; init => field = value.ForEach(f => f.Schema = this); }

    /// <summary>
    /// A list of routines (functions and procedures) that are part of the schema. Functions and procedures share
    /// one name space, so they live in a single list.
    /// </summary>
    public IReadOnlyList<Routine> Routines { get; init => field = value.ForEach(f => f.Schema = this); }

    /// <summary>
    /// A list of domains that are part of the schema.
    /// </summary>
    public IReadOnlyList<DomainType> Domains { get; init => field = value.ForEach(f => f.Schema = this); }

    /// <summary>
    /// A list of composite types that are part of the schema.
    /// </summary>
    public IReadOnlyList<CompositeType> CompositeTypes { get; init => field = value.ForEach(f => f.Schema = this); }

    /// <summary>
    /// A list of grants that define the permissions associated with the schema.
    /// </summary>
    public IReadOnlyList<SchemaGrant> Grants { get; init; }

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

    /// <summary>
    /// Returns a copy of the schema with the given object lists replaced. A <see langword="null"/> argument
    /// keeps the current objects.
    /// </summary>
    public Schema With(
        IReadOnlyList<Table>? tables = null,
        IReadOnlyList<View>? views = null,
        IReadOnlyList<EnumType>? enums = null,
        IReadOnlyList<Sequence>? sequences = null,
        IReadOnlyList<Routine>? routines = null,
        IReadOnlyList<DomainType>? domains = null,
        IReadOnlyList<CompositeType>? compositeTypes = null
    ) => new(Name,
        [.. (tables ?? Tables).Select(t => t.Clone())],
        Grants,
        [.. (views ?? Views).Select(v => v.Clone())],
        [.. (enums ?? Enums).Select(e => e.Clone())],
        [.. (sequences ?? Sequences).Select(s => s.Clone())],
        [.. (routines ?? Routines).Select(r => r.Clone())],
        [.. (domains ?? Domains).Select(d => d.Clone())],
        [.. (compositeTypes ?? CompositeTypes).Select(t => t.Clone())]
    ) { Comment = Comment };

    /// <summary>
    /// Returns the schema restricted to the objects whose identity is in the set. Grants ride the schema and
    /// table members ride their table.
    /// </summary>
    public Schema FilteredTo(IdentitySet identities) => With(
        tables: [.. Tables.Where(identities.Contains)],
        views: [.. Views.Where(identities.Contains)],
        enums: [.. Enums.Where(identities.Contains)],
        sequences: [.. Sequences.Where(identities.Contains)],
        routines: [.. Routines.Where(identities.Contains)],
        domains: [.. Domains.Where(identities.Contains)],
        compositeTypes: [.. CompositeTypes.Where(identities.Contains)]);

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
