using NSchema.Model.Scripts;

namespace NSchema.Project.Domain.Models;

/// <summary>
/// The management directives a project declares.
/// </summary>
public sealed record ProjectDirectives(
    SchemaDirectives? Schemas = null,
    TableDirectives? Tables = null,
    ViewDirectives? Views = null,
    EnumDirectives? Enums = null,
    SequenceDirectives? Sequences = null,
    RoutineDirectives? Routines = null,
    DomainDirectives? Domains = null,
    CompositeTypeDirectives? CompositeTypes = null,
    ExtensionDirectives? Extensions = null,
    IReadOnlyList<DeploymentScript>? DeploymentScripts = null
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

    /// <summary>
    /// The deployment scripts.
    /// </summary>
    public IReadOnlyList<DeploymentScript> DeploymentScripts { get; init; } = DeploymentScripts ?? [];
}
