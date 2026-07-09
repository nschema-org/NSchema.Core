using NSchema.Diff.Model;
using NSchema.Schema.Model.Migrations;

namespace NSchema.Diff;

/// <summary>
/// Matches declared data migrations to the structural changes in a diff, annotating each matched node with its migration.
/// </summary>
internal static class MigrationMatcher
{
    public static (DatabaseDiff Diff, IReadOnlyList<DataMigration> Unmatched) Apply(DatabaseDiff diff, IReadOnlyList<DataMigration> migrations)
    {
        if (migrations.Count == 0)
        {
            return (diff, []);
        }

        var matched = new HashSet<DataMigration>();
        var schemas = diff.Schemas
            .Select(schema => schema with { Tables = schema.Tables.Select(t => Annotate(schema.Name, t, migrations, matched)).ToList() })
            .ToList();

        var unmatched = migrations.Where(m => !matched.Contains(m)).ToList();
        return (diff with { Schemas = schemas }, unmatched);
    }

    private static TableDiff Annotate(string schemaName, TableDiff table, IReadOnlyList<DataMigration> migrations, HashSet<DataMigration> matched)
    {
        if (table.Kind != ChangeKind.Modify)
        {
            return table;
        }

        var candidates = migrations
            .Where(m => m.SchemaName.Equals(schemaName, StringComparison.OrdinalIgnoreCase) && m.ObjectName.Equals(table.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (candidates.Count == 0)
        {
            return table;
        }

        return table with
        {
            Columns = table.Columns.Select(column => column switch
            {
                { Kind: ChangeKind.Add } when Take(DataMigrationTrigger.AddColumn, column.Name) is { } m => column with { Migration = m },
                { Kind: ChangeKind.Modify, Type: not null } when Take(DataMigrationTrigger.AlterColumnType, column.Name) is { } m => column with { Migration = m },
                _ => column,
            }).ToList(),
            PrimaryKey = table.PrimaryKey
                .Select(pk => pk.Kind == ChangeKind.Add && Take(DataMigrationTrigger.AddConstraint, pk.Name) is { } m ? pk with { Migration = m } : pk)
                .ToList(),
            UniqueConstraints = table.UniqueConstraints
                .Select(uc => uc.Kind == ChangeKind.Add && Take(DataMigrationTrigger.AddConstraint, uc.Name) is { } m ? uc with { Migration = m } : uc)
                .ToList(),
            ForeignKeys = table.ForeignKeys
                .Select(fk => fk.Kind == ChangeKind.Add && Take(DataMigrationTrigger.AddConstraint, fk.Name) is { } m ? fk with { Migration = m } : fk)
                .ToList(),
            Checks = table.Checks
                .Select(check => check.Kind == ChangeKind.Add && Take(DataMigrationTrigger.AddConstraint, check.Name) is { } m ? check with { Migration = m } : check)
                .ToList(),
            ExclusionConstraints = table.ExclusionConstraints
                .Select(ex => ex.Kind == ChangeKind.Add && Take(DataMigrationTrigger.AddConstraint, ex.Name) is { } m ? ex with { Migration = m } : ex)
                .ToList(),
        };

        DataMigration? Take(DataMigrationTrigger trigger, string memberName)
        {
            var migration = candidates.FirstOrDefault(m => m.Trigger == trigger && m.MemberName.Equals(memberName, StringComparison.OrdinalIgnoreCase));
            if (migration is not null)
            {
                matched.Add(migration);
            }
            return migration;
        }
    }
}
