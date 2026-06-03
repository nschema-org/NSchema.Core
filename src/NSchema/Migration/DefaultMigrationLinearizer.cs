using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Schema.Model;

namespace NSchema.Migration;

/// <summary>
/// Default <see cref="IMigrationLinearizer"/>. Walks the structured diff, emits the migration actions that realize
/// each change, and orders the result into a safe dependency order via <see cref="ActionOrderingTransformer"/>.
/// </summary>
internal sealed class DefaultMigrationLinearizer : IMigrationLinearizer
{
    private static readonly ActionOrderingTransformer Orderer = new();

    public MigrationPlan Linearize(MigrationDiff diff, DatabaseSchema desiredSchema)
    {
        var actions = new List<MigrationAction>();
        foreach (var schema in diff.Schemas)
        {
            EmitSchema(schema, actions);
        }

        return Orderer.Transform(new MigrationPlan(actions, desiredSchema));
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

            default: // Modify
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
        }
    }

    private static void EmitConstraints(TableDiff table, List<MigrationAction> actions)
    {
        foreach (var constraint in table.Constraints)
        {
            actions.Add((constraint.Kind, constraint.Type) switch
            {
                (ChangeKind.Add, ConstraintType.PrimaryKey) => new AddPrimaryKey(table.Schema, table.Name, constraint.PrimaryKey!),
                (ChangeKind.Remove, ConstraintType.PrimaryKey) => new DropPrimaryKey(table.Schema, table.Name, constraint.Name),
                (ChangeKind.Add, ConstraintType.ForeignKey) => new AddForeignKey(table.Schema, table.Name, constraint.ForeignKey!),
                (ChangeKind.Remove, ConstraintType.ForeignKey) => new DropForeignKey(table.Schema, table.Name, constraint.Name),
                _ => throw new NotSupportedException($"Cannot linearize constraint change {constraint.Kind} {constraint.Type}."),
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
