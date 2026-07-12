using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models.Tables;
namespace NSchema.Diff.Domain.Models;

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
    string Name { get; }

    /// <summary>
    /// The change to the object.
    /// </summary>
    ChangeKind Kind { get; }
}
