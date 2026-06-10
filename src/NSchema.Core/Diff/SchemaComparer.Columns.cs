using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

internal sealed partial class SchemaComparer
{
    private List<ColumnDiff> CompareColumns(string schemaName, string tableName, IReadOnlyList<Column> current, IReadOnlyList<Column> desired)
    {
        var result = new List<ColumnDiff>();
        var (forDesired, currentMatched) = MatchEntities(current, desired, c => c.Name, c => c.OldName, "column", $"{schemaName}.{tableName}");

        for (var j = 0; j < current.Count; j++)
        {
            if (currentMatched[j])
            {
                LogColumnExists(schemaName, tableName, current[j].Name);
            }
            else
            {
                LogColumnNotInDesired(schemaName, tableName, current[j].Name);
                result.Add(new ColumnDiff(current[j].Name, ChangeKind.Remove, current[j], null, null, null, null, null, null));
            }
        }

        for (var i = 0; i < desired.Count; i++)
        {
            var desiredCol = desired[i];
            if (forDesired[i] is not { } matchingCurrent)
            {
                LogColumnNew(schemaName, tableName, desiredCol.Name);
                var comment = desiredCol.Comment is not null ? new ValueChange<string>(null, desiredCol.Comment) : null;
                result.Add(new ColumnDiff(desiredCol.Name, ChangeKind.Add, desiredCol, null, null, null, null, null, comment));
            }
            else if (BuildModifiedColumn(schemaName, tableName, matchingCurrent, desiredCol) is { } col)
            {
                result.Add(col);
            }
        }

        return result;
    }

    private ColumnDiff? BuildModifiedColumn(string schemaName, string tableName, Column current, Column desired)
    {
        string? renamedFrom = null;
        if (current.Name == desired.Name)
        {
            LogColumnUnchanged(schemaName, tableName, desired.Name);
        }
        else
        {
            LogColumnRenamed(schemaName, tableName, current.Name, desired.Name);
            renamedFrom = current.Name;
        }

        ValueChange<SqlType>? type = null;
        if (current.Type == desired.Type)
        {
            LogColumnTypeUnchanged(schemaName, tableName, desired.Name, desired.Type);
        }
        else
        {
            LogColumnTypeChanged(schemaName, tableName, desired.Name, current.Type, desired.Type);
            type = new ValueChange<SqlType>(current.Type, desired.Type);
        }

        ValueChange<bool>? nullability = null;
        if (current.IsNullable == desired.IsNullable)
        {
            LogColumnNullabilityUnchanged(schemaName, tableName, desired.Name, desired.IsNullable ? "NULL" : "NOT NULL");
        }
        else
        {
            LogColumnNullabilityChanged(schemaName, tableName, desired.Name, current.IsNullable, desired.IsNullable);
            nullability = new ValueChange<bool>(current.IsNullable, desired.IsNullable);
        }

        ValueChange<string>? @default = null;
        if (current.DefaultExpression == desired.DefaultExpression)
        {
            LogColumnDefaultUnchanged(schemaName, tableName, desired.Name, desired.DefaultExpression ?? "no default");
        }
        else
        {
            LogColumnDefaultChanged(schemaName, tableName, desired.Name, current.DefaultExpression, desired.DefaultExpression);
            @default = new ValueChange<string>(current.DefaultExpression, desired.DefaultExpression);
        }

        ValueChange<string>? comment = null;
        if (current.Comment != desired.Comment)
        {
            LogColumnCommentChanged(schemaName, tableName, desired.Name);
            comment = new ValueChange<string>(current.Comment, desired.Comment);
        }

        // Identity changes when the column is toggled into or out of identity, or when both columns are
        // identity but their sequence options differ. Old/New options are null on the side that isn't identity.
        ValueChange<IdentityOptions>? identity = null;
        var identityToggled = current.IsIdentity != desired.IsIdentity;
        var identityOptionsChanged = current.IsIdentity && desired.IsIdentity && current.IdentityOptions != desired.IdentityOptions;
        if (identityToggled || identityOptionsChanged)
        {
            var oldOptions = current.IsIdentity ? current.IdentityOptions : null;
            var newOptions = desired.IsIdentity ? desired.IdentityOptions : null;
            LogColumnIdentityChanged(schemaName, tableName, desired.Name,
                oldOptions?.StartWith, newOptions?.StartWith,
                oldOptions?.MinValue, newOptions?.MinValue,
                oldOptions?.IncrementBy, newOptions?.IncrementBy);
            identity = new ValueChange<IdentityOptions>(oldOptions, newOptions);
        }

        if (renamedFrom is null && type is null && nullability is null && @default is null && comment is null && identity is null)
        {
            return null;
        }

        return new ColumnDiff(desired.Name, ChangeKind.Modify, null, renamedFrom, type, nullability, @default, identity, comment);
    }
}
