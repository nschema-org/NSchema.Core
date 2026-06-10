using System.Diagnostics;

namespace NSchema.Schema.Model;

/// <summary>
/// Represents the definition of a database schema.
/// </summary>
/// <param name="Name">The name of the schema.</param>
/// <param name="OldName">The previous name of the schema, if it has been renamed.</param>
/// <param name="IsPartial">Indicates whether the schema definition is partial, meaning it may not include all details of the schema.</param>
/// <param name="Comment">An optional comment or description for the schema.</param>
/// <param name="Tables">A list of tables that are part of the schema.</param>
/// <param name="DroppedTables">A list of tables that have been dropped from the schema.</param>
/// <param name="Grants">A list of grants that define the permissions associated with the schema.</param>
/// <param name="Views">A list of views that are part of the schema.</param>
/// <param name="DroppedViews">A list of views that have been dropped from the schema.</param>
[DebuggerDisplay("{Name,nq} ({Tables.Count} tables)")]
public record SchemaDefinition(
    string Name,
    string? OldName = null,
    bool IsPartial = false,
    string? Comment = null,
    IReadOnlyList<Table>? Tables = null,
    IReadOnlyList<string>? DroppedTables = null,
    IReadOnlyList<SchemaGrant>? Grants = null,
    IReadOnlyList<View>? Views = null,
    IReadOnlyList<string>? DroppedViews = null
)
{
    /// <summary>
    /// A list of tables that are part of the schema.
    /// </summary>
    public IReadOnlyList<Table> Tables { get; init; } = Tables ?? [];

    /// <summary>
    /// A list of tables that have been dropped from the schema.
    /// </summary>
    public IReadOnlyList<string> DroppedTables { get; init; } = DroppedTables ?? [];

    /// <summary>
    /// A list of views that are part of the schema.
    /// </summary>
    public IReadOnlyList<View> Views { get; init; } = Views ?? [];

    /// <summary>
    /// A list of views that have been dropped from the schema.
    /// </summary>
    public IReadOnlyList<string> DroppedViews { get; init; } = DroppedViews ?? [];

    /// <summary>
    /// A list of grants that define the permissions associated with the schema.
    /// </summary>
    public IReadOnlyList<SchemaGrant> Grants { get; init; } = Grants ?? [];
}
