using System.Diagnostics;
using NSchema.Schema.Model.Enums;
using NSchema.Schema.Model.Functions;
using NSchema.Schema.Model.Procedures;
using NSchema.Schema.Model.Sequences;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Views;

namespace NSchema.Schema.Model.Schemas;

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
/// <param name="Enums">A list of enum types that are part of the schema.</param>
/// <param name="DroppedEnums">A list of enum types that have been dropped from the schema.</param>
/// <param name="Sequences">A list of sequences that are part of the schema.</param>
/// <param name="DroppedSequences">A list of sequences that have been dropped from the schema.</param>
/// <param name="Functions">A list of functions that are part of the schema.</param>
/// <param name="DroppedFunctions">A list of functions that have been dropped from the schema.</param>
/// <param name="Procedures">A list of procedures that are part of the schema.</param>
/// <param name="DroppedProcedures">A list of procedures that have been dropped from the schema.</param>
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
    IReadOnlyList<string>? DroppedViews = null,
    IReadOnlyList<EnumType>? Enums = null,
    IReadOnlyList<string>? DroppedEnums = null,
    IReadOnlyList<Sequence>? Sequences = null,
    IReadOnlyList<string>? DroppedSequences = null,
    IReadOnlyList<Function>? Functions = null,
    IReadOnlyList<string>? DroppedFunctions = null,
    IReadOnlyList<Procedure>? Procedures = null,
    IReadOnlyList<string>? DroppedProcedures = null
) : IRenameableObject
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
    /// A list of enum types that are part of the schema.
    /// </summary>
    public IReadOnlyList<EnumType> Enums { get; init; } = Enums ?? [];

    /// <summary>
    /// A list of enum types that have been dropped from the schema.
    /// </summary>
    public IReadOnlyList<string> DroppedEnums { get; init; } = DroppedEnums ?? [];

    /// <summary>
    /// A list of sequences that are part of the schema.
    /// </summary>
    public IReadOnlyList<Sequence> Sequences { get; init; } = Sequences ?? [];

    /// <summary>
    /// A list of sequences that have been dropped from the schema.
    /// </summary>
    public IReadOnlyList<string> DroppedSequences { get; init; } = DroppedSequences ?? [];

    /// <summary>
    /// A list of functions that are part of the schema.
    /// </summary>
    public IReadOnlyList<Function> Functions { get; init; } = Functions ?? [];

    /// <summary>
    /// A list of functions that have been dropped from the schema.
    /// </summary>
    public IReadOnlyList<string> DroppedFunctions { get; init; } = DroppedFunctions ?? [];

    /// <summary>
    /// A list of procedures that are part of the schema.
    /// </summary>
    public IReadOnlyList<Procedure> Procedures { get; init; } = Procedures ?? [];

    /// <summary>
    /// A list of procedures that have been dropped from the schema.
    /// </summary>
    public IReadOnlyList<string> DroppedProcedures { get; init; } = DroppedProcedures ?? [];

    /// <summary>
    /// A list of grants that define the permissions associated with the schema.
    /// </summary>
    public IReadOnlyList<SchemaGrant> Grants { get; init; } = Grants ?? [];
}
