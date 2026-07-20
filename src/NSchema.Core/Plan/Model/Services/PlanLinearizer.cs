using System.Collections.Frozen;
using NSchema.Diff.Model;
using NSchema.Diff.Model.Columns;
using NSchema.Diff.Model.Extensions;
using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Tables;
using NSchema.Diff.Model.Views;
using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.Plan.Model.Columns;
using NSchema.Plan.Model.CompositeTypes;
using NSchema.Plan.Model.Constraints;
using NSchema.Plan.Model.Domains;
using NSchema.Plan.Model.Enums;
using NSchema.Plan.Model.Extensions;
using NSchema.Plan.Model.Indexes;
using NSchema.Plan.Model.Routines;
using NSchema.Plan.Model.Schemas;
using NSchema.Plan.Model.Scripts;
using NSchema.Plan.Model.Sequences;
using NSchema.Plan.Model.Tables;
using NSchema.Plan.Model.Triggers;
using NSchema.Plan.Model.Views;

namespace NSchema.Plan.Model.Services;

/// <summary>
/// Walks the structured diff and produces a migration plan.
/// </summary>
internal sealed class PlanLinearizer : IPlanLinearizer
{
    private static readonly IReadOnlyDictionary<Type, int> _actionPriorities = new List<Type> {
        // The schema rename runs before everything else: every child diff node carries the new schema name, so
        // once the schema is renamed every later action.
        typeof(RenameSchema),
        // Views are dropped before anything they read (columns, tables) is dropped.
        typeof(DropView),
        // Triggers are dropped before their table and before the function they call (functions drop last).
        typeof(DropTrigger),
        typeof(DropForeignKey),
        typeof(DropCheckConstraint),
        typeof(DropExclusionConstraint),
        typeof(DropUniqueConstraint),
        typeof(DropIndex),
        typeof(DropPrimaryKey),
        typeof(RevokeSchemaUsage),
        typeof(RevokeTablePrivileges),
        // Extensions are database-global infrastructure: they are created (and version-updated) before any schema
        // or object that might depend on a type, function or operator the extension provides.
        typeof(CreateExtension),
        typeof(AlterExtension),
        typeof(CreateSchema),
        // Enums and sequences are created (and renamed, and gain values) before any table change can reference
        // them: a column may use the enum type, and a default may call the sequence.
        typeof(RenameEnum),
        typeof(RenameSequence),
        typeof(CreateEnum),
        typeof(CreateSequence),
        typeof(AddEnumValue),
        typeof(AlterSequence),
        // Domains are types used by columns, so they are created (and renamed/altered) before any table change.
        // A base-type change recreates; default/not-null/check changes apply in place. Renames precede so a
        // recreate targets the final name.
        typeof(RenameDomain),
        typeof(CreateDomain),
        typeof(RecreateDomain),
        typeof(AlterDomainDefault),
        typeof(AlterDomainNotNull),
        typeof(AddDomainCheck),
        typeof(DropDomainCheck),
        // Composite types are types used by columns too, so they are created (and renamed/altered) before any
        // table change. Every change applies in place (ALTER TYPE) — there is no recreate — and renames precede
        // field changes so an add/retype targets the final name.
        typeof(RenameCompositeType),
        typeof(CreateCompositeType),
        typeof(AddCompositeField),
        typeof(AlterCompositeFieldType),
        typeof(DropCompositeField),
        // Routines are created/recreated/renamed before any table change because column DEFAULTs and CHECK
        // constraints may call them, and after enums/sequences because their args and bodies may use those.
        // Renames precede creates/recreates so a recreate targets the final name.
        typeof(RenameRoutine),
        typeof(CreateRoutine),
        typeof(RecreateRoutine),
        typeof(RenameTable),
        typeof(RenameView),
        typeof(CreateTable),
        typeof(DropColumn),
        typeof(RenameColumn),
        typeof(AddColumn),
        // Data migrations run after column adds (a backfill needs its column) and before type alters,
        // nullability alters, and constraint adds (their SQL prepares the data those changes depend on).
        typeof(ExecuteScript),
        typeof(AlterColumnType),
        typeof(AlterColumnNullability),
        typeof(AlterIdentitySequence),
        typeof(SetColumnDefault),
        typeof(SetColumnGenerated),
        typeof(AddPrimaryKey),
        typeof(AddUniqueConstraint),
        typeof(AddForeignKey),
        typeof(AddCheckConstraint),
        typeof(AddExclusionConstraint),
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
        typeof(SetRoutineComment),
        typeof(SetDomainComment),
        typeof(SetCompositeTypeComment),
        typeof(SetExtensionComment),
        typeof(DropTable),
        // Routines are dropped after the tables whose defaults/checks called them, and before the enums their
        // signatures may use; enums and sequences then drop after everything that referenced them.
        typeof(DropRoutine),
        // Domains are dropped after the tables whose columns used them, alongside enums and sequences.
        typeof(DropDomain),
        // Composite types drop after the tables whose columns used them, alongside domains/enums/sequences.
        typeof(DropCompositeType),
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

        // Deployment scripts bookend the plan: pre scripts run before everything, post scripts after.
        return [.. ScriptActions(diff, DeploymentPhase.Pre), .. actions, .. ScriptActions(diff, DeploymentPhase.Post)];
    }

    private static IEnumerable<ExecuteScript> ScriptActions(DatabaseDiff diff, DeploymentPhase phase) =>
        diff.DeploymentScripts.Where(s => s.Phase == phase).Select(s => new ExecuteScript(s));

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
                // A rename that accompanies a recreate is subsumed by it: the drop removes the old name and the
                // definition recreates the view under the new one, so no RenameView is emitted.
                if (view.RenamedFrom is not null && !view.RequiresRecreate)
                {
                    actions.Add(new RenameView(new(view.Schema, view.RenamedFrom), view.Name, view.IsMaterialized));
                }

                if (view.Kind == ChangeKind.Remove)
                {
                    drops.Add(view);
                }
                else if (view.RequiresRecreate)
                {
                    // A materialized view's body change (or a view <-> materialized-view conversion) can't be
                    // replaced in place, so it is both dropped and recreated; its indexes rebuild with it.
                    drops.Add(view);
                    creates.Add(view);
                }
                else if (view.Definition is not null)
                {
                    // A plain view's body change, applied as CREATE OR REPLACE.
                    creates.Add(view);
                }

                if (view.Kind != ChangeKind.Remove && view.Comment is not null)
                {
                    actions.Add(new SetViewComment(new(view.Schema, view.Name), view.Comment.Old, view.Comment.New, view.IsMaterialized));
                }

                // In-place index changes on a materialized view whose body is unchanged; on a create/recreate the
                // indexes ride along on the definition instead. Index drops sort before RenameView, so on a
                // renamed view they run while it still carries its old name.
                foreach (var index in view.Indexes)
                {
                    actions.Add(index.Kind switch
                    {
                        ChangeKind.Add => new CreateIndex(new(view.Schema, view.Name), index.Definition!),
                        ChangeKind.Remove => new DropIndex(new(view.Schema, view.RenamedFrom ?? view.Name, index.Name)),
                        _ => new SetIndexComment(new(view.Schema, view.Name, index.Name), index.Comment!.Old, index.Comment.New),
                    });
                }
            }
        }

        foreach (var view in OrderByDependency(creates))
        {
            actions.Add(new CreateView(view.Schema, view.Definition!));
        }

        // Dropped views go out dependents-first: the reverse of the create order. A renamed view recreating is
        // dropped under its old name (no rename precedes the drop), and a converting view is dropped as what it
        // currently is — IsMaterialized reflects the desired side, so a flip drops with the old materialization.
        foreach (var view in OrderByDependency(drops).Reverse())
        {
            actions.Add(new DropView(new(view.Schema, view.RenamedFrom ?? view.Name), view.Materialized?.Old ?? view.IsMaterialized));
        }
    }

    /// <summary>
    /// The instance-level ordering layered on top of the fixed action-type order: where two views are created
    /// together and one reads the other, the type order can't separate them — a dependency sort must.
    /// </summary>
    private static IReadOnlyList<ViewDiff> OrderByDependency(IReadOnlyList<ViewDiff> views) =>
        views.OrderedByDependencies(
            ViewKey,
            view => view.DependsOn.Select(d => (d.Schema, d.Name)),
            view => $"view {view.Schema}.{view.Name}");

    private static (SqlIdentifier Schema, SqlIdentifier Name) ViewKey(ViewDiff view) => (view.Schema, view.Name);


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
                EmitRoutines(schema, actions);
                EmitDomains(schema, actions);
                EmitCompositeTypes(schema, actions);
                foreach (var table in schema.Tables)
                {
                    EmitTable(table, actions);
                }
                break;

            case ChangeKind.Remove:
                // Drop everything the schema contains before the schema itself, rather than relying on a
                // provider-specific DROP SCHEMA CASCADE. The final type-sort orders these object drops ahead of the
                // DropSchema, and views are emitted by the cross-schema view pass.
                EmitEnums(schema, actions);
                EmitSequences(schema, actions);
                EmitRoutines(schema, actions);
                EmitDomains(schema, actions);
                EmitCompositeTypes(schema, actions);
                foreach (var table in schema.Tables)
                {
                    EmitTable(table, actions);
                }
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
                EmitRoutines(schema, actions);
                EmitDomains(schema, actions);
                EmitCompositeTypes(schema, actions);
                foreach (var table in schema.Tables)
                {
                    EmitTable(table, actions);
                }
                break;
        }
    }

    /// <summary>
    /// Emits one schema-object kind: create on add, drop on remove, and on modify a rename (when one is
    /// recorded) followed by the kind's own modify actions; a comment change trails every non-remove.
    /// </summary>
    private static void EmitObjects<T>(
        IReadOnlyList<T> objects,
        List<MigrationAction> actions,
        Func<T, MigrationAction> create,
        Func<T, MigrationAction> drop,
        Func<T, MigrationAction> rename,
        Func<T, MigrationAction> comment,
        Action<T> modify
    ) where T : ISchemaObjectDiff
    {
        foreach (var diff in objects)
        {
            switch (diff.Kind)
            {
                case ChangeKind.Add:
                    actions.Add(create(diff));
                    break;

                case ChangeKind.Remove:
                    actions.Add(drop(diff));
                    break;

                default: // Modify
                    if (diff.RenamedFrom is not null)
                    {
                        actions.Add(rename(diff));
                    }
                    modify(diff);
                    break;
            }

            if (diff.Kind != ChangeKind.Remove && diff.Comment is not null)
            {
                actions.Add(comment(diff));
            }
        }
    }

    private static void EmitRoutines(SchemaDiff schema, List<MigrationAction> actions) =>
        EmitObjects(schema.Routines, actions,
            r => new CreateRoutine(r.Schema, r.Definition!),
            r => new DropRoutine(new(r.Schema, r.Name), r.RoutineKind),
            r => new RenameRoutine(new(r.Schema, r.RenamedFrom!), r.Name, r.RoutineKind),
            r => new SetRoutineComment(new(r.Schema, r.Name), r.Comment!.Old, r.Comment.New, r.RoutineKind),
            r =>
            {
                // A signature (or kind) change recreates (a replace under different arguments would create a
                // separate overload); a definition-only change replaces in place.
                if (r.RequiresRecreate)
                {
                    actions.Add(new RecreateRoutine(r.Schema, r.Definition!));
                }
                else if (r.Definition is not null)
                {
                    actions.Add(new CreateRoutine(r.Schema, r.Definition));
                }
            });

    private static void EmitDomains(SchemaDiff schema, List<MigrationAction> actions) =>
        EmitObjects(schema.Domains, actions,
            d => new CreateDomain(d.Schema, d.Definition!),
            d => new DropDomain(new(d.Schema, d.Name)),
            d => new RenameDomain(new(d.Schema, d.RenamedFrom!), d.Name),
            d => new SetDomainComment(new(d.Schema, d.Name), d.Comment!.Old, d.Comment.New),
            d =>
            {
                // A base-type change can't be altered in place, so it recreates (default/not-null/checks rebuild
                // with the definition); otherwise each facet is altered in place.
                if (d.RequiresRecreate)
                {
                    actions.Add(new RecreateDomain(d.Schema, d.Definition!));
                    return;
                }

                if (d.Default is not null)
                {
                    actions.Add(new AlterDomainDefault(new(d.Schema, d.Name), d.Default.Old, d.Default.New));
                }
                if (d.NotNull is not null)
                {
                    actions.Add(new AlterDomainNotNull(new(d.Schema, d.Name), d.NotNull.New));
                }
                foreach (var check in d.Checks)
                {
                    actions.Add(check.Kind == ChangeKind.Remove
                        ? new DropDomainCheck(new(d.Schema, d.Name, check.Name))
                        : new AddDomainCheck(new(d.Schema, d.Name), check.Definition!));
                }
            });

    private static void EmitCompositeTypes(SchemaDiff schema, List<MigrationAction> actions) =>
        EmitObjects(schema.CompositeTypes, actions,
            t => new CreateCompositeType(t.Schema, t.Definition!),
            t => new DropCompositeType(new(t.Schema, t.Name)),
            t => new RenameCompositeType(new(t.Schema, t.RenamedFrom!), t.Name),
            t => new SetCompositeTypeComment(new(t.Schema, t.Name), t.Comment!.Old, t.Comment.New),
            t =>
            {
                // Every field change applies in place: a matched field whose type differs is retyped, a missing
                // field is dropped, a new field is added. There is no recreate.
                foreach (var field in t.Fields)
                {
                    actions.Add(field.Kind switch
                    {
                        ChangeKind.Remove => new DropCompositeField(new(t.Schema, t.Name, field.Name)),
                        ChangeKind.Modify => new AlterCompositeFieldType(new(t.Schema, t.Name, field.Name), field.Type!.Old!, field.Type.New!),
                        _ => new AddCompositeField(new(t.Schema, t.Name), field.Definition!),
                    });
                }
            });

    private static void EmitEnums(SchemaDiff schema, List<MigrationAction> actions) =>
        EmitObjects(schema.Enums, actions,
            e => new CreateEnum(e.Schema, e.Definition!),
            e => new DropEnum(new(e.Schema, e.Name)),
            e => new RenameEnum(new(e.Schema, e.RenamedFrom!), e.Name),
            e => new SetEnumComment(new(e.Schema, e.Name), e.Comment!.Old, e.Comment.New),
            e =>
            {
                // Additions are emitted in list order so each anchor exists when its addition runs (the
                // stable priority sort preserves this). A removal/reorder has no AddedValues — it cannot be
                // planned, and the always-on EnumValueRemovalPolicy fails the run before execution.
                foreach (var addition in e.AddedValues)
                {
                    actions.Add(new AddEnumValue(new(e.Schema, e.Name), addition.Value, addition.Before, addition.After));
                }
            });

    private static void EmitSequences(SchemaDiff schema, List<MigrationAction> actions) =>
        EmitObjects(schema.Sequences, actions,
            s => new CreateSequence(s.Schema, s.Definition!),
            s => new DropSequence(new(s.Schema, s.Name)),
            s => new RenameSequence(new(s.Schema, s.RenamedFrom!), s.Name),
            s => new SetSequenceComment(new(s.Schema, s.Name), s.Comment!.Old, s.Comment.New),
            s =>
            {
                if (s.Options is not null)
                {
                    actions.Add(new AlterSequence(new(s.Schema, s.Name), s.Options.Old!, s.Options.New!));
                }
            });

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
                // The columns and every table constraint are created inline by CREATE TABLE (carried on
                // Definition); only indexes, triggers, comments and grants arrive as separate actions.
                actions.Add(new CreateTable(table.Schema, table.Definition!));
                if (table.Comment is not null)
                {
                    actions.Add(new SetTableComment(new(table.Schema, table.Name), table.Comment.Old, table.Comment.New));
                }
                foreach (var column in table.Columns.Where(c => c.Comment is not null))
                {
                    actions.Add(new SetColumnComment(new(table.Schema, table.Name, column.Name), column.Comment!.Old, column.Comment.New));
                }
                EmitConstraints(table, actions);
                EmitIndexes(table, actions);
                EmitTriggers(table, actions);
                EmitGrants(table, actions);
                break;

            case ChangeKind.Remove:
                actions.Add(new DropTable(new(table.Schema, table.Name)));
                break;

            default: // Modify
                if (table.RenamedFrom is not null)
                {
                    actions.Add(new RenameTable(new(table.Schema, table.RenamedFrom), table.Name));
                }
                if (table.Comment is not null)
                {
                    actions.Add(new SetTableComment(new(table.Schema, table.Name), table.Comment.Old, table.Comment.New));
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
                // A required column with a matched backfill migration is decomposed: added nullable, backfilled
                // by the migration SQL, then tightened to NOT NULL. Identity and generated columns fill
                // themselves and a default covers existing rows, so those adds keep their declared shape.
                if (column is { MigrationScript: { } backfill, Definition: { IsNullable: false, DefaultExpression: null, IsIdentity: false, GeneratedExpression: null } })
                {
                    // The declared column belongs to the project tree, so the nullable variant is a copy.
                    var nullable = column.Definition.Clone();
                    nullable.IsNullable = true;
                    actions.Add(new AddColumn(new(table.Schema, table.Name), nullable));
                    actions.Add(new ExecuteScript(backfill));
                    actions.Add(new AlterColumnNullability(new(table.Schema, table.Name, column.Name), OldNullable: true, NewNullable: false, column.Definition.Type));
                }
                else
                {
                    actions.Add(new AddColumn(new(table.Schema, table.Name), column.Definition!));
                    if (column.MigrationScript is { } migration)
                    {
                        actions.Add(new ExecuteScript(migration));
                    }
                }
                if (column.Comment is not null)
                {
                    actions.Add(new SetColumnComment(new(table.Schema, table.Name, column.Name), column.Comment.Old, column.Comment.New));
                }
                break;

            case ChangeKind.Remove:
                actions.Add(new DropColumn(new(table.Schema, table.Name), column.Definition!));
                break;

            case ChangeKind.Modify:
                if (column.RenamedFrom is not null)
                {
                    actions.Add(new RenameColumn(new(table.Schema, table.Name, column.RenamedFrom), column.Name));
                }
                if (column.Type is not null)
                {
                    // A matched migration prepares the data for the cast; the priority table runs it first.
                    if (column.MigrationScript is { } prep)
                    {
                        actions.Add(new ExecuteScript(prep));
                    }
                    actions.Add(new AlterColumnType(new(table.Schema, table.Name, column.Name), column.Type.Old!, column.Type.New!, column.Definition?.IsNullable));
                }
                if (column.Nullability is not null)
                {
                    actions.Add(new AlterColumnNullability(new(table.Schema, table.Name, column.Name), column.Nullability.Old, column.Nullability.New, column.Definition?.Type));
                }
                if (column.Default is not null)
                {
                    actions.Add(new SetColumnDefault(new(table.Schema, table.Name, column.Name), column.Default.Old, column.Default.New));
                }
                if (column.Generated is not null)
                {
                    actions.Add(new SetColumnGenerated(new(table.Schema, table.Name, column.Name), column.Generated.Old, column.Generated.New));
                }
                if (column.Identity is not null)
                {
                    actions.Add(new AlterIdentitySequence(new(table.Schema, table.Name, column.Name), column.Identity.Old, column.Identity.New));
                }
                if (column.Comment is not null)
                {
                    actions.Add(new SetColumnComment(new(table.Schema, table.Name, column.Name), column.Comment.Old, column.Comment.New));
                }
                break;
            default: throw new NotSupportedException($"Cannot linearize column change {column.Kind}.");
        }
    }

    // Drops and revokes are sorted before RenameTable, so on a renamed table they run while it still carries
    // its old name; every action from the rename onward targets the new name.
    private static void EmitConstraints(TableDiff table, List<MigrationAction> actions)
    {
        var preRenameName = table.RenamedFrom ?? table.Name;

        // A newly-created table carries its constraints inline on CreateTable's definition, so their adds fold
        // into the CREATE TABLE and only comment changes still arrive as separate actions.
        var foldAdds = table.Kind == ChangeKind.Add;

        EmitConstraintKind(table.PrimaryKey, actions, foldAdds,
            pk => new AddPrimaryKey(new(table.Schema, table.Name), pk.Definition!),
            pk => new DropPrimaryKey(new(table.Schema, preRenameName, pk.Name)),
            pk => new SetConstraintComment(new(table.Schema, table.Name, pk.Name), pk.Comment!.Old, pk.Comment.New));

        EmitConstraintKind(table.ForeignKeys, actions, foldAdds,
            fk => new AddForeignKey(new(table.Schema, table.Name), fk.Definition!),
            fk => new DropForeignKey(new(table.Schema, preRenameName, fk.Name)),
            fk => new SetConstraintComment(new(table.Schema, table.Name, fk.Name), fk.Comment!.Old, fk.Comment.New));

        EmitConstraintKind(table.UniqueConstraints, actions, foldAdds,
            uq => new AddUniqueConstraint(new(table.Schema, table.Name), uq.Definition!),
            uq => new DropUniqueConstraint(new(table.Schema, preRenameName, uq.Name)),
            uq => new SetConstraintComment(new(table.Schema, table.Name, uq.Name), uq.Comment!.Old, uq.Comment.New));

        EmitConstraintKind(table.Checks, actions, foldAdds,
            ck => new AddCheckConstraint(new(table.Schema, table.Name), ck.Definition!),
            ck => new DropCheckConstraint(new(table.Schema, preRenameName, ck.Name)),
            ck => new SetConstraintComment(new(table.Schema, table.Name, ck.Name), ck.Comment!.Old, ck.Comment.New));

        EmitConstraintKind(table.ExclusionConstraints, actions, foldAdds,
            ex => new AddExclusionConstraint(new(table.Schema, table.Name), ex.Definition!),
            ex => new DropExclusionConstraint(new(table.Schema, preRenameName, ex.Name)),
            ex => new SetConstraintComment(new(table.Schema, table.Name, ex.Name), ex.Comment!.Old, ex.Comment.New));
    }

    /// <summary>
    /// Emits one constraint kind: an add's matched migration first (it prepares the data the constraint depends
    /// on — de-duplication, backfill — and the priority table runs every data migration before the constraint
    /// adds), then the change itself. A constraint Modify is always a comment-only change. When <paramref
    /// name="foldAdds"/> is set the table is being created, so an add is inlined into the CREATE TABLE and skipped.
    /// </summary>
    private static void EmitConstraintKind<T>(
        IReadOnlyList<T> constraints,
        List<MigrationAction> actions,
        bool foldAdds,
        Func<T, MigrationAction> add,
        Func<T, MigrationAction> drop,
        Func<T, MigrationAction> comment
    ) where T : IMigratableDiff
    {
        foreach (var constraint in constraints)
        {
            if (foldAdds && constraint.Kind == ChangeKind.Add)
            {
                continue;
            }

            EmitConstraintMigration(constraint.Kind, constraint.MigrationScript, actions);
            actions.Add(constraint.Kind switch
            {
                ChangeKind.Add => add(constraint),
                ChangeKind.Remove => drop(constraint),
                _ => comment(constraint),
            });
        }
    }

    private static void EmitConstraintMigration(ChangeKind kind, ChangeScript? migration, List<MigrationAction> actions)
    {
        if (kind == ChangeKind.Add && migration is { } script)
        {
            actions.Add(new ExecuteScript(script));
        }
    }

    private static void EmitIndexes(TableDiff table, List<MigrationAction> actions)
    {
        foreach (var index in table.Indexes)
        {
            actions.Add(index.Kind switch
            {
                ChangeKind.Add => new CreateIndex(new(table.Schema, table.Name), index.Definition!),
                ChangeKind.Remove => new DropIndex(new(table.Schema, table.RenamedFrom ?? table.Name, index.Name)),
                _ => new SetIndexComment(new(table.Schema, table.Name, index.Name), index.Comment!.Old, index.Comment.New),
            });
        }
    }

    private static void EmitTriggers(TableDiff table, List<MigrationAction> actions)
    {
        foreach (var trigger in table.Triggers)
        {
            actions.Add(trigger.Kind switch
            {
                ChangeKind.Add => new CreateTrigger(new(table.Schema, table.Name), trigger.Definition!),
                ChangeKind.Remove => new DropTrigger(new(table.Schema, table.RenamedFrom ?? table.Name, trigger.Name)),
                _ => new SetTriggerComment(new(table.Schema, table.Name, trigger.Name), trigger.Comment!.Old, trigger.Comment.New),
            });
        }
    }

    private static void EmitGrants(TableDiff table, List<MigrationAction> actions)
    {
        foreach (var grant in table.Grants)
        {
            actions.Add(grant.Kind == ChangeKind.Add
                ? new GrantTablePrivileges(new(table.Schema, table.Name), grant.Role, grant.Privileges!.Value)
                : new RevokeTablePrivileges(new(table.Schema, table.RenamedFrom ?? table.Name), grant.Role, grant.Privileges!.Value));
        }
    }
}
