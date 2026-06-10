using System.Text;
using NSchema.Schema.Model;

namespace NSchema.Schema;

/// <summary>
/// Default <see cref="ISchemaRenderer"/> that presents a schema as an indented tree:
/// schema → table → columns, primary key, foreign keys, indexes, and grants.
/// </summary>
internal sealed class DefaultSchemaRenderer : ISchemaRenderer
{
    private const string Indent = "  ";

    public string Render(DatabaseSchema schema)
    {
        if (schema.Schemas.Count == 0)
        {
            return "Schema is empty.";
        }

        var sb = new StringBuilder();

        foreach (var definition in schema.Schemas)
        {
            RenderSchema(sb, definition);
        }

        return sb.ToString().Trim();
    }

    private static void RenderSchema(StringBuilder sb, SchemaDefinition schema)
    {
        sb.AppendLine();
        sb.Append("schema ").Append(schema.Name).AppendLine(CommentSuffix(schema.Comment));

        foreach (var grant in schema.Grants)
        {
            sb.Append(Indent).Append("grant usage to ").AppendLine(grant.Role);
        }

        foreach (var table in schema.Tables)
        {
            RenderTable(sb, table);
        }

        foreach (var view in schema.Views)
        {
            RenderView(sb, view);
        }
    }

    private static void RenderView(StringBuilder sb, View view)
    {
        sb.Append(Indent).Append("view ").Append(view.Name).AppendLine(CommentSuffix(view.Comment));
        foreach (var dependency in view.DependsOn)
        {
            sb.Append(Indent).Append(Indent).Append("reads ").Append(dependency.Schema).Append('.').AppendLine(dependency.Name);
        }
    }

    private static void RenderTable(StringBuilder sb, Table table)
    {
        sb.Append(Indent).Append("table ").Append(table.Name).AppendLine(CommentSuffix(table.Comment));

        foreach (var column in table.Columns)
        {
            sb.Append(Indent).Append(Indent).AppendLine(FormatColumn(column));
        }

        if (table.PrimaryKey is { } pk)
        {
            sb.Append(Indent).Append(Indent)
                .Append("primary key ").Append(pk.Name)
                .Append(" (").Append(string.Join(", ", pk.ColumnNames)).Append(')')
                .AppendLine(CommentSuffix(pk.Comment));
        }

        foreach (var fk in table.ForeignKeys)
        {
            sb.Append(Indent).Append(Indent)
                .Append("foreign key ").Append(fk.Name)
                .Append(" (").Append(string.Join(", ", fk.ColumnNames)).Append(") -> ")
                .Append(fk.ReferencedSchema).Append('.').Append(fk.ReferencedTable)
                .Append(" (").Append(string.Join(", ", fk.ReferencedColumnNames)).Append(')')
                .AppendLine(CommentSuffix(fk.Comment));
        }

        foreach (var unique in table.UniqueConstraints)
        {
            sb.Append(Indent).Append(Indent)
                .Append("unique ").Append(unique.Name)
                .Append(" (").Append(string.Join(", ", unique.ColumnNames)).Append(')')
                .AppendLine(CommentSuffix(unique.Comment));
        }

        foreach (var check in table.CheckConstraints)
        {
            sb.Append(Indent).Append(Indent)
                .Append("check ").Append(check.Name)
                .Append(" (").Append(check.Expression).Append(')')
                .AppendLine(CommentSuffix(check.Comment));
        }

        foreach (var index in table.Indexes)
        {
            sb.Append(Indent).Append(Indent)
                .Append("index ").Append(index.Name)
                .Append(" (").Append(string.Join(", ", index.ColumnNames)).Append(')')
                .Append(index.IsUnique ? " unique" : string.Empty)
                .AppendLine(index.Predicate is { } p ? $" where {p}" : string.Empty);
        }

        foreach (var grant in table.Grants)
        {
            sb.Append(Indent).Append(Indent)
                .Append("grant ").Append(FormatPrivileges(grant.Privileges))
                .Append(" to ").AppendLine(grant.Role);
        }
    }

    private static string FormatColumn(Column column)
    {
        var text = $"{column.Name} {column.Type} {(column.IsNullable ? "null" : "not null")}";
        if (column.IsIdentity)
        {
            text += " identity";
        }

        if (column.DefaultExpression is { } @default)
        {
            text += $" default {@default}";
        }

        return text + CommentSuffix(column.Comment);
    }

    private static string CommentSuffix(string? comment) => comment is null ? string.Empty : $" (\"{comment}\")";

    // Decompose the privilege flags into the underlying SQL privileges rather than rendering the enum name,
    // which would surface aliases (e.g. ReadOnly for Select) and composites (All) instead of the real grants.
    private static string FormatPrivileges(TablePrivilege privileges)
    {
        if (privileges == TablePrivilege.None)
        {
            return "no privileges";
        }

        var parts = new List<string>(4);
        if (privileges.HasFlag(TablePrivilege.Select))
        {
            parts.Add("SELECT");
        }

        if (privileges.HasFlag(TablePrivilege.Insert))
        {
            parts.Add("INSERT");
        }

        if (privileges.HasFlag(TablePrivilege.Update))
        {
            parts.Add("UPDATE");
        }

        if (privileges.HasFlag(TablePrivilege.Delete))
        {
            parts.Add("DELETE");
        }

        return string.Join(", ", parts);
    }
}
