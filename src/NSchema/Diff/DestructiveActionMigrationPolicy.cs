using Microsoft.Extensions.Options;
using NSchema.Diff.Model;
using NSchema.Migration;
using NSchema.Plan.Model;
using NSchema.Policies;

namespace NSchema.Diff;

/// <summary>
/// A diff policy that checks for destructive changes and applies the configured policy.
/// Non-fatal outcomes are returned as Info/Warning diagnostics so the pipeline can surface them without aborting.
/// </summary>
internal sealed class DestructiveActionMigrationPolicy(IOptions<MigrationOptions> options) : IDiffPolicy
{
    public IEnumerable<PolicyError> Validate(MigrationDiff diff)
    {
        var destructive = DestructiveChanges(diff).Distinct().ToList();
        if (destructive.Count == 0)
        {
            return [];
        }

        var typeString = string.Join(", ", destructive);

        return options.Value.DestructiveActionPolicy switch
        {
            DestructiveActionPolicy.Allow => [new PolicyError(
                nameof(DestructiveActionMigrationPolicy),
                $"Allowing destructive actions in migration plan: {typeString}.",
                PolicySeverity.Info
            )],
            DestructiveActionPolicy.Warn => [new PolicyError(
                nameof(DestructiveActionMigrationPolicy),
                $"Migration plan contains destructive actions: {typeString}.",
                PolicySeverity.Warning
                )],
            _ => [new PolicyError(
                nameof(DestructiveActionMigrationPolicy),
                $"Destructive actions blocked by policy: {typeString}."
            )]
        };
    }

    // Mirrors MigrationAction.IsDestructive: dropped schemas/tables/columns, narrowing column changes, and
    // revoked grants. Labels reuse the action type names so messages stay consistent across the pipeline.
    private static IEnumerable<string> DestructiveChanges(MigrationDiff diff)
    {
        foreach (var schema in diff.Schemas)
        {
            if (schema.Kind == ChangeKind.Remove)
            {
                yield return nameof(DropSchema);
            }

            foreach (var grant in schema.Grants.Where(g => g.Kind == ChangeKind.Remove))
            {
                yield return nameof(RevokeSchemaUsage);
            }

            foreach (var table in schema.Tables)
            {
                if (table.Kind == ChangeKind.Remove)
                {
                    yield return nameof(DropTable);
                }

                foreach (var grant in table.Grants.Where(g => g.Kind == ChangeKind.Remove))
                {
                    yield return nameof(RevokeTablePrivileges);
                }

                foreach (var column in table.Columns)
                {
                    if (column.Kind == ChangeKind.Remove)
                    {
                        yield return nameof(DropColumn);
                    }

                    if (column.Type is not null)
                    {
                        yield return nameof(AlterColumnType);
                    }

                    if (column.Nullability is not null)
                    {
                        yield return nameof(AlterColumnNullability);
                    }
                }
            }
        }
    }
}
