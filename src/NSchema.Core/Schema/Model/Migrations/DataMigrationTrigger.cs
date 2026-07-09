namespace NSchema.Schema.Model.Migrations;

/// <summary>
/// The structural change a <see cref="DataMigration"/> attaches to.
/// </summary>
public enum DataMigrationTrigger
{
    /// <summary>
    /// The migration runs when the named column is added to an existing table.
    /// </summary>
    AddColumn,

    /// <summary>
    /// The migration runs before the named column's type is changed.
    /// </summary>
    AlterColumnType,

    /// <summary>
    /// The migration runs before the named constraint is added to an existing table.
    /// </summary>
    AddConstraint,
}
