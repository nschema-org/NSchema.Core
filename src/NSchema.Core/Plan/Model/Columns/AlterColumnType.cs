using NSchema.Model;
using NSchema.Model.Columns;

namespace NSchema.Plan.Model.Columns;

/// <summary>
/// Represents changing the data type of an existing column in the database schema.
/// </summary>
/// <param name="Column">The address of the column.</param>
/// <param name="OldType">The current data type of the column before alteration.</param>
/// <param name="NewType">The new data type of the column after alteration.</param>
/// <param name="IsNullable">The column's nullability in its final (desired) state.</param>
/// <remarks>
/// This action may involve data transformation and can potentially lead to data loss if the new type is not compatible with the existing data.
/// Therefore, it is considered a destructive migration action.
/// </remarks>
public sealed record AlterColumnType(
    MemberAddress Column,
    SqlType OldType,
    SqlType NewType,
    bool? IsNullable = null
) : MigrationAction;
