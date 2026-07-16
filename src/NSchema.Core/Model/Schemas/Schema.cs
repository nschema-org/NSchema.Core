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
/// <param name="Name">The name of the schema.</param>
/// <param name="Comment">An optional comment or description for the schema.</param>
/// <param name="Tables">A list of tables that are part of the schema.</param>
/// <param name="Grants">A list of grants that define the permissions associated with the schema.</param>
/// <param name="Views">A list of views that are part of the schema.</param>
/// <param name="Enums">A list of enum types that are part of the schema.</param>
/// <param name="Sequences">A list of sequences that are part of the schema.</param>
/// <param name="Routines">A list of routines (functions and procedures) that are part of the schema.</param>
/// <param name="Domains">A list of domains that are part of the schema.</param>
/// <param name="CompositeTypes">A list of composite types that are part of the schema.</param>
[DebuggerDisplay("{Name,nq} ({Tables.Count} tables)")]
public record Schema(
    SqlIdentifier Name,
    string? Comment = null,
    IReadOnlyList<Table>? Tables = null,
    IReadOnlyList<SchemaGrant>? Grants = null,
    IReadOnlyList<View>? Views = null,
    IReadOnlyList<EnumType>? Enums = null,
    IReadOnlyList<Sequence>? Sequences = null,
    IReadOnlyList<Routine>? Routines = null,
    IReadOnlyList<DomainType>? Domains = null,
    IReadOnlyList<CompositeType>? CompositeTypes = null
) : INamedObject
{
    /// <summary>
    /// A list of tables that are part of the schema.
    /// </summary>
    public IReadOnlyList<Table> Tables { get; init; } = Tables ?? [];

    /// <summary>
    /// A list of views that are part of the schema.
    /// </summary>
    public IReadOnlyList<View> Views { get; init; } = Views ?? [];

    /// <summary>
    /// A list of enum types that are part of the schema.
    /// </summary>
    public IReadOnlyList<EnumType> Enums { get; init; } = Enums ?? [];

    /// <summary>
    /// A list of sequences that are part of the schema.
    /// </summary>
    public IReadOnlyList<Sequence> Sequences { get; init; } = Sequences ?? [];

    /// <summary>
    /// A list of routines (functions and procedures) that are part of the schema. Functions and procedures share
    /// one name space, so they live in a single list.
    /// </summary>
    public IReadOnlyList<Routine> Routines { get; init; } = Routines ?? [];

    /// <summary>
    /// A list of domains that are part of the schema.
    /// </summary>
    public IReadOnlyList<DomainType> Domains { get; init; } = Domains ?? [];

    /// <summary>
    /// A list of composite types that are part of the schema.
    /// </summary>
    public IReadOnlyList<CompositeType> CompositeTypes { get; init; } = CompositeTypes ?? [];

    /// <summary>
    /// A list of grants that define the permissions associated with the schema.
    /// </summary>
    public IReadOnlyList<SchemaGrant> Grants { get; init; } = Grants ?? [];
}
