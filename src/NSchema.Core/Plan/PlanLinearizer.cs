using System.Collections.Frozen;
using NSchema.Diff.Model;
using NSchema.Plan.Model;
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
using NSchema.Plan.Model.Sequence;
using NSchema.Plan.Model.Tables;
using NSchema.Plan.Model.Triggers;
using NSchema.Plan.Model.Views;
using NSchema.Schema.Model.Scripts;

namespace NSchema.Plan;

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
            EmitSchema(schema, diff, actions);
        }

        // Views are emitted in one cross-schema pass: their create/drop order is governed by a dependency sort
        // (a view after what it reads, dropped before), which the per-schema walk above cannot express.
        EmitViews(diff, actions);

        actions = actions.OrderBy(action => _actionPriorities[action.GetType()]).ToList();

        // Deployment scripts bookend the plan: pre scripts run before everything, post scripts after.
        return [.. ScriptActions(diff, DeploymentPhase.Pre), .. actions, .. ScriptActions(diff, DeploymentPhase.Post)];
    }

    /// <summary>
    /// Resolves a node's script annotation against the diff. The matcher only annotates with scripts it put on
    /// the diff, so a miss is an upstream invariant violation, not an input condition.
    /// </summary>
    private static Script ResolveScript(DatabaseDiff diff, string name) =>
        diff.FindScript(name)
        ?? throw new InvalidOperationException($"The diff annotates a change with script '{name}', but carries no script with that name.");

    private static IEnumerable<ExecuteScript> ScriptActions(DatabaseDiff diff, DeploymentPhase phase) =>
        diff.Scripts.Where(s => s.Event is DeploymentEvent e && e.Phase == phase).Select(s => new ExecuteScript(s));

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
                    actions.Add(new RenameView(view.Schema, view.RenamedFrom, view.Name, view.IsMaterialized));
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

                if (view.Comment is not null)
                {
                    actions.Add(new SetViewComment(view.Schema, view.Name, view.Comment.Old, view.Comment.New, view.IsMaterialized));
                }

                // In-place index changes on a materialized view whose body is unchanged; on a create/recreate the
                // indexes ride along on the definition instead. Index drops sort before RenameView, so on a
                // renamed view they run while it still carries its old name.
                foreach (var index in view.Indexes)
                {
                    actions.Add(index.Kind switch
                    {
                        ChangeKind.Add => new CreateIndex(view.Schema, view.Name, index.Definition!),
                        ChangeKind.Remove => new DropIndex(view.Schema, view.RenamedFrom ?? view.Name, index.Name),
                        _ => new SetIndexComment(view.Schema, view.Name, index.Name, index.Comment!.Old, index.Comment.New),
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
            actions.Add(new DropView(view.Schema, view.RenamedFrom ?? view.Name, view.Materialized?.Old ?? view.IsMaterialized));
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

    private static void EmitSchema(SchemaDiff schema, DatabaseDiff diff, List<MigrationAction> actions)
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
                    EmitTable(table, diff, actions);
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
                    EmitTable(table, diff, actions);
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
                    EmitTable(table, diff, actions);
                }
                break;
        }
    }

    private static void EmitRoutines(SchemaDiff schema, List<MigrationAction> actions)
    {
        foreach (var routine in schema.Routines)
        {
            switch (routine.Kind)
            {
                case ChangeKind.Add:
                    actions.Add(new CreateRoutine(routine.Schema, routine.Definition!));
                    break;

                case ChangeKind.Remove:
                    actions.Add(new DropRoutine(routine.Schema, routine.Name, routine.RoutineKind));
                    break;

                default: // Modify
                    if (routine.RenamedFrom is not null)
                    {
                        actions.Add(new RenameRoutine(routine.Schema, routine.RenamedFrom, routine.Name, routine.RoutineKind));
                    }
                    // A signature (or kind) change recreates (a replace under different arguments would create a
                    // separate overload); a definition-only change replaces in place.
                    if (routine.RequiresRecreate)
                    {
                        actions.Add(new RecreateRoutine(routine.Schema, routine.Definition!));
                    }
                    else if (routine.Definition is not null)
                    {
                        actions.Add(new CreateRoutine(routine.Schema, routine.Definition));
                    }
                    break;
            }

            if (routine.Kind != ChangeKind.Remove && routine.Comment is not null)
            {
                actions.Add(new SetRoutineComment(routine.Schema, routine.Name, routine.Comment.Old, routine.Comment.New, routine.RoutineKind));
            }
        }
    }

    private static void EmitDomains(SchemaDiff schema, List<MigrationAction> actions)
    {
        foreach (var domain in schema.Domains)
        {
            switch (domain.Kind)
            {
                case ChangeKind.Add:
                    actions.Add(new CreateDomain(domain.Schema, domain.Definition!));
                    break;

                case ChangeKind.Remove:
                    actions.Add(new DropDomain(domain.Schema, domain.Name));
                    break;

                default: // Modify
                    if (domain.RenamedFrom is not null)
                    {
                        actions.Add(new RenameDomain(domain.Schema, domain.RenamedFrom, domain.Name));
                    }
                    // A base-type change can't be altered in place, so it recreates (default/not-null/checks rebuild
                    // with the definition); otherwise each facet is altered in place.
                    if (domain.RequiresRecreate)
                    {
                        actions.Add(new RecreateDomain(domain.Schema, domain.Definition!));
                    }
                    else
                    {
                        if (domain.Default is not null)
                        {
                            actions.Add(new AlterDomainDefault(domain.Schema, domain.Name, domain.Default.Old, domain.Default.New));
                        }
                        if (domain.NotNull is not null)
                        {
                            actions.Add(new AlterDomainNotNull(domain.Schema, domain.Name, domain.NotNull.New));
                        }
                        foreach (var check in domain.Checks)
                        {
                            actions.Add(check.Kind == ChangeKind.Remove
                                ? new DropDomainCheck(domain.Schema, domain.Name, check.Name)
                                : new AddDomainCheck(domain.Schema, domain.Name, check.Definition!));
                        }
                    }
                    break;
            }

            if (domain.Kind != ChangeKind.Remove && domain.Comment is not null)
            {
                actions.Add(new SetDomainComment(domain.Schema, domain.Name, domain.Comment.Old, domain.Comment.New));
            }
        }
    }

    private static void EmitCompositeTypes(SchemaDiff schema, List<MigrationAction> actions)
    {
        foreach (var type in schema.CompositeTypes)
        {
            switch (type.Kind)
            {
                case ChangeKind.Add:
                    actions.Add(new CreateCompositeType(type.Schema, type.Definition!));
                    break;

                case ChangeKind.Remove:
                    actions.Add(new DropCompositeType(type.Schema, type.Name));
                    break;

                default: // Modify
                    if (type.RenamedFrom is not null)
                    {
                        actions.Add(new RenameCompositeType(type.Schema, type.RenamedFrom, type.Name));
                    }
                    // Every field change applies in place: a matched field whose type differs is retyped, a missing
                    // field is dropped, a new field is added. There is no recreate.
                    foreach (var field in type.Fields)
                    {
                        actions.Add(field.Kind switch
                        {
                            ChangeKind.Remove => new DropCompositeField(type.Schema, type.Name, field.Name),
                            ChangeKind.Modify => new AlterCompositeFieldType(type.Schema, type.Name, field.Name, field.Type!.Old!, field.Type.New!),
                            _ => new AddCompositeField(type.Schema, type.Name, field.Definition!),
                        });
                    }
                    break;
            }

            if (type.Kind != ChangeKind.Remove && type.Comment is not null)
            {
                actions.Add(new SetCompositeTypeComment(type.Schema, type.Name, type.Comment.Old, type.Comment.New));
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

    private static void EmitTable(TableDiff table, DatabaseDiff diff, List<MigrationAction> actions)
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
                EmitConstraints(table, diff, actions);
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
                    EmitColumn(table, column, diff, actions);
                }
                EmitConstraints(table, diff, actions);
                EmitIndexes(table, actions);
                EmitTriggers(table, actions);
                EmitGrants(table, actions);
                break;
        }
    }

    private static void EmitColumn(TableDiff table, ColumnDiff column, DatabaseDiff diff, List<MigrationAction> actions)
    {
        switch (column.Kind)
        {
            case ChangeKind.Add:
                // A required column with a matched backfill migration is decomposed: added nullable, backfilled
                // by the migration SQL, then tightened to NOT NULL. Identity and generated columns fill
                // themselves and a default covers existing rows, so those adds keep their declared shape.
                if (column is { MigrationScript: { } backfill, Definition: { IsNullable: false, DefaultExpression: null, IsIdentity: false, GeneratedExpression: null } })
                {
                    actions.Add(new AddColumn(table.Schema, table.Name, column.Definition with { IsNullable = true }));
                    actions.Add(new ExecuteScript(ResolveScript(diff, backfill)));
                    actions.Add(new AlterColumnNullability(table.Schema, table.Name, column.Name, OldNullable: true, NewNullable: false, column.Definition.Type));
                }
                else
                {
                    actions.Add(new AddColumn(table.Schema, table.Name, column.Definition!));
                    if (column.MigrationScript is { } migration)
                    {
                        actions.Add(new ExecuteScript(ResolveScript(diff, migration)));
                    }
                }
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
                    // A matched migration prepares the data for the cast; the priority table runs it first.
                    if (column.MigrationScript is { } prep)
                    {
                        actions.Add(new ExecuteScript(ResolveScript(diff, prep)));
                    }
                    actions.Add(new AlterColumnType(table.Schema, table.Name, column.Name, column.Type.Old!, column.Type.New!, column.Definition?.IsNullable));
                }
                if (column.Nullability is not null)
                {
                    actions.Add(new AlterColumnNullability(table.Schema, table.Name, column.Name, column.Nullability.Old, column.Nullability.New, column.Definition?.Type));
                }
                if (column.Default is not null)
                {
                    actions.Add(new SetColumnDefault(table.Schema, table.Name, column.Name, column.Default.Old, column.Default.New));
                }
                if (column.Generated is not null)
                {
                    actions.Add(new SetColumnGenerated(table.Schema, table.Name, column.Name, column.Generated.Old, column.Generated.New));
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

    // Drops and revokes are sorted before RenameTable, so on a renamed table they run while it still carries
    // its old name; every action from the rename onward targets the new name.
    private static void EmitConstraints(TableDiff table, DatabaseDiff diff, List<MigrationAction> actions)
    {
        var preRenameName = table.RenamedFrom ?? table.Name;

        // A constraint add's matched migration prepares the data the constraint depends on (de-duplication,
        // backfill); the priority table runs every data migration before the constraint adds.
        foreach (var pk in table.PrimaryKey)
        {
            EmitConstraintMigration(pk.Kind, pk.MigrationScript, diff, actions);
            actions.Add(pk.Kind switch
            {
                ChangeKind.Add => new AddPrimaryKey(table.Schema, table.Name, pk.Definition!),
                ChangeKind.Remove => new DropPrimaryKey(table.Schema, preRenameName, pk.Name),
                _ => new SetConstraintComment(table.Schema, table.Name, pk.Name, pk.Comment!.Old, pk.Comment.New),
            });
        }

        foreach (var fk in table.ForeignKeys)
        {
            EmitConstraintMigration(fk.Kind, fk.MigrationScript, diff, actions);
            actions.Add(fk.Kind switch
            {
                ChangeKind.Add => new AddForeignKey(table.Schema, table.Name, fk.Definition!),
                ChangeKind.Remove => new DropForeignKey(table.Schema, preRenameName, fk.Name),
                _ => new SetConstraintComment(table.Schema, table.Name, fk.Name, fk.Comment!.Old, fk.Comment.New),
            });
        }

        foreach (var uq in table.UniqueConstraints)
        {
            EmitConstraintMigration(uq.Kind, uq.MigrationScript, diff, actions);
            actions.Add(uq.Kind switch
            {
                ChangeKind.Add => new AddUniqueConstraint(table.Schema, table.Name, uq.Definition!),
                ChangeKind.Remove => new DropUniqueConstraint(table.Schema, preRenameName, uq.Name),
                _ => new SetConstraintComment(table.Schema, table.Name, uq.Name, uq.Comment!.Old, uq.Comment.New),
            });
        }

        foreach (var ck in table.Checks)
        {
            EmitConstraintMigration(ck.Kind, ck.MigrationScript, diff, actions);
            actions.Add(ck.Kind switch
            {
                ChangeKind.Add => new AddCheckConstraint(table.Schema, table.Name, ck.Definition!),
                ChangeKind.Remove => new DropCheckConstraint(table.Schema, preRenameName, ck.Name),
                _ => new SetConstraintComment(table.Schema, table.Name, ck.Name, ck.Comment!.Old, ck.Comment.New),
            });
        }

        foreach (var ex in table.ExclusionConstraints)
        {
            EmitConstraintMigration(ex.Kind, ex.MigrationScript, diff, actions);
            actions.Add(ex.Kind switch
            {
                ChangeKind.Add => new AddExclusionConstraint(table.Schema, table.Name, ex.Definition!),
                ChangeKind.Remove => new DropExclusionConstraint(table.Schema, preRenameName, ex.Name),
                _ => new SetConstraintComment(table.Schema, table.Name, ex.Name, ex.Comment!.Old, ex.Comment.New),
            });
        }
    }

    private static void EmitConstraintMigration(ChangeKind kind, string? migrationName, DatabaseDiff diff, List<MigrationAction> actions)
    {
        if (kind == ChangeKind.Add && migrationName is not null)
        {
            actions.Add(new ExecuteScript(ResolveScript(diff, migrationName)));
        }
    }

    private static void EmitIndexes(TableDiff table, List<MigrationAction> actions)
    {
        foreach (var index in table.Indexes)
        {
            actions.Add(index.Kind switch
            {
                ChangeKind.Add => new CreateIndex(table.Schema, table.Name, index.Definition!),
                ChangeKind.Remove => new DropIndex(table.Schema, table.RenamedFrom ?? table.Name, index.Name),
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
                ChangeKind.Remove => new DropTrigger(table.Schema, table.RenamedFrom ?? table.Name, trigger.Name),
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
                : new RevokeTablePrivileges(table.Schema, table.RenamedFrom ?? table.Name, grant.Role, grant.Privileges!.Value));
        }
    }
}
