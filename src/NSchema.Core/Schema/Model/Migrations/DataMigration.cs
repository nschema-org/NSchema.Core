using NSchema.Schema.Model.Scripts;

namespace NSchema.Schema.Model.Migrations;

/// <summary>
/// A raw-SQL data migration declared in the DDL, attached to a structural change and spliced into the plan only when the matching change is also planned.
/// </summary>
/// <param name="Name">An optional display name for the migration.</param>
/// <param name="Trigger">The structural change the migration attaches to.</param>
/// <param name="SchemaName">The schema of the target object.</param>
/// <param name="ObjectName">The table the change applies to.</param>
/// <param name="MemberName">The column or constraint name the change targets.</param>
/// <param name="Sql">The raw SQL to run when the change is planned.</param>
public sealed record DataMigration(
    string? Name,
    DataMigrationTrigger Trigger,
    string SchemaName,
    string ObjectName,
    string MemberName,
    string Sql
)
{
    /// <summary>
    /// Whether the SQL must run outside the migration transaction.
    /// </summary>
    public bool RunOutsideTransaction { get; init; }

    /// <summary>
    /// When the migration runs, relative to occurrences of its change event.
    /// </summary>
    public RunCondition RunCondition { get; init; } = RunCondition.Always;

    /// <summary>
    /// The fully qualified path of the change target (<c>schema.table.member</c>).
    /// </summary>
    public string Path => $"{SchemaName}.{ObjectName}.{MemberName}";

    /// <summary>
    /// The human-readable label for messages and plan output: the name when one was declared, otherwise the
    /// trigger and path (e.g. <c>ADD COLUMN app.users.email</c>).
    /// </summary>
    public string Description => Name ?? $"{TriggerText(Trigger)} {Path}";

    /// <summary>
    /// The DDL keyword form of a trigger (e.g. <c>ADD COLUMN</c>), as written in the source.
    /// </summary>
    public static string TriggerText(DataMigrationTrigger trigger) => trigger switch
    {
        DataMigrationTrigger.AddColumn => "ADD COLUMN",
        DataMigrationTrigger.AlterColumnType => "ALTER COLUMN TYPE",
        DataMigrationTrigger.AddConstraint => "ADD CONSTRAINT",
        _ => "UNKNOWN",
    };
}
