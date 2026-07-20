using NSchema.Plan.Model;
using NSchema.Plan.Model.Constraints;

namespace NSchema.Plan.Backends;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders adding a check constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AddCheckConstraint(AddCheckConstraint action) =>
        Statement($"ALTER TABLE {Qualify(action.Table)} ADD CONSTRAINT {Quote(action.CheckConstraint.Name)} CHECK ({action.CheckConstraint.Expression})");

    /// <summary>
    /// Renders dropping a check constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropCheckConstraint(DropCheckConstraint action) =>
        Statement($"ALTER TABLE {Qualify(action.Constraint.Owner)} DROP CONSTRAINT {Quote(action.Constraint.Member)}");

    /// <summary>
    /// Renders adding a unique constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AddUniqueConstraint(AddUniqueConstraint action) =>
        Statement($"ALTER TABLE {Qualify(action.Table)} ADD CONSTRAINT {Quote(action.UniqueConstraint.Name)} UNIQUE ({ColumnList(action.UniqueConstraint.ColumnNames)})");

    /// <summary>
    /// Renders dropping a unique constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropUniqueConstraint(DropUniqueConstraint action) =>
        Statement($"ALTER TABLE {Qualify(action.Constraint.Owner)} DROP CONSTRAINT {Quote(action.Constraint.Member)}");

    /// <summary>
    /// Renders adding an exclusion constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AddExclusionConstraint(AddExclusionConstraint action) =>
        Unsupported(action);

    /// <summary>
    /// Renders dropping an exclusion constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropExclusionConstraint(DropExclusionConstraint action) =>
        Unsupported(action);

    /// <summary>
    /// Renders setting or clearing a constraint's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetConstraintComment(SetConstraintComment action) =>
        Unsupported(action);
}
