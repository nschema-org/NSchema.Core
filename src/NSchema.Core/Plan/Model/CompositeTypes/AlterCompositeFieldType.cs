using NSchema.Model;
using NSchema.Model.Columns;

namespace NSchema.Plan.Domain.Models.CompositeTypes;

/// <summary>
/// Represents changing the type of a composite type's field (<c>ALTER TYPE … ALTER ATTRIBUTE … TYPE …</c>).
/// </summary>
/// <param name="SchemaName">The name of the schema containing the composite type.</param>
/// <param name="TypeName">The name of the composite type.</param>
/// <param name="FieldName">The name of the field whose type changes.</param>
/// <param name="OldType">The field's type before the change.</param>
/// <param name="NewType">The field's type after the change.</param>
public sealed record AlterCompositeFieldType(SqlIdentifier SchemaName, SqlIdentifier TypeName, SqlIdentifier FieldName, SqlType OldType, SqlType NewType) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
