using NSchema.Model;
using NSchema.Model.Columns;

namespace NSchema.Plan.Model.Columns;

/// <summary>
/// Represents altering the nullability of an existing column in the database schema.
/// </summary>
/// <param name="Column">The address of the column.</param>
/// <param name="OldNullable">A boolean value indicating the current nullability of the column.</param>
/// <param name="NewNullable">A boolean value indicating the new nullability of the column after alteration.</param>
/// <param name="ColumnType">The column's data type in its final (desired) state.</param>
/// <remarks>
/// This action can either make a column nullable or non-nullable, depending on the specified parameters.
/// Altering a column's nullability may lead to data loss if changing from nullable to non-nullable, as existing null values would need to be handled appropriately.
/// </remarks>
public sealed record AlterColumnNullability(
    MemberAddress Column,
    bool OldNullable,
    bool NewNullable,
    SqlType? ColumnType = null
) : MigrationAction;
