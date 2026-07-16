using NSchema.Diff.Model.CompositeTypes;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Schemas;

namespace NSchema.Diff.Model.Services;

internal sealed partial class DatabaseComparer
{
    private static List<CompositeTypeDiff> CompareCompositeTypes(SqlIdentifier schemaName, SqlIdentifier currentSchemaName, IReadOnlyList<CompositeType> current, Schema desired, DirectiveLookup directives) =>
        CompareObjects(schemaName, "composite type", current, desired.CompositeTypes,
            directives.CompositeTypeRenames(currentSchemaName), directives.CompositeTypeDrops(currentSchemaName), directives.IsPartial(schemaName),
            type => new CompositeTypeDiff(schemaName, type.Name, ChangeKind.Remove),
            type => BuildNewCompositeType(schemaName, type),
            (currentType, desiredType) => BuildModifiedCompositeType(schemaName, currentType, desiredType));

    private static CompositeTypeDiff BuildNewCompositeType(SqlIdentifier schema, CompositeType type) =>
        new(schema, type.Name, ChangeKind.Add, Definition: type, Comment: ValueChanges.Changed(null, type.Comment));

    // A composite type's every change is applied in place (ALTER TYPE), so there is no recreate: a rename, the
    // comment, and each field add/drop/retype are tracked independently. Fields are matched by name; a type
    // change on a matched field is an in-place retype, not a drop + add.
    private static CompositeTypeDiff? BuildModifiedCompositeType(SqlIdentifier schema, CompositeType current, CompositeType desired)
    {
        var renamedFrom = current.Name == desired.Name ? (SqlIdentifier?)null : current.Name;
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
            var match = desired.FirstOrDefault(d => d.Name == currentField.Name);
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
            if (current.All(c => c.Name != desiredField.Name))
            {
                result.Add(new CompositeFieldDiff(ChangeKind.Add, desiredField.Name, desiredField));
            }
        }

        return result;
    }
}
