using Microsoft.Extensions.Options;
using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Policies;

namespace NSchema.Diff.Policies;

/// <summary>
/// A diff policy that checks for destructive changes and applies the configured policy.
/// Non-fatal outcomes are returned as Info/Warning diagnostics so the pipeline can surface them without aborting.
/// </summary>
internal sealed class DestructiveActionDiffPolicy(IOptions<DestructiveActionOptions> options) : IDiffPolicy
{
    private const string PolicyName = "destructive-actions";

    public IEnumerable<PolicyDiagnostic> Validate(DatabaseDiff diff)
    {
        var destructive = DestructiveChanges(diff).Distinct().ToList();
        if (destructive.Count == 0)
        {
            return [];
        }

        var typeString = string.Join(", ", destructive);

        return options.Value.Policy switch
        {
            DestructiveActionPolicy.Allow => [PolicyDiagnostic.Info(PolicyName, $"Allowing destructive actions in migration plan: {typeString}.")],
            DestructiveActionPolicy.Warn => [PolicyDiagnostic.Warning(PolicyName, $"Migration plan contains destructive actions: {typeString}.")],
            _ => [PolicyDiagnostic.Error(PolicyName, $"Destructive actions blocked by policy: {typeString}.")]
        };
    }

    private static IEnumerable<string> DestructiveChanges(DatabaseDiff diff)
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

                foreach (var pk in table.PrimaryKey.Where(c => c.Kind == ChangeKind.Remove))
                {
                    yield return nameof(DropPrimaryKey);
                }

                foreach (var fk in table.ForeignKeys.Where(c => c.Kind == ChangeKind.Remove))
                {
                    yield return nameof(DropForeignKey);
                }

                // Dropping a unique constraint removes a structural guarantee (and may be a foreign-key target),
                // so it is destructive; dropping a check only loosens validation and is not.
                foreach (var unique in table.UniqueConstraints.Where(c => c.Kind == ChangeKind.Remove))
                {
                    yield return nameof(DropUniqueConstraint);
                }
            }

            foreach (var view in schema.Views.Where(v => v.Kind == ChangeKind.Remove))
            {
                yield return nameof(DropView);
            }

            foreach (var enumDiff in schema.Enums.Where(e => e.Kind == ChangeKind.Remove))
            {
                yield return nameof(DropEnum);
            }

            foreach (var sequence in schema.Sequences.Where(s => s.Kind == ChangeKind.Remove))
            {
                yield return nameof(DropSequence);
            }
        }
    }
}
