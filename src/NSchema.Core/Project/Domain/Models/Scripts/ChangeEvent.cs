namespace NSchema.Project.Domain.Models.Scripts;

/// <summary>
/// A structural change event: the script is spliced into the plan only when the matching change is also planned.
/// </summary>
/// <param name="Trigger">The structural change the script attaches to.</param>
/// <param name="TableName">The table the change applies to.</param>
/// <param name="MemberName">The column or constraint name the change targets.</param>
public sealed record ChangeEvent(
    ChangeTrigger Trigger,
    string TableName,
    string MemberName
) : ScriptEvent
{
    /// <summary>
    /// The fully qualified path of the change target (<c>schema.table.member</c>).
    /// </summary>
    public string Path => $"{ScopeSchema}.{TableName}.{MemberName}";

    /// <inheritdoc />
    public override string Description => $"{TriggerText(Trigger)} {Path}";

    /// <summary>
    /// The DDL keyword form of a trigger (e.g. <c>ADD COLUMN</c>), as written in the source.
    /// </summary>
    public static string TriggerText(ChangeTrigger trigger) => trigger switch
    {
        ChangeTrigger.AddColumn => "ADD COLUMN",
        ChangeTrigger.AlterColumnType => "ALTER COLUMN TYPE",
        ChangeTrigger.AddConstraint => "ADD CONSTRAINT",
        _ => "UNKNOWN",
    };
}
