using NSchema.Diff.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Domains;
using NSchema.Schema.Model.Schemas;

namespace NSchema.Diff;

internal sealed partial class SchemaComparer
{
    private List<DomainDiff> CompareDomains(string schemaName, IReadOnlyList<Domain> current, SchemaDefinition desired) =>
        CompareObjects(schemaName, "domain", current, desired.Domains, desired.DroppedDomains, desired.IsPartial,
            domain => new DomainDiff(schemaName, domain.Name, ChangeKind.Remove),
            domain => BuildNewDomain(schemaName, domain),
            (currentDomain, desiredDomain) => BuildModifiedDomain(schemaName, currentDomain, desiredDomain));

    private static DomainDiff BuildNewDomain(string schema, Domain domain) =>
        new(schema, domain.Name, ChangeKind.Add, Definition: domain,
            Comment: ValueChanges.Changed(null, domain.Comment));

    // The base type cannot be altered in place (no ALTER DOMAIN … TYPE), so a change to it is a drop + recreate;
    // the default, not-null and checks then ride along on the definition. Every other change (default, not-null,
    // checks, comment, rename) is applied in place, since a domain is depended on by columns and must not be
    // dropped to be modified.
    private DomainDiff? BuildModifiedDomain(string schema, Domain current, Domain desired)
    {
        var renamedFrom = current.Name == desired.Name ? null : current.Name;
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
            : CompareTableMembers(schema, desired.Name, "Domain check", current.Checks, desired.Checks,
                (kind, name, definition, checkComment) => new CheckConstraintDiff(kind, name, definition, checkComment));

        if (renamedFrom is null && dataType is null && @default is null && notNull is null && checks.Count == 0 && comment is null)
        {
            return null;
        }

        return new DomainDiff(schema, desired.Name, ChangeKind.Modify, renamedFrom,
            requiresRecreate ? desired : null, dataType, @default, notNull, checks, comment);
    }
}
