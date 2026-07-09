using NSchema.Schema.Model.Migrations;

namespace NSchema.Plan.Model.Migrations;

/// <summary>
/// Runs a declared data migration's raw SQL, spliced into the plan because its matching structural change is being applied.
/// </summary>
/// <param name="Name">The migration's declared name, when it has one.</param>
/// <param name="Trigger">The structural change the migration attaches to.</param>
/// <param name="SchemaName">The schema of the target object.</param>
/// <param name="TableName">The table the change applies to.</param>
/// <param name="MemberName">The column or constraint name the change targets.</param>
/// <param name="Sql">The raw SQL to execute.</param>
public sealed record ExecuteDataMigration(
    string? Name,
    DataMigrationTrigger Trigger,
    string SchemaName,
    string TableName,
    string MemberName,
    string Sql
) : MigrationAction
{
    /// <summary>
    /// Whether the SQL must run outside the migration transaction.
    /// </summary>
    public bool RunOutsideTransaction { get; init; }

    /// <summary>
    /// The human-readable label for plan output: the name when one was declared, otherwise the trigger and path.
    /// </summary>
    public string Description => Name ?? $"{DataMigration.TriggerText(Trigger)} {SchemaName}.{TableName}.{MemberName}";

    /// <inheritdoc />
    public override bool IsDestructive => false;
}
