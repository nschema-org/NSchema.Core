using System.Collections.Frozen;
using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Columns;
using NSchema.Plan.Model.Constraints;
using NSchema.Plan.Model.Enums;
using NSchema.Plan.Model.Extensions;
using NSchema.Plan.Model.Functions;
using NSchema.Plan.Model.Indexes;
using NSchema.Plan.Model.Procedures;
using NSchema.Plan.Model.Schemas;
using NSchema.Plan.Model.Sequence;
using NSchema.Plan.Model.Tables;
using NSchema.Plan.Model.Triggers;
using NSchema.Plan.Model.Views;

namespace NSchema.Plan;

/// <summary>
/// Walks the structured diff and produces a migration plan.
/// </summary>
internal sealed class PlanLinearizer : IPlanLinearizer
{
    private static readonly IReadOnlyDictionary<Type, int> _actionPriorities = new List<Type> {
        // Views are dropped before anything they read (columns, tables) is dropped.
        typeof(DropView),
        // Triggers are dropped before their table and before the function they call (functions drop last).
        typeof(DropTrigger),
        typeof(DropForeignKey),
        typeof(DropCheckConstraint),
        typeof(DropUniqueConstraint),
        typeof(DropIndex),
        typeof(DropPrimaryKey),
        typeof(RevokeSchemaUsage),
        typeof(RevokeTablePrivileges),
        // Extensions are database-global infrastructure: they are created (and version-updated) before any schema
        // or object that might depend on a type, function or operator the extension provides.
        typeof(CreateExtension),
        typeof(AlterExtension),
        typeof(RenameSchema),
        typeof(CreateSchema),
        // Enums and sequences are created (and renamed, and gain values) before any table change can reference
        // them: a column may use the enum type, and a default may call the sequence.
        typeof(RenameEnum),
        typeof(RenameSequence),
        typeof(CreateEnum),
        typeof(CreateSequence),
        typeof(AddEnumValue),
        typeof(AlterSequence),
        // Routines are created/recreated/renamed before any table change because column DEFAULTs and CHECK
        // constraints may call them, and after enums/sequences because their args and bodies may use those.
        // Renames precede creates/recreates so a recreate targets the final name.
        typeof(RenameFunction),
        typeof(RenameProcedure),
        typeof(CreateFunction),
        typeof(CreateProcedure),
        typeof(RecreateFunction),
        typeof(RecreateProcedure),
        typeof(RenameTable),
        typeof(RenameView),
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
        // Triggers are created once their table exists; the function they call was already created before any
        // table (functions precede tables above).
        typeof(CreateTrigger),
        // Views are created after every table, constraint and index they might read exists.
        typeof(CreateView),
        typeof(GrantSchemaUsage),
        typeof(GrantTablePrivileges),
        typeof(SetSchemaComment),
        typeof(SetTableComment),
        typeof(SetColumnComment),
        typeof(SetIndexComment),
        typeof(SetTriggerComment),
        typeof(SetConstraintComment),
        typeof(SetViewComment),
        typeof(SetEnumComment),
        typeof(SetSequenceComment),
        typeof(SetFunctionComment),
        typeof(SetProcedureComment),
        typeof(SetExtensionComment),
        typeof(DropTable),
        // Routines are dropped after the tables whose defaults/checks called them, and before the enums their
        // signatures may use; enums and sequences then drop after everything that referenced them.
        typeof(DropFunction),
        typeof(DropProcedure),
        typeof(DropEnum),
        typeof(DropSequence),
        typeof(DropSchema),
        // Extensions drop last — after every schema object that might depend on them is gone, so the drop can't
        // fail on a lingering dependency.
        typeof(DropExtension),
    }.Index().ToFrozenDictionary(x => x.Item, x => x.Index);

    public IReadOnlyList<MigrationAction> Linearize(DatabaseDiff diff)
    {
        var actions = new List<MigrationAction>();
        EmitExtensions(diff, actions);
        foreach (var schema in diff.Schemas)
        {
            EmitSchema(schema, actions);
        }

        // Views are emitted in one cross-schema pass: their create/drop order is governed by a dependency sort
        // (a view after what it reads, dropped before), which the per-schema walk above cannot express.
        EmitViews(diff, actions);

        actions = actions.OrderBy(action => _actionPriorities[action.GetType()]).ToList();
        return actions;
    }

    /// <summary>
    /// Emits the view actions across every schema. <see cref="CreateView"/>s are appended in dependency order and
    /// <see cref="DropView"/>s in the reverse, so that once the stable type sort above gathers each kind into its
    /// band, a view is created after the views it reads and dropped before them.
    /// </summary>
    private static void EmitViews(DatabaseDiff diff, List<MigrationAction> actions)
    {
        var creates = new List<ViewDiff>();
        var drops = new List<ViewDiff>();

        foreach (var schema in diff.Schemas)
        {
            foreach (var view in schema.Views)
            {
                if (view.RenamedFrom is not null)
                {
                    actions.Add(new RenameView(view.Schema, view.RenamedFrom, view.Name));
                }

                if (view.Kind == ChangeKind.Remove)
                {
                    drops.Add(view);
                }
                else if (view.Definition is not null)
                {
                    // An Add, or a body change applied as a replace.
                    creates.Add(view);
                }

                if (view.Comment is not null)
                {
                    actions.Add(new SetViewComment(view.Schema, view.Name, view.Comment.Old, view.Comment.New));
                }
            }
        }

        foreach (var view in OrderByDependency(creates))
        {
            actions.Add(new CreateView(view.Schema, view.Definition!));
        }

        // Dropped views go out dependents-first: the reverse of the create order.
        foreach (var view in OrderByDependency(drops).Reverse())
        {
            actions.Add(new DropView(view.Schema, view.Name));
        }
    }

    private static IReadOnlyList<ViewDiff> OrderByDependency(IReadOnlyList<ViewDiff> views) =>
        TopologicalSort.Order(
            views,
            ViewKey,
            view => view.DependsOn.Select(d => Key(d.Schema, d.Name)),
            StringComparer.OrdinalIgnoreCase,
            view => $"view {view.Schema}.{view.Name}");

    private static string ViewKey(ViewDiff view) => Key(view.Schema, view.Name);

    private static string Key(string schema, string name) => $"{schema}.{name}";

    /// <summary>
    /// Emits the root-level extension actions. Ordering (extensions created/updated before schemas, dropped after
    /// everything) is governed by the priority table above; this just maps each <see cref="ExtensionDiff"/> to its
    /// action(s).
    /// </summary>
    private static void EmitExtensions(DatabaseDiff diff, List<MigrationAction> actions)
    {
        foreach (var extension in diff.Extensions)
        {
            switch (extension.Kind)
            {
                case ChangeKind.Add:
                    actions.Add(new CreateExtension(extension.Definition!));
                    break;

                case ChangeKind.Remove:
                    actions.Add(new DropExtension(extension.Name));
                    break;

                default: // Modify
                    if (extension.Version is not null)
                    {
                        actions.Add(new AlterExtension(extension.Name, extension.Version.Old, extension.Version.New));
                    }
                    break;
            }

            if (extension.Kind != ChangeKind.Remove && extension.Comment is not null)
            {
                actions.Add(new SetExtensionComment(extension.Name, extension.Comment.Old, extension.Comment.New));
            }
        }
    }

    private static void EmitSchema(SchemaDiff schema, List<MigrationAction> actions)
    {
        switch (schema.Kind)
        {
            case ChangeKind.Add:
                actions.Add(new CreateSchema(schema.Name));
                EmitSchemaAttributes(schema, actions);
                EmitEnums(schema, actions);
                EmitSequences(schema, actions);
                EmitFunctions(schema, actions);
                EmitProcedures(schema, actions);
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
                EmitEnums(schema, actions);
                EmitSequences(schema, actions);
                EmitFunctions(schema, actions);
                EmitProcedures(schema, actions);
                foreach (var table in schema.Tables)
                {
                    EmitTable(table, actions);
                }
                break;
        }
    }

    private static void EmitFunctions(SchemaDiff schema, List<MigrationAction> actions)
    {
        foreach (var function in schema.Functions)
        {
            switch (function.Kind)
            {
                case ChangeKind.Add:
                    actions.Add(new CreateFunction(function.Schema, function.Definition!));
                    break;

                case ChangeKind.Remove:
                    actions.Add(new DropFunction(function.Schema, function.Name));
                    break;

                default: // Modify
                    if (function.RenamedFrom is not null)
                    {
                        actions.Add(new RenameFunction(function.Schema, function.RenamedFrom, function.Name));
                    }
                    // A signature change recreates (a replace under different arguments would create a separate
                    // overload); a definition-only change replaces in place.
                    if (function.RequiresRecreate)
                    {
                        actions.Add(new RecreateFunction(function.Schema, function.Definition!));
                    }
                    else if (function.Definition is not null)
                    {
                        actions.Add(new CreateFunction(function.Schema, function.Definition));
                    }
                    break;
            }

            if (function.Kind != ChangeKind.Remove && function.Comment is not null)
            {
                actions.Add(new SetFunctionComment(function.Schema, function.Name, function.Comment.Old, function.Comment.New));
            }
        }
    }

    private static void EmitProcedures(SchemaDiff schema, List<MigrationAction> actions)
    {
        foreach (var procedure in schema.Procedures)
        {
            switch (procedure.Kind)
            {
                case ChangeKind.Add:
                    actions.Add(new CreateProcedure(procedure.Schema, procedure.Definition!));
                    break;

                case ChangeKind.Remove:
                    actions.Add(new DropProcedure(procedure.Schema, procedure.Name));
                    break;

                default: // Modify
                    if (procedure.RenamedFrom is not null)
                    {
                        actions.Add(new RenameProcedure(procedure.Schema, procedure.RenamedFrom, procedure.Name));
                    }
                    if (procedure.RequiresRecreate)
                    {
                        actions.Add(new RecreateProcedure(procedure.Schema, procedure.Definition!));
                    }
                    else if (procedure.Definition is not null)
                    {
                        actions.Add(new CreateProcedure(procedure.Schema, procedure.Definition));
                    }
                    break;
            }

            if (procedure.Kind != ChangeKind.Remove && procedure.Comment is not null)
            {
                actions.Add(new SetProcedureComment(procedure.Schema, procedure.Name, procedure.Comment.Old, procedure.Comment.New));
            }
        }
    }

    private static void EmitEnums(SchemaDiff schema, List<MigrationAction> actions)
    {
        foreach (var enumDiff in schema.Enums)
        {
            switch (enumDiff.Kind)
            {
                case ChangeKind.Add:
                    actions.Add(new CreateEnum(enumDiff.Schema, enumDiff.Definition!));
                    break;

                case ChangeKind.Remove:
                    actions.Add(new DropEnum(enumDiff.Schema, enumDiff.Name));
                    break;

                default: // Modify
                    if (enumDiff.RenamedFrom is not null)
                    {
                        actions.Add(new RenameEnum(enumDiff.Schema, enumDiff.RenamedFrom, enumDiff.Name));
                    }
                    // Additions are emitted in list order so each anchor exists when its addition runs (the
                    // stable priority sort preserves this). A removal/reorder has no AddedValues — it cannot be
                    // planned, and the always-on EnumValueRemovalDiffPolicy fails the run before execution.
                    foreach (var addition in enumDiff.AddedValues)
                    {
                        actions.Add(new AddEnumValue(enumDiff.Schema, enumDiff.Name, addition.Value, addition.Before, addition.After));
                    }
                    break;
            }

            if (enumDiff.Kind != ChangeKind.Remove && enumDiff.Comment is not null)
            {
                actions.Add(new SetEnumComment(enumDiff.Schema, enumDiff.Name, enumDiff.Comment.Old, enumDiff.Comment.New));
            }
        }
    }

    private static void EmitSequences(SchemaDiff schema, List<MigrationAction> actions)
    {
        foreach (var sequence in schema.Sequences)
        {
            switch (sequence.Kind)
            {
                case ChangeKind.Add:
                    actions.Add(new CreateSequence(sequence.Schema, sequence.Definition!));
                    break;

                case ChangeKind.Remove:
                    actions.Add(new DropSequence(sequence.Schema, sequence.Name));
                    break;

                default: // Modify
                    if (sequence.RenamedFrom is not null)
                    {
                        actions.Add(new RenameSequence(sequence.Schema, sequence.RenamedFrom, sequence.Name));
                    }
                    if (sequence.Options is not null)
                    {
                        actions.Add(new AlterSequence(sequence.Schema, sequence.Name, sequence.Options.Old!, sequence.Options.New!));
                    }
                    break;
            }

            if (sequence.Kind != ChangeKind.Remove && sequence.Comment is not null)
            {
                actions.Add(new SetSequenceComment(sequence.Schema, sequence.Name, sequence.Comment.Old, sequence.Comment.New));
            }
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
                EmitTriggers(table, actions);
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
                EmitTriggers(table, actions);
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

    private static void EmitTriggers(TableDiff table, List<MigrationAction> actions)
    {
        foreach (var trigger in table.Triggers)
        {
            actions.Add(trigger.Kind switch
            {
                ChangeKind.Add => new CreateTrigger(table.Schema, table.Name, trigger.Definition!),
                ChangeKind.Remove => new DropTrigger(table.Schema, table.Name, trigger.Name),
                _ => new SetTriggerComment(table.Schema, table.Name, trigger.Name, trigger.Comment!.Old, trigger.Comment.New),
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
