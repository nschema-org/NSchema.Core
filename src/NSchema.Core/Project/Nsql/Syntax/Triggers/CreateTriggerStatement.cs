using NSchema.Model;

namespace NSchema.Project.Nsql.Syntax.Triggers;

/// <summary>
/// <c>CREATE TRIGGER name timing event [OR event]… [UPDATE OF columns] ON schema.table
/// [FOR EACH ROW|STATEMENT] [WHEN (expr)] action;</c>
/// </summary>
/// <param name="Name">The trigger name.</param>
/// <param name="Timing">The timing keyword.</param>
/// <param name="Events">The firing events.</param>
/// <param name="On">The table the trigger attaches to.</param>
/// <param name="Action">The trigger action: a function call or an inline body.</param>
/// <param name="UpdateOfColumns">The <c>UPDATE OF</c> columns, or <see langword="null"/> when absent.</param>
/// <param name="Level">The <c>FOR EACH</c> level (default <see cref="TriggerLevel.Statement"/>).</param>
/// <param name="When">The <c>WHEN</c> condition, or <see langword="null"/>.</param>
public sealed record CreateTriggerStatement(
    Identifier Name,
    TriggerTiming Timing,
    TriggerEvent Events,
    QualifiedName On,
    TriggerAction Action,
    IReadOnlyList<Identifier>? UpdateOfColumns = null,
    TriggerLevel Level = TriggerLevel.Statement,
    SqlText? When = null
) : NsqlStatement;
