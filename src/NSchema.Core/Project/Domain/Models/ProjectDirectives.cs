using NSchema.Project.Domain.Models.CompositeTypes;
using NSchema.Project.Domain.Models.Domains;
using NSchema.Project.Domain.Models.Enums;
using NSchema.Project.Domain.Models.Extensions;
using NSchema.Project.Domain.Models.Routines;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Sequences;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Views;

namespace NSchema.Project.Domain.Models;

/// <summary>
/// The management directives a project declares.
/// </summary>
/// <remarks>
/// Directive addresses name current reality — what exists now, before anything is applied — with one
/// exception: a partial marks the project's own declaration, so it carries the declared name. A future
/// directive that crosses kinds belongs here at the root, beside the slices, not inside any of them.
/// </remarks>
public sealed record ProjectDirectives(
    SchemaDirectives? Schemas = null,
    TableDirectives? Tables = null,
    ViewDirectives? Views = null,
    EnumDirectives? Enums = null,
    SequenceDirectives? Sequences = null,
    RoutineDirectives? Routines = null,
    DomainDirectives? Domains = null,
    CompositeTypeDirectives? CompositeTypes = null,
    ExtensionDirectives? Extensions = null
)
{
    /// <summary>
    /// A project declaring no directives.
    /// </summary>
    public static ProjectDirectives Empty { get; } = new();

    /// <summary>
    /// The schema directives.
    /// </summary>
    public SchemaDirectives Schemas { get; init; } = Schemas ?? new();

    /// <summary>
    /// The table directives, column renames included.
    /// </summary>
    public TableDirectives Tables { get; init; } = Tables ?? new();

    /// <summary>
    /// The view directives.
    /// </summary>
    public ViewDirectives Views { get; init; } = Views ?? new();

    /// <summary>
    /// The enum-type directives.
    /// </summary>
    public EnumDirectives Enums { get; init; } = Enums ?? new();

    /// <summary>
    /// The sequence directives.
    /// </summary>
    public SequenceDirectives Sequences { get; init; } = Sequences ?? new();

    /// <summary>
    /// The routine directives.
    /// </summary>
    public RoutineDirectives Routines { get; init; } = Routines ?? new();

    /// <summary>
    /// The domain directives.
    /// </summary>
    public DomainDirectives Domains { get; init; } = Domains ?? new();

    /// <summary>
    /// The composite-type directives.
    /// </summary>
    public CompositeTypeDirectives CompositeTypes { get; init; } = CompositeTypes ?? new();

    /// <summary>
    /// The extension directives (drops only).
    /// </summary>
    public ExtensionDirectives Extensions { get; init; } = Extensions ?? new();
}
