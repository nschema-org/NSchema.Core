using NSchema.Diff.Domain.Models.CompositeTypes;
using NSchema.Diff.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.CompositeTypes;
using NSchema.Project.Domain.Models.Schemas;

namespace NSchema.Diff.Domain;

internal sealed partial class SchemaComparer
{
    private static List<CompositeTypeDiff> CompareCompositeTypes(string schemaName, IReadOnlyList<CompositeType> current, SchemaDefinition desired) =>
        CompareObjects(schemaName, "composite type", current, desired.CompositeTypes, desired.DroppedCompositeTypes, desired.IsPartial,
            type => new CompositeTypeDiff(schemaName, type.Name, ChangeKind.Remove),
            type => BuildNewCompositeType(schemaName, type),
            (currentType, desiredType) => BuildModifiedCompositeType(schemaName, currentType, desiredType));

    private static CompositeTypeDiff BuildNewCompositeType(string schema, CompositeType type) =>
        new(schema, type.Name, ChangeKind.Add, Definition: type, Comment: ValueChanges.Changed(null, type.Comment));

    // A composite type's every change is applied in place (ALTER TYPE), so there is no recreate: a rename, the
    // comment, and each field add/drop/retype are tracked independently. Fields are matched by name; a type
    // change on a matched field is an in-place retype, not a drop + add.
    private static CompositeTypeDiff? BuildModifiedCompositeType(string schema, CompositeType current, CompositeType desired)
    {
        var renamedFrom = current.Name == desired.Name ? null : current.Name;
        var comment = ValueChanges.Changed(current.Comment, desired.Comment);
        var fields = CompareCompositeFields(current.Fields, desired.Fields);

        if (renamedFrom is null && comment is null && fields.Count == 0)
        {
            return null;
        }

        return new CompositeTypeDiff(schema, desired.Name, ChangeKind.Modify, renamedFrom, null, fields, comment);
    }

    private static List<CompositeFieldDiff> CompareCompositeFields(IReadOnlyList<CompositeField> current, IReadOnlyList<CompositeField> desired)
    {
        var result = new List<CompositeFieldDiff>();

        foreach (var currentField in current)
        {
            var match = desired.FirstOrDefault(d => string.Equals(d.Name, currentField.Name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                result.Add(new CompositeFieldDiff(ChangeKind.Remove, currentField.Name));
            }
            else if (match.DataType != currentField.DataType)
            {
                result.Add(new CompositeFieldDiff(ChangeKind.Modify, currentField.Name, Type: new ValueChange<SqlType>(currentField.DataType, match.DataType)));
            }
        }

        foreach (var desiredField in desired)
        {
            if (!current.Any(c => string.Equals(c.Name, desiredField.Name, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(new CompositeFieldDiff(ChangeKind.Add, desiredField.Name, desiredField));
            }
        }

        return result;
    }
}
