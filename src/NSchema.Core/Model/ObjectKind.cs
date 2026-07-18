namespace NSchema.Model;

/// <summary>
/// The kind of a schema-level object: what sort of thing lives at an address.
/// </summary>
public enum ObjectKind
{
    /// <summary>
    /// A table.
    /// </summary>
    Table,

    /// <summary>
    /// A view.
    /// </summary>
    View,

    /// <summary>
    /// An enum type.
    /// </summary>
    Enum,

    /// <summary>
    /// A sequence.
    /// </summary>
    Sequence,

    /// <summary>
    /// A routine (function or procedure — one name space).
    /// </summary>
    Routine,

    /// <summary>
    /// A domain.
    /// </summary>
    Domain,

    /// <summary>
    /// A composite type.
    /// </summary>
    CompositeType,

    /// <summary>
    /// An extension.
    /// </summary>
    Extension,
}

/// <summary>
/// Rendering for <see cref="ObjectKind"/>.
/// </summary>
internal static class ObjectKindExtensions
{
    /// <summary>
    /// The kind as display prose (e.g. <c>"composite type"</c>), for diagnostics.
    /// </summary>
    public static string Display(this ObjectKind kind) => kind switch
    {
        ObjectKind.Table => "table",
        ObjectKind.View => "view",
        ObjectKind.Enum => "enum",
        ObjectKind.Sequence => "sequence",
        ObjectKind.Routine => "routine",
        ObjectKind.Domain => "domain",
        ObjectKind.CompositeType => "composite type",
        ObjectKind.Extension => "extension",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}


