using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Constraints;
using NSchema.Diff.Domain.Models.Domains;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Domains;
using NSchema.Project.Domain.Models.Schemas;

namespace NSchema.Diff.Domain;

internal sealed partial class SchemaComparer
{
    private List<DomainDiff> CompareDomains(SqlIdentifier schemaName, IReadOnlyList<DomainDefinition> current, SchemaDefinition desired) =>
        CompareObjects(schemaName, "domain", current, desired.Domains, desired.DroppedDomains, desired.IsPartial,
            domain => new DomainDiff(schemaName, domain.Name, ChangeKind.Remove),
            domain => BuildNewDomain(schemaName, domain),
            (currentDomain, desiredDomain) => BuildModifiedDomain(schemaName, currentDomain, desiredDomain));

    private static DomainDiff BuildNewDomain(SqlIdentifier schema, DomainDefinition domain) =>
        new(schema, domain.Name, ChangeKind.Add, Definition: domain,
            Comment: ValueChanges.Changed(null, domain.Comment));

    // The base type cannot be altered in place (no ALTER DOMAIN … TYPE), so a change to it is a drop + recreate;
    // the default, not-null and checks then ride along on the definition. Every other change (default, not-null,
    // checks, comment, rename) is applied in place, since a domain is depended on by columns and must not be
    // dropped to be modified.
    private DomainDiff? BuildModifiedDomain(SqlIdentifier schema, DomainDefinition current, DomainDefinition desired)
    {
        var renamedFrom = current.Name == desired.Name ? (SqlIdentifier?)null : current.Name;
        var dataType = current.DataType == desired.DataType ? null : new ValueChange<SqlType>(current.DataType, desired.DataType);
        var comment = ValueChanges.Changed(current.Comment, desired.Comment);
        var requiresRecreate = dataType is not null;

        // On a recreate the default/not-null/checks are rebuilt from the definition, so they are not diffed in place.
        var @default = requiresRecreate ? null : ValueChanges.Changed(current.Default, desired.Default);
        var notNull = requiresRecreate || current.NotNull == desired.NotNull
            ? null
            : new ValueChange<bool>(current.NotNull, desired.NotNull);
        IReadOnlyList<CheckConstraintDiff> checks = requiresRecreate
            ? []
            : CompareTableMembers(new ObjectReference(schema, desired.Name), "DomainDefinition check", current.Checks, desired.Checks,
                (kind, name, definition, checkComment) => new CheckConstraintDiff(kind, name, definition, checkComment));

        if (renamedFrom is null && dataType is null && @default is null && notNull is null && checks.Count == 0 && comment is null)
        {
            return null;
        }

        return new DomainDiff(schema, desired.Name, ChangeKind.Modify, renamedFrom,
            requiresRecreate ? desired : null, dataType, @default, notNull, checks, comment);
    }
}
