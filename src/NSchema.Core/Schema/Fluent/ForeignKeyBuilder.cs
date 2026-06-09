using NSchema.Schema.Model;

namespace NSchema.Schema.Fluent;

/// <summary>
/// Provides a fluent API for configuring a foreign key constraint in a database schema.
/// </summary>
public sealed class ForeignKeyBuilder
{
    private readonly string _name;
    private readonly IReadOnlyList<string> _columns;
    private readonly string _referencedSchema;
    private readonly string _referencedTable;
    private readonly IReadOnlyList<string> _referencedColumns;
    private ReferentialAction _onDelete = ReferentialAction.NoAction;
    private ReferentialAction _onUpdate = ReferentialAction.NoAction;
    private string? _comment;

    internal ForeignKeyBuilder(
        string name,
        IReadOnlyList<string> columns,
        string referencedSchema,
        string referencedTable,
        IReadOnlyList<string> referencedColumns
    )
    {
        _name = name;
        _columns = columns;
        _referencedSchema = referencedSchema;
        _referencedTable = referencedTable;
        _referencedColumns = referencedColumns;
    }

    /// <summary>
    /// Specifies the referential action to take when a referenced row is deleted or updated.
    /// </summary>
    /// <param name="action">The referential action to take when a referenced row is deleted or updated.</param>
    /// <returns>The current <see cref="ForeignKeyBuilder"/> instance, allowing for method chaining.</returns>
    public ForeignKeyBuilder OnDelete(ReferentialAction action) { _onDelete = action; return this; }

    /// <summary>
    /// Specifies the referential action to take when a referenced row is updated.
    /// </summary>
    /// <param name="action">The referential action to take when a referenced row is updated.</param>
    /// <returns>The current <see cref="ForeignKeyBuilder"/> instance, allowing for method chaining.</returns>
    public ForeignKeyBuilder OnUpdate(ReferentialAction action) { _onUpdate = action; return this; }

    /// <summary>
    /// Adds an optional comment or description to the foreign key constraint.
    /// </summary>
    /// <param name="comment">The comment or description to associate with the foreign key constraint.</param>
    /// <returns>The current <see cref="ForeignKeyBuilder"/> instance, allowing for method chaining.</returns>
    public ForeignKeyBuilder Comment(string? comment) { _comment = comment; return this; }

    internal ForeignKey Build() => new(_name, _columns, _referencedSchema, _referencedTable, _referencedColumns, _onDelete, _onUpdate, _comment);
}
