using NSchema.Model;

namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// A column definition: <c>name type [NOT NULL | NULL] [IDENTITY [(options)]] [DEFAULT expr]
/// [GENERATED ALWAYS AS (expr) STORED] [RENAMED FROM old]</c>.
/// </summary>
/// <param name="Name">The column name.</param>
/// <param name="Type">The column type as written.</param>
/// <param name="IsNullable">Whether the column allows nulls (<c>NOT NULL</c> absent).</param>
/// <param name="IsIdentity">Whether the column is declared <c>IDENTITY</c>.</param>
/// <param name="IdentityOptions">The identity options clause, or <see langword="null"/>.</param>
/// <param name="Default">The <c>DEFAULT</c> expression, or <see langword="null"/>.</param>
/// <param name="Generated">The <c>GENERATED ALWAYS AS</c> expression, or <see langword="null"/>.</param>
public sealed record ColumnDefinition(
    Identifier Name,
    TypeName Type,
    bool IsNullable = true,
    bool IsIdentity = false,
    IdentityOptionsClause? IdentityOptions = null,
    SqlText? Default = null,
    SqlText? Generated = null
) : TableMember;
