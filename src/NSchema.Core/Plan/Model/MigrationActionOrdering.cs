using System.Collections.Frozen;
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

namespace NSchema.Plan.Model;

/// <summary>
/// Orders migration actions by their database dependencies.
/// </summary>
internal static class MigrationActionOrdering
{
    private static readonly IReadOnlyDictionary<Type, int> _priorities = new[]
    {
        typeof(RenameSchema), typeof(DropView), typeof(DropTrigger), typeof(DropForeignKey),
        typeof(DropCheckConstraint), typeof(DropExclusionConstraint), typeof(DropUniqueConstraint),
        typeof(DropIndex), typeof(DropPrimaryKey), typeof(RevokeSchemaUsage), typeof(RevokeTablePrivileges),
        typeof(CreateExtension), typeof(AlterExtension), typeof(CreateSchema), typeof(RenameEnum),
        typeof(RenameSequence), typeof(CreateEnum), typeof(CreateSequence), typeof(AddEnumValue),
        typeof(AlterSequence), typeof(RenameDomain), typeof(CreateDomain), typeof(RecreateDomain),
        typeof(AlterDomainDefault), typeof(AlterDomainNotNull), typeof(AddDomainCheck), typeof(DropDomainCheck),
        typeof(RenameCompositeType), typeof(CreateCompositeType), typeof(AddCompositeField),
        typeof(AlterCompositeFieldType), typeof(DropCompositeField), typeof(RenameRoutine), typeof(CreateRoutine),
        typeof(RecreateRoutine), typeof(RenameTable), typeof(RenameView), typeof(CreateTable), typeof(DropColumn),
        typeof(RenameColumn), typeof(AddColumn), typeof(ExecuteScript), typeof(AlterColumn),
        typeof(AlterIdentitySequence), typeof(SetColumnDefault), typeof(SetColumnGenerated), typeof(AddPrimaryKey),
        typeof(AddUniqueConstraint), typeof(AddForeignKey), typeof(AddCheckConstraint), typeof(AddExclusionConstraint),
        typeof(CreateIndex), typeof(CreateTrigger), typeof(CreateView), typeof(GrantSchemaUsage),
        typeof(GrantTablePrivileges), typeof(SetSchemaComment), typeof(SetTableComment), typeof(SetColumnComment),
        typeof(SetIndexComment), typeof(SetTriggerComment), typeof(SetConstraintComment), typeof(SetViewComment),
        typeof(SetEnumComment), typeof(SetSequenceComment), typeof(SetRoutineComment), typeof(SetDomainComment),
        typeof(SetCompositeTypeComment), typeof(SetExtensionComment), typeof(DropTable), typeof(DropRoutine),
        typeof(DropDomain), typeof(DropCompositeType), typeof(DropEnum), typeof(DropSequence), typeof(DropSchema),
        typeof(DropExtension),
    }.Index().ToFrozenDictionary(item => item.Item, item => item.Index);

    public static IReadOnlyList<MigrationAction> Order(IEnumerable<MigrationAction> actions) =>
        [.. actions.OrderBy(PriorityOf)];

    internal static bool HasPriority(Type actionType) => _priorities.ContainsKey(actionType);

    private static int PriorityOf(MigrationAction action) => _priorities.TryGetValue(action.GetType(), out var priority)
        ? priority
        : throw new InvalidOperationException($"Migration action '{action.GetType().Name}' has no ordering priority.");
}
