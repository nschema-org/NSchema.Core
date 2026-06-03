using NSchema.Migration.Diff;
using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Migration;

/// <summary>
/// Default <see cref="IMigrationDiffBuilder"/>. Groups a plan's actions by schema and table and folds
/// them into the hierarchical <see cref="MigrationDiff"/> model.
/// </summary>
internal sealed class DefaultMigrationDiffBuilder : IMigrationDiffBuilder
{
    public MigrationDiff Build(MigrationPlan plan)
    {
        var scripts = plan.Actions.OfType<RunScript>().ToList();

        var schemaChanges = new Dictionary<string, SchemaChange>(StringComparer.Ordinal);
        var tablesBySchema = new Dictionary<string, List<TableDiff>>(StringComparer.Ordinal);
        var schemaNames = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var group in plan.Actions.Where(a => a is not RunScript).GroupBy(GroupKeyFor))
        {
            var key = group.Key;
            schemaNames.Add(key.SchemaName);

            if (key.TableName is null)
            {
                schemaChanges[key.SchemaName] = BuildSchemaChange(group.ToList());
            }
            else
            {
                if (!tablesBySchema.TryGetValue(key.SchemaName, out var tables))
                {
                    tablesBySchema[key.SchemaName] = tables = [];
                }

                tables.Add(BuildTableDiff(key.SchemaName, key.TableName, group.ToList()));
            }
        }

        var schemas = schemaNames.Select(name =>
        {
            schemaChanges.TryGetValue(name, out var change);
            var tables = tablesBySchema.TryGetValue(name, out var t)
                ? t.OrderBy(x => x.Name, StringComparer.Ordinal).ToList()
                : [];

            return new SchemaDiff(
                name,
                change?.Kind,
                change?.RenamedFrom,
                change?.Comment,
                change?.Grants ?? [],
                tables);
        }).ToList();

        return new MigrationDiff(
            schemas,
            scripts.Where(s => s.Script.Type == ScriptType.PreDeployment).Select(s => s.Script.Name).ToList(),
            scripts.Where(s => s.Script.Type == ScriptType.PostDeployment).Select(s => s.Script.Name).ToList());
    }

    private static SchemaChange BuildSchemaChange(IReadOnlyList<MigrationAction> actions)
    {
        var kind = actions.Any(a => a is CreateSchema) ? ChangeKind.Add
            : actions.Any(a => a is DropSchema) ? ChangeKind.Remove
            : ChangeKind.Modify;

        var renamedFrom = actions.OfType<RenameSchema>().FirstOrDefault()?.OldName;
        var comment = actions.OfType<SetSchemaComment>().FirstOrDefault() is { } c
            ? new ValueChange<string>(c.OldComment, c.NewComment)
            : null;

        var grants = new List<GrantChange>();
        foreach (var action in actions)
        {
            switch (action)
            {
                case GrantSchemaUsage g:
                    grants.Add(new GrantChange(ChangeKind.Add, g.Role, null));
                    break;
                case RevokeSchemaUsage r:
                    grants.Add(new GrantChange(ChangeKind.Remove, r.Role, null));
                    break;
            }
        }

        return new SchemaChange(kind, renamedFrom, comment, grants);
    }

    private static TableDiff BuildTableDiff(string schema, string table, IReadOnlyList<MigrationAction> actions)
    {
        var kind = actions.Any(a => a is CreateTable) ? ChangeKind.Add
            : actions.Any(a => a is DropTable) ? ChangeKind.Remove
            : ChangeKind.Modify;

        var renamedFrom = actions.OfType<RenameTable>().FirstOrDefault()?.OldName;
        var comment = actions.OfType<SetTableComment>().FirstOrDefault() is { } c
            ? new ValueChange<string>(c.OldComment, c.NewComment)
            : null;

        var columns = BuildColumns(kind, actions);

        var grants = new List<GrantChange>();
        var indexes = new List<IndexDiff>();
        var constraints = new List<ConstraintDiff>();
        foreach (var action in actions)
        {
            switch (action)
            {
                case GrantTablePrivileges g:
                    grants.Add(new GrantChange(ChangeKind.Add, g.Role, g.Privileges));
                    break;
                case RevokeTablePrivileges r:
                    grants.Add(new GrantChange(ChangeKind.Remove, r.Role, r.Privileges));
                    break;

                case CreateIndex ci:
                    indexes.Add(new IndexDiff(ChangeKind.Add, ci.Index.Name, ci.Index, null));
                    break;
                case DropIndex di:
                    indexes.Add(new IndexDiff(ChangeKind.Remove, di.IndexName, null, null));
                    break;
                case SetIndexComment si:
                    indexes.Add(new IndexDiff(ChangeKind.Modify, si.IndexName, null, new ValueChange<string>(si.OldComment, si.NewComment)));
                    break;

                case AddPrimaryKey pk:
                    constraints.Add(new ConstraintDiff(ChangeKind.Add, ConstraintType.PrimaryKey, pk.PrimaryKey.Name, pk.PrimaryKey, null));
                    break;
                case DropPrimaryKey pk:
                    constraints.Add(new ConstraintDiff(ChangeKind.Remove, ConstraintType.PrimaryKey, pk.PrimaryKeyName, null, null));
                    break;
                case AddForeignKey fk:
                    constraints.Add(new ConstraintDiff(ChangeKind.Add, ConstraintType.ForeignKey, fk.ForeignKey.Name, null, fk.ForeignKey));
                    break;
                case DropForeignKey fk:
                    constraints.Add(new ConstraintDiff(ChangeKind.Remove, ConstraintType.ForeignKey, fk.ForeignKeyName, null, null));
                    break;
            }
        }

        return new TableDiff(schema, table, kind, renamedFrom, comment, columns, grants, indexes, constraints);
    }

    private static IReadOnlyList<ColumnDiff> BuildColumns(ChangeKind tableKind, IReadOnlyList<MigrationAction> actions)
    {
        var comments = actions.OfType<SetColumnComment>().ToDictionary(c => c.ColumnName, StringComparer.Ordinal);

        // A newly created table lists its columns from the table definition; their comments (which arrive as
        // separate actions) are folded into the column rather than rendered on their own.
        if (tableKind == ChangeKind.Add && actions.OfType<CreateTable>().FirstOrDefault() is { } create)
        {
            return create.Table.Columns
                .Select(col => new ColumnDiff(
                    col.Name,
                    ChangeKind.Add,
                    col,
                    RenamedFrom: null,
                    Type: null,
                    Nullability: null,
                    Default: null,
                    Identity: null,
                    Comment: comments.TryGetValue(col.Name, out var cc) ? new ValueChange<string>(cc.OldComment, cc.NewComment) : null))
                .ToList();
        }

        // An existing table: collapse every action targeting a given column into a single ColumnDiff, in the
        // order each column is first seen.
        var order = new List<string>();
        var byColumn = new Dictionary<string, List<MigrationAction>>(StringComparer.Ordinal);
        foreach (var action in actions)
        {
            if (ColumnNameOf(action) is not { } name)
            {
                continue;
            }

            if (!byColumn.TryGetValue(name, out var list))
            {
                byColumn[name] = list = [];
                order.Add(name);
            }

            list.Add(action);
        }

        return order.Select(name => BuildColumn(name, byColumn[name])).ToList();
    }

    private static ColumnDiff BuildColumn(string name, IReadOnlyList<MigrationAction> actions)
    {
        ChangeKind kind;
        Column? definition = null;
        if (actions.OfType<AddColumn>().FirstOrDefault() is { } add)
        {
            (kind, definition) = (ChangeKind.Add, add.Column);
        }
        else if (actions.OfType<DropColumn>().FirstOrDefault() is { } drop)
        {
            (kind, definition) = (ChangeKind.Remove, drop.Column);
        }
        else
        {
            kind = ChangeKind.Modify;
        }

        var renamedFrom = actions.OfType<RenameColumn>().FirstOrDefault()?.OldName;
        var type = actions.OfType<AlterColumnType>().FirstOrDefault() is { } t ? new ValueChange<SqlType>(t.OldType, t.NewType) : null;
        var nullability = actions.OfType<AlterColumnNullability>().FirstOrDefault() is { } n ? new ValueChange<bool>(n.OldNullable, n.NewNullable) : null;
        var @default = actions.OfType<SetColumnDefault>().FirstOrDefault() is { } d ? new ValueChange<string>(d.OldDefault, d.NewDefault) : null;
        var identity = actions.OfType<AlterIdentitySequence>().FirstOrDefault() is { } i ? new ValueChange<IdentityOptions>(i.OldOptions, i.NewOptions) : null;
        var comment = actions.OfType<SetColumnComment>().FirstOrDefault() is { } cc ? new ValueChange<string>(cc.OldComment, cc.NewComment) : null;

        return new ColumnDiff(name, kind, definition, renamedFrom, type, nullability, @default, identity, comment);
    }

    private static string? ColumnNameOf(MigrationAction action) => action switch
    {
        AddColumn a => a.Column.Name,
        DropColumn a => a.Column.Name,
        RenameColumn a => a.NewName,
        AlterColumnType a => a.ColumnName,
        AlterColumnNullability a => a.ColumnName,
        AlterIdentitySequence a => a.ColumnName,
        SetColumnDefault a => a.ColumnName,
        SetColumnComment a => a.ColumnName,
        _ => null,
    };

    private static GroupKey GroupKeyFor(MigrationAction action) => action switch
    {
        CreateSchema a => new GroupKey(a.SchemaName, null),
        DropSchema a => new GroupKey(a.SchemaName, null),
        RenameSchema a => new GroupKey(a.NewName, null),
        SetSchemaComment a => new GroupKey(a.SchemaName, null),
        GrantSchemaUsage a => new GroupKey(a.SchemaName, null),
        RevokeSchemaUsage a => new GroupKey(a.SchemaName, null),

        CreateTable a => new GroupKey(a.SchemaName, a.Table.Name),
        DropTable a => new GroupKey(a.SchemaName, a.TableName),
        RenameTable a => new GroupKey(a.SchemaName, a.NewName),
        SetTableComment a => new GroupKey(a.SchemaName, a.TableName),
        GrantTablePrivileges a => new GroupKey(a.SchemaName, a.TableName),
        RevokeTablePrivileges a => new GroupKey(a.SchemaName, a.TableName),

        AddColumn a => new GroupKey(a.SchemaName, a.TableName),
        DropColumn a => new GroupKey(a.SchemaName, a.TableName),
        RenameColumn a => new GroupKey(a.SchemaName, a.TableName),
        AlterColumnType a => new GroupKey(a.SchemaName, a.TableName),
        AlterColumnNullability a => new GroupKey(a.SchemaName, a.TableName),
        AlterIdentitySequence a => new GroupKey(a.SchemaName, a.TableName),
        SetColumnDefault a => new GroupKey(a.SchemaName, a.TableName),
        SetColumnComment a => new GroupKey(a.SchemaName, a.TableName),

        CreateIndex a => new GroupKey(a.SchemaName, a.TableName),
        DropIndex a => new GroupKey(a.SchemaName, a.TableName),
        SetIndexComment a => new GroupKey(a.SchemaName, a.TableName),

        AddPrimaryKey a => new GroupKey(a.SchemaName, a.TableName),
        DropPrimaryKey a => new GroupKey(a.SchemaName, a.TableName),
        AddForeignKey a => new GroupKey(a.SchemaName, a.TableName),
        DropForeignKey a => new GroupKey(a.SchemaName, a.TableName),

        _ => throw new NotSupportedException($"Diff builder cannot group action of type {action.GetType().Name}."),
    };

    private readonly record struct GroupKey(string SchemaName, string? TableName);

    private sealed record SchemaChange(
        ChangeKind Kind,
        string? RenamedFrom,
        ValueChange<string>? Comment,
        IReadOnlyList<GrantChange> Grants);
}
