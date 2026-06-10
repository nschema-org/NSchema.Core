using System.Text;
using NSchema.Schema.Model;

namespace NSchema.Schema.Serialization.Ddl;

/// <summary>
/// Emits a <see cref="DatabaseSchema"/> as canonical NSchema DDL (see <c>docs/dsl-grammar.md</c>).
/// </summary>
internal static class DdlSchemaWriter
{
    public static string Write(DatabaseSchema schema)
    {
        var sb = new StringBuilder();
        var first = true;

        foreach (var definition in schema.Schemas)
        {
            if (!first)
            {
                sb.AppendLine();
            }
            first = false;
            WriteSchema(sb, definition);
        }

        foreach (var dropped in schema.DroppedSchemas)
        {
            if (!first)
            {
                sb.AppendLine();
            }
            first = false;
            sb.Append("DROP SCHEMA ").Append(dropped).AppendLine(";");
        }

        return sb.ToString();
    }

    private static void WriteSchema(StringBuilder sb, SchemaDefinition schema)
    {
        WriteDocComment(sb, schema.Comment, indent: "");
        sb.Append("CREATE ");
        if (schema.IsPartial)
        {
            sb.Append("PARTIAL ");
        }
        sb.Append("SCHEMA ").Append(schema.Name);
        if (schema.OldName is { } oldName)
        {
            sb.Append(" RENAMED FROM ").Append(oldName);
        }
        sb.AppendLine(";");

        foreach (var grant in schema.Grants)
        {
            sb.Append("GRANT USAGE ON SCHEMA ").Append(schema.Name).Append(" TO ").Append(grant.Role).AppendLine(";");
        }

        foreach (var table in schema.Tables)
        {
            sb.AppendLine();
            WriteTable(sb, schema.Name, table);
        }

        foreach (var view in schema.Views)
        {
            sb.AppendLine();
            WriteView(sb, schema.Name, view);
        }

        foreach (var dropped in schema.DroppedTables)
        {
            sb.Append("DROP TABLE ").Append(schema.Name).Append('.').Append(dropped).AppendLine(";");
        }

        foreach (var dropped in schema.DroppedViews)
        {
            sb.Append("DROP VIEW ").Append(schema.Name).Append('.').Append(dropped).AppendLine(";");
        }
    }

    private static void WriteView(StringBuilder sb, string schemaName, View view)
    {
        WriteDocComment(sb, view.Comment, indent: "");
        sb.Append("CREATE VIEW ").Append(schemaName).Append('.').Append(view.Name);
        if (view.OldName is { } oldName)
        {
            sb.Append(" RENAMED FROM ").Append(oldName);
        }
        sb.Append(" AS ").Append(view.Body).AppendLine(";");
    }

    private static void WriteTable(StringBuilder sb, string schemaName, Table table)
    {
        WriteDocComment(sb, table.Comment, indent: "");
        sb.Append("CREATE TABLE ").Append(schemaName).Append('.').Append(table.Name);
        if (table.OldName is { } oldName)
        {
            sb.Append(" RENAMED FROM ").Append(oldName);
        }
        sb.AppendLine(" (");

        var members = new List<(string? Comment, string Text)>();
        foreach (var column in table.Columns)
        {
            members.Add((column.Comment, ColumnText(column)));
        }
        if (table.PrimaryKey is { } pk)
        {
            members.Add((pk.Comment, $"CONSTRAINT {pk.Name} PRIMARY KEY ({Columns(pk.ColumnNames)})"));
        }
        foreach (var fk in table.ForeignKeys)
        {
            members.Add((fk.Comment, ForeignKeyText(fk)));
        }
        foreach (var unique in table.UniqueConstraints)
        {
            members.Add((unique.Comment, $"CONSTRAINT {unique.Name} UNIQUE ({Columns(unique.ColumnNames)})"));
        }
        foreach (var check in table.CheckConstraints)
        {
            members.Add((check.Comment, $"CONSTRAINT {check.Name} CHECK ({check.Expression})"));
        }
        foreach (var index in table.Indexes)
        {
            members.Add((index.Comment, IndexText(index)));
        }

        for (var i = 0; i < members.Count; i++)
        {
            WriteDocComment(sb, members[i].Comment, indent: "  ");
            sb.Append("  ").Append(members[i].Text).AppendLine(i < members.Count - 1 ? "," : string.Empty);
        }
        sb.AppendLine(");");

        foreach (var grant in table.Grants)
        {
            sb.Append("GRANT ").Append(Privileges(grant.Privileges))
                .Append(" ON ").Append(schemaName).Append('.').Append(table.Name)
                .Append(" TO ").Append(grant.Role).AppendLine(";");
        }
    }

    private static string ColumnText(Column column)
    {
        var sb = new StringBuilder();
        sb.Append(column.Name).Append(' ').Append(column.Type);
        if (!column.IsNullable)
        {
            sb.Append(" NOT NULL");
        }
        if (column.IsIdentity)
        {
            sb.Append(" IDENTITY");
            if (IdentityOptionsText(column.IdentityOptions) is { } options)
            {
                sb.Append(" (").Append(options).Append(')');
            }
        }
        if (column.DefaultExpression is { } @default)
        {
            sb.Append(" DEFAULT ").Append(@default);
        }
        if (column.OldName is { } oldName)
        {
            sb.Append(" RENAMED FROM ").Append(oldName);
        }
        return sb.ToString();
    }

    private static string? IdentityOptionsText(IdentityOptions? options)
    {
        if (options is null)
        {
            return null;
        }
        var parts = new List<string>();
        if (options.StartWith is { } start)
        {
            parts.Add($"START {start}");
        }
        if (options.IncrementBy is { } increment)
        {
            parts.Add($"INCREMENT {increment}");
        }
        if (options.MinValue is { } min)
        {
            parts.Add($"MINVALUE {min}");
        }
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string ForeignKeyText(ForeignKey fk)
    {
        var sb = new StringBuilder();
        sb.Append("CONSTRAINT ").Append(fk.Name)
            .Append(" FOREIGN KEY (").Append(Columns(fk.ColumnNames)).Append(')')
            .Append(" REFERENCES ").Append(fk.ReferencedSchema).Append('.').Append(fk.ReferencedTable)
            .Append(" (").Append(Columns(fk.ReferencedColumnNames)).Append(')');
        if (fk.OnDelete != ReferentialAction.NoAction)
        {
            sb.Append(" ON DELETE ").Append(ActionText(fk.OnDelete));
        }
        if (fk.OnUpdate != ReferentialAction.NoAction)
        {
            sb.Append(" ON UPDATE ").Append(ActionText(fk.OnUpdate));
        }
        return sb.ToString();
    }

    private static string IndexText(TableIndex index)
    {
        var sb = new StringBuilder();
        if (index.IsUnique)
        {
            sb.Append("UNIQUE ");
        }
        sb.Append("INDEX ").Append(index.Name).Append(" (").Append(Columns(index.ColumnNames)).Append(')');
        if (index.Predicate is { } predicate)
        {
            sb.Append(" WHERE (").Append(predicate).Append(')');
        }
        return sb.ToString();
    }

    private static string ActionText(ReferentialAction action) => action switch
    {
        ReferentialAction.Cascade => "CASCADE",
        ReferentialAction.SetNull => "SET NULL",
        ReferentialAction.SetDefault => "SET DEFAULT",
        _ => "NO ACTION",
    };

    private static string Privileges(TablePrivilege privileges)
    {
        var parts = new List<string>();
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

    private static string Columns(IReadOnlyList<string> columns) => string.Join(", ", columns);

    private static void WriteDocComment(StringBuilder sb, string? comment, string indent)
    {
        if (comment is null)
        {
            return;
        }
        foreach (var line in comment.Split('\n'))
        {
            sb.Append(indent).Append("--- ").AppendLine(line);
        }
    }
}
