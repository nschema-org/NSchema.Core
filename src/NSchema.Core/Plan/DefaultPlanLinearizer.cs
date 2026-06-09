using System.Collections.Frozen;
using NSchema.Diff.Model;
using NSchema.Plan.Model;

namespace NSchema.Plan;

/// <summary>
/// Walks the structured diff and produces a migration plan.
/// </summary>
internal sealed class DefaultPlanLinearizer : IPlanLinearizer
{
    private static readonly IReadOnlyDictionary<Type, int> _actionPriorities = new List<Type> {
        typeof(DropForeignKey),
        typeof(DropCheckConstraint),
        typeof(DropUniqueConstraint),
        typeof(DropIndex),
        typeof(DropPrimaryKey),
        typeof(RevokeSchemaUsage),
        typeof(RevokeTablePrivileges),
        typeof(RenameSchema),
        typeof(CreateSchema),
        typeof(RenameTable),
        typeof(CreateTable),
        typeof(DropColumn),
        typeof(RenameColumn),
        typeof(AddColumn),
        typeof(AlterColumnType),
        typeof(AlterColumnNullability),
        typeof(AlterIdentitySequence),
        typeof(SetColumnDefault),
        typeof(AddPrimaryKey),
        typeof(AddUniqueConstraint),
        typeof(AddForeignKey),
        typeof(AddCheckConstraint),
        typeof(CreateIndex),
        typeof(GrantSchemaUsage),
        typeof(GrantTablePrivileges),
        typeof(SetSchemaComment),
        typeof(SetTableComment),
        typeof(SetColumnComment),
        typeof(SetIndexComment),
        typeof(SetConstraintComment),
        typeof(DropTable),
        typeof(DropSchema),
    }.Index().ToFrozenDictionary(x => x.Item, x => x.Index);

    public IReadOnlyList<MigrationAction> Linearize(DatabaseDiff diff)
    {
        var actions = new List<MigrationAction>();
        foreach (var schema in diff.Schemas)
        {
            EmitSchema(schema, actions);
        }

        actions = actions.OrderBy(action => _actionPriorities[action.GetType()]).ToList();
        return actions;
    }

    private static void EmitSchema(SchemaDiff schema, List<MigrationAction> actions)
    {
        switch (schema.Kind)
        {
            case ChangeKind.Add:
                actions.Add(new CreateSchema(schema.Name));
                EmitSchemaAttributes(schema, actions);
                foreach (var table in schema.Tables)
                {
                    EmitTable(table, actions);
                }
                break;

            case ChangeKind.Remove:
                actions.Add(new DropSchema(schema.Name));
                break;

            default: // Modify, or a null-Kind container whose tables changed.
                if (schema.RenamedFrom is not null)
                {
                    actions.Add(new RenameSchema(schema.RenamedFrom, schema.Name));
                }
                EmitSchemaAttributes(schema, actions);
                foreach (var table in schema.Tables)
                {
                    EmitTable(table, actions);
                }
                break;
        }
    }

    private static void EmitSchemaAttributes(SchemaDiff schema, List<MigrationAction> actions)
    {
        if (schema.Comment is not null)
        {
            actions.Add(new SetSchemaComment(schema.Name, schema.Comment.Old, schema.Comment.New));
        }

        foreach (var grant in schema.Grants)
        {
            actions.Add(grant.Kind == ChangeKind.Add
                ? new GrantSchemaUsage(schema.Name, grant.Role)
                : new RevokeSchemaUsage(schema.Name, grant.Role));
        }
    }

    private static void EmitTable(TableDiff table, List<MigrationAction> actions)
    {
        switch (table.Kind)
        {
            case ChangeKind.Add:
                // The primary key and columns are created inline by CREATE TABLE (carried on Definition); only
                // the foreign keys, indexes, comments and grants arrive as separate actions.
                actions.Add(new CreateTable(table.Schema, table.Definition!));
                if (table.Comment is not null)
                {
                    actions.Add(new SetTableComment(table.Schema, table.Name, table.Comment.Old, table.Comment.New));
                }
                foreach (var column in table.Columns.Where(c => c.Comment is not null))
                {
                    actions.Add(new SetColumnComment(table.Schema, table.Name, column.Name, column.Comment!.Old, column.Comment.New));
                }
                EmitConstraints(table, actions);
                EmitIndexes(table, actions);
                EmitGrants(table, actions);
                break;

            case ChangeKind.Remove:
                actions.Add(new DropTable(table.Schema, table.Name));
                break;

            default: // Modify
                if (table.RenamedFrom is not null)
                {
                    actions.Add(new RenameTable(table.Schema, table.RenamedFrom, table.Name));
                }
                if (table.Comment is not null)
                {
                    actions.Add(new SetTableComment(table.Schema, table.Name, table.Comment.Old, table.Comment.New));
                }
                foreach (var column in table.Columns)
                {
                    EmitColumn(table, column, actions);
                }
                EmitConstraints(table, actions);
                EmitIndexes(table, actions);
                EmitGrants(table, actions);
                break;
        }
    }

    private static void EmitColumn(TableDiff table, ColumnDiff column, List<MigrationAction> actions)
    {
        switch (column.Kind)
        {
            case ChangeKind.Add:
                actions.Add(new AddColumn(table.Schema, table.Name, column.Definition!));
                if (column.Comment is not null)
                {
                    actions.Add(new SetColumnComment(table.Schema, table.Name, column.Name, column.Comment.Old, column.Comment.New));
                }
                break;

            case ChangeKind.Remove:
                actions.Add(new DropColumn(table.Schema, table.Name, column.Definition!));
                break;

            case ChangeKind.Modify:
                if (column.RenamedFrom is not null)
                {
                    actions.Add(new RenameColumn(table.Schema, table.Name, column.RenamedFrom, column.Name));
                }
                if (column.Type is not null)
                {
                    actions.Add(new AlterColumnType(table.Schema, table.Name, column.Name, column.Type.Old!, column.Type.New!));
                }
                if (column.Nullability is not null)
                {
                    actions.Add(new AlterColumnNullability(table.Schema, table.Name, column.Name, column.Nullability.Old, column.Nullability.New));
                }
                if (column.Default is not null)
                {
                    actions.Add(new SetColumnDefault(table.Schema, table.Name, column.Name, column.Default.Old, column.Default.New));
                }
                if (column.Identity is not null)
                {
                    actions.Add(new AlterIdentitySequence(table.Schema, table.Name, column.Name, column.Identity.Old, column.Identity.New));
                }
                if (column.Comment is not null)
                {
                    actions.Add(new SetColumnComment(table.Schema, table.Name, column.Name, column.Comment.Old, column.Comment.New));
                }
                break;
            default: throw new NotSupportedException($"Cannot linearize column change {column.Kind}.");
        }
    }

    private static void EmitConstraints(TableDiff table, List<MigrationAction> actions)
    {
        foreach (var pk in table.PrimaryKey)
        {
            actions.Add(pk.Kind switch
            {
                ChangeKind.Add => new AddPrimaryKey(table.Schema, table.Name, pk.Definition!),
                ChangeKind.Remove => new DropPrimaryKey(table.Schema, table.Name, pk.Name),
                _ => new SetConstraintComment(table.Schema, table.Name, pk.Name, pk.Comment!.Old, pk.Comment.New),
            });
        }

        foreach (var fk in table.ForeignKeys)
        {
            actions.Add(fk.Kind switch
            {
                ChangeKind.Add => new AddForeignKey(table.Schema, table.Name, fk.Definition!),
                ChangeKind.Remove => new DropForeignKey(table.Schema, table.Name, fk.Name),
                _ => new SetConstraintComment(table.Schema, table.Name, fk.Name, fk.Comment!.Old, fk.Comment.New),
            });
        }

        foreach (var uq in table.UniqueConstraints)
        {
            actions.Add(uq.Kind switch
            {
                ChangeKind.Add => new AddUniqueConstraint(table.Schema, table.Name, uq.Definition!),
                ChangeKind.Remove => new DropUniqueConstraint(table.Schema, table.Name, uq.Name),
                _ => new SetConstraintComment(table.Schema, table.Name, uq.Name, uq.Comment!.Old, uq.Comment.New),
            });
        }

        foreach (var ck in table.Checks)
        {
            actions.Add(ck.Kind switch
            {
                ChangeKind.Add => new AddCheckConstraint(table.Schema, table.Name, ck.Definition!),
                ChangeKind.Remove => new DropCheckConstraint(table.Schema, table.Name, ck.Name),
                _ => new SetConstraintComment(table.Schema, table.Name, ck.Name, ck.Comment!.Old, ck.Comment.New),
            });
        }
    }

    private static void EmitIndexes(TableDiff table, List<MigrationAction> actions)
    {
        foreach (var index in table.Indexes)
        {
            actions.Add(index.Kind switch
            {
                ChangeKind.Add => new CreateIndex(table.Schema, table.Name, index.Definition!),
                ChangeKind.Remove => new DropIndex(table.Schema, table.Name, index.Name),
                _ => new SetIndexComment(table.Schema, table.Name, index.Name, index.Comment!.Old, index.Comment.New),
            });
        }
    }

    private static void EmitGrants(TableDiff table, List<MigrationAction> actions)
    {
        foreach (var grant in table.Grants)
        {
            actions.Add(grant.Kind == ChangeKind.Add
                ? new GrantTablePrivileges(table.Schema, table.Name, grant.Role, grant.Privileges!.Value)
                : new RevokeTablePrivileges(table.Schema, table.Name, grant.Role, grant.Privileges!.Value));
        }
    }
}
