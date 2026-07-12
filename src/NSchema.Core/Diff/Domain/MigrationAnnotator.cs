using NSchema.Diff.Domain.Models.Tables;
using NSchema.Diff.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Diff.Domain;

/// <summary>
/// Matches declared change-event scripts to the structural changes in a diff, annotating each matched node with its script's name.
/// </summary>
internal static class MigrationAnnotator
{
    public static MigrationAnnotationResult Annotate(DatabaseDiff diff, IReadOnlyList<Script> migrations)
    {
        if (migrations.Count == 0)
        {
            return new MigrationAnnotationResult(diff, []);
        }

        var matched = new HashSet<Script>();
        var schemas = diff.Schemas
            .Select(schema => schema with { Tables = schema.Tables.Select(t => Annotate(schema.Name, t, migrations, matched)).ToList() })
            .ToList();

        var unmatched = migrations.Where(m => !matched.Contains(m)).ToList();
        return new MigrationAnnotationResult(diff with { Schemas = schemas }, unmatched);
    }

    private static TableDiff Annotate(string schemaName, TableDiff table, IReadOnlyList<Script> migrations, HashSet<Script> matched)
    {
        if (table.Kind != ChangeKind.Modify)
        {
            return table;
        }

        var candidates = migrations
            .Where(m => m.Event is ChangeEvent e
                && string.Equals(e.ScopeSchema, schemaName, StringComparison.OrdinalIgnoreCase)
                && e.TableName.Equals(table.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (candidates.Count == 0)
        {
            return table;
        }

        return table with
        {
            Columns = table.Columns.Select(column => column switch
            {
                { Kind: ChangeKind.Add } when Take(ChangeTrigger.AddColumn, column.Name) is { } m => column with { MigrationScript = m.Name },
                { Kind: ChangeKind.Modify, Type: not null } when Take(ChangeTrigger.AlterColumnType, column.Name) is { } m => column with { MigrationScript = m.Name },
                _ => column,
            }).ToList(),
            PrimaryKey = table.PrimaryKey
                .Select(pk => pk.Kind == ChangeKind.Add && Take(ChangeTrigger.AddConstraint, pk.Name) is { } m ? pk with { MigrationScript = m.Name } : pk)
                .ToList(),
            UniqueConstraints = table.UniqueConstraints
                .Select(uc => uc.Kind == ChangeKind.Add && Take(ChangeTrigger.AddConstraint, uc.Name) is { } m ? uc with { MigrationScript = m.Name } : uc)
                .ToList(),
            ForeignKeys = table.ForeignKeys
                .Select(fk => fk.Kind == ChangeKind.Add && Take(ChangeTrigger.AddConstraint, fk.Name) is { } m ? fk with { MigrationScript = m.Name } : fk)
                .ToList(),
            Checks = table.Checks
                .Select(check => check.Kind == ChangeKind.Add && Take(ChangeTrigger.AddConstraint, check.Name) is { } m ? check with { MigrationScript = m.Name } : check)
                .ToList(),
            ExclusionConstraints = table.ExclusionConstraints
                .Select(ex => ex.Kind == ChangeKind.Add && Take(ChangeTrigger.AddConstraint, ex.Name) is { } m ? ex with { MigrationScript = m.Name } : ex)
                .ToList(),
        };

        Script? Take(ChangeTrigger trigger, string memberName)
        {
            var migration = candidates.FirstOrDefault(m =>
                m.Event is ChangeEvent e && e.Trigger == trigger && e.MemberName.Equals(memberName, StringComparison.OrdinalIgnoreCase));
            if (migration is not null)
            {
                matched.Add(migration);
            }
            return migration;
        }
    }
}
