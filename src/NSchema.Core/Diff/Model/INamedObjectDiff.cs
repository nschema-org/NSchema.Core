using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Tables;
using NSchema.Model;

namespace NSchema.Diff.Model;

/// <summary>
/// The members every named-object diff shares — table members (columns, constraints, indexes) as well as
/// schema-level objects. Backs kind-agnostic consumers; see <see cref="TableDiff.EnumerateMembers"/> and
/// <see cref="SchemaDiff.EnumerateObjects"/>.
/// </summary>
public interface INamedObjectDiff
{
    /// <summary>
    /// The object name.
    /// </summary>
    SqlIdentifier Name { get; }

    /// <summary>
    /// The change to the object.
    /// </summary>
    ChangeKind Kind { get; }
}
