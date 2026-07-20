using System.Text;
using NSchema.Model.Tables;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Tables;

namespace NSchema.Plan.Backends;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation of a table.
    /// </summary>
    protected abstract Result<IReadOnlyList<SqlStatement>> CreateTable(CreateTable action);

    /// <summary>
    /// Renders the removal of a table.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropTable(DropTable action) =>
        Statement($"DROP TABLE {Qualify(action.Table)}");

    /// <summary>
    /// Renders the renaming of a table.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RenameTable(RenameTable action) =>
        Statement($"ALTER TABLE {Qualify(action.Table)} RENAME TO {Quote(action.NewName)}");

    /// <summary>
    /// Renders adding a primary key constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AddPrimaryKey(AddPrimaryKey action) =>
        Statement($"ALTER TABLE {Qualify(action.Table)} ADD CONSTRAINT {Quote(action.PrimaryKey.Name)} PRIMARY KEY ({ColumnList(action.PrimaryKey.ColumnNames)})");

    /// <summary>
    /// Renders dropping a primary key constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropPrimaryKey(DropPrimaryKey action) =>
        Statement($"ALTER TABLE {Qualify(action.PrimaryKey.Owner)} DROP CONSTRAINT {Quote(action.PrimaryKey.Member)}");

    /// <summary>
    /// Renders adding a foreign key constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AddForeignKey(AddForeignKey action)
    {
        var key = action.ForeignKey;
        var sql = new StringBuilder(
            $"ALTER TABLE {Qualify(action.Table)} ADD CONSTRAINT {Quote(key.Name)} " +
            $"FOREIGN KEY ({ColumnList(key.ColumnNames)}) " +
            $"REFERENCES {Qualify(key.References)} ({ColumnList(key.ReferencedColumnNames)})");

        if (key.OnDelete != ReferentialAction.NoAction)
        {
            sql.Append($" ON DELETE {ReferentialActionSql(key.OnDelete)}");
        }

        if (key.OnUpdate != ReferentialAction.NoAction)
        {
            sql.Append($" ON UPDATE {ReferentialActionSql(key.OnUpdate)}");
        }

        return Statement(sql.ToString());
    }

    /// <summary>
    /// Renders dropping a foreign key constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropForeignKey(DropForeignKey action) =>
        Statement($"ALTER TABLE {Qualify(action.ForeignKey.Owner)} DROP CONSTRAINT {Quote(action.ForeignKey.Member)}");

    /// <summary>
    /// Renders granting table privileges to a role.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> GrantTablePrivileges(GrantTablePrivileges action) =>
        action.Privileges == TablePrivilege.None
            ? Statements()
            : Statement($"GRANT {PrivilegeList(action.Privileges)} ON {Qualify(action.Table)} TO {Quote(action.Role)}");

    /// <summary>
    /// Renders revoking table privileges from a role.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RevokeTablePrivileges(RevokeTablePrivileges action) =>
        action.Privileges == TablePrivilege.None
            ? Statements()
            : Statement($"REVOKE {PrivilegeList(action.Privileges)} ON {Qualify(action.Table)} FROM {Quote(action.Role)}");

    /// <summary>
    /// Renders setting or clearing a table's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetTableComment(SetTableComment action) =>
        Unsupported(action);

    private static string ReferentialActionSql(ReferentialAction action) => action switch
    {
        ReferentialAction.Cascade => "CASCADE",
        ReferentialAction.SetNull => "SET NULL",
        ReferentialAction.SetDefault => "SET DEFAULT",
        _ => "NO ACTION",
    };

    private static string PrivilegeList(TablePrivilege privileges) => string.Join(", ", privileges.SqlNames());
}
