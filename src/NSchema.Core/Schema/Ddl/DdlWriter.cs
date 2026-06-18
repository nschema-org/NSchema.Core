using System.Globalization;
using System.Text;
using NSchema.Configuration;
using NSchema.Schema.Ddl.Model;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Enums;
using NSchema.Schema.Model.Extensions;
using NSchema.Schema.Model.Functions;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Procedures;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Scripts;
using NSchema.Schema.Model.Sequences;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Triggers;
using NSchema.Schema.Model.Views;

namespace NSchema.Schema.Ddl;

/// <summary>
/// Emits a <see cref="DatabaseSchema"/> as canonical NSchema DDL.
/// </summary>
public sealed class DdlWriter
{
    /// <summary>
    /// The singleton instance of <see cref="DdlWriter"/> for convenience.
    /// </summary>
    public static readonly DdlWriter Instance = new();

    /// <summary>
    /// Writes <paramref name="schema"/> as canonical NSchema DDL.
    /// </summary>
    /// <param name="schema">The schema to write.</param>
    /// <returns>The canonical NSchema DDL for <paramref name="schema"/>.</returns>
    public string Write(DatabaseSchema schema) => Write(new DdlDocument(schema, [], []));

    /// <summary>
    /// Writes a full <see cref="DdlDocument"/> as canonical NSchema DDL.
    /// </summary>
    /// <param name="document">The document to write.</param>
    /// <returns>The canonical NSchema DDL for <paramref name="document"/>.</returns>
    public string Write(DdlDocument document)
    {
        var sb = new StringBuilder();
        var first = true;

        foreach (var block in document.Config)
        {
            Separate(sb, ref first);
            WriteConfigBlock(sb, block);
        }

        // Extensions are database-global and are created first, so they precede the schemas.
        foreach (var extension in document.Schema.Extensions)
        {
            Separate(sb, ref first);
            WriteExtension(sb, extension);
        }

        foreach (var definition in document.Schema.Schemas)
        {
            Separate(sb, ref first);
            WriteSchema(sb, definition);
        }

        foreach (var dropped in document.Schema.DroppedSchemas)
        {
            Separate(sb, ref first);
            sb.Append("DROP SCHEMA ").Append(dropped).AppendLine(";");
        }

        // Extensions are dropped last, so their drops trail the schema drops.
        foreach (var dropped in document.Schema.DroppedExtensions)
        {
            Separate(sb, ref first);
            sb.Append("DROP EXTENSION ").Append(ExtensionName(dropped)).AppendLine(";");
        }

        foreach (var script in document.Scripts)
        {
            Separate(sb, ref first);
            WriteScript(sb, script);
        }

        return sb.ToString();
    }

    private static void Separate(StringBuilder sb, ref bool first)
    {
        if (!first)
        {
            sb.AppendLine();
        }
        first = false;
    }

    private static void WriteConfigBlock(StringBuilder sb, ConfigBlock block)
    {
        sb.Append(block.Type.ToUpperInvariant());
        if (block.Label is { } label)
        {
            sb.Append(' ').Append(label);
        }
        if (block.Attributes.Count == 0)
        {
            sb.AppendLine(" ();");
            return;
        }

        sb.AppendLine(" (");
        var i = 0;
        foreach (var (key, value) in block.Attributes)
        {
            sb.Append("  ").Append(key).Append(" = ").Append(ConfigValueText(value));
            sb.AppendLine(++i < block.Attributes.Count ? "," : string.Empty);
        }
        sb.AppendLine(");");
    }

    private static string ConfigValueText(ConfigValue value) => value.Kind switch
    {
        ConfigValueKind.String => $"'{value.AsString().Replace("'", "''")}'",
        ConfigValueKind.Integer => value.AsInteger().ToString(CultureInfo.InvariantCulture),
        ConfigValueKind.Boolean => value.AsBoolean() ? "true" : "false",
        ConfigValueKind.Identifier => value.AsString(),
        _ => throw new ArgumentOutOfRangeException(nameof(value), value.Kind, "Unknown configuration value kind."),
    };

    private static void WriteScript(StringBuilder sb, Script script)
    {
        sb.Append(script.Type == ScriptType.PreDeployment ? "PRE" : "POST")
            .Append(" DEPLOYMENT '").Append(script.Name.Replace("'", "''")).Append('\'');
        if (script.RunOutsideTransaction)
        {
            sb.Append(" (run_outside_transaction = true)");
        }

        // Emit the body in a dollar-quoted block, choosing a tag that doesn't occur in the body so it is
        // taken verbatim. The reader strips the delimiters and trims surrounding whitespace, so a body
        // stored without its delimiters round-trips back to the same text.
        var delimiter = DollarDelimiter(script.Sql);
        sb.Append(" AS ").AppendLine(delimiter);
        sb.AppendLine(script.Sql);
        sb.Append(delimiter).AppendLine(";");
    }

    private static string DollarDelimiter(string body)
    {
        if (!body.Contains("$$", StringComparison.Ordinal))
        {
            return "$$";
        }
        for (var i = 1; ; i++)
        {
            var tag = $"$body{i.ToString(CultureInfo.InvariantCulture)}$";
            if (!body.Contains(tag, StringComparison.Ordinal))
            {
                return tag;
            }
        }
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

        foreach (var enumType in schema.Enums)
        {
            sb.AppendLine();
            WriteEnum(sb, schema.Name, enumType);
        }

        foreach (var sequence in schema.Sequences)
        {
            sb.AppendLine();
            WriteSequence(sb, schema.Name, sequence);
        }

        foreach (var function in schema.Functions)
        {
            sb.AppendLine();
            WriteFunction(sb, schema.Name, function);
        }

        foreach (var procedure in schema.Procedures)
        {
            sb.AppendLine();
            WriteProcedure(sb, schema.Name, procedure);
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

        foreach (var dropped in schema.DroppedEnums)
        {
            sb.Append("DROP ENUM ").Append(schema.Name).Append('.').Append(dropped).AppendLine(";");
        }

        foreach (var dropped in schema.DroppedSequences)
        {
            sb.Append("DROP SEQUENCE ").Append(schema.Name).Append('.').Append(dropped).AppendLine(";");
        }

        foreach (var dropped in schema.DroppedFunctions)
        {
            sb.Append("DROP FUNCTION ").Append(schema.Name).Append('.').Append(dropped).AppendLine(";");
        }

        foreach (var dropped in schema.DroppedProcedures)
        {
            sb.Append("DROP PROCEDURE ").Append(schema.Name).Append('.').Append(dropped).AppendLine(";");
        }
    }

    private static void WriteExtension(StringBuilder sb, Extension extension)
    {
        WriteDocComment(sb, extension.Comment, indent: "");
        sb.Append("CREATE EXTENSION ").Append(ExtensionName(extension.Name));
        if (extension.Version is { } version)
        {
            sb.Append(" VERSION '").Append(version.Replace("'", "''")).Append('\'');
        }
        sb.AppendLine(";");
    }

    /// <summary>
    /// Renders an extension name: bare when it is a valid identifier, otherwise single-quoted (e.g.
    /// <c>'uuid-ossp'</c>) so it round-trips through the parser.
    /// </summary>
    private static string ExtensionName(string name) =>
        IsBareIdentifier(name) ? name : $"'{name.Replace("'", "''")}'";

    private static bool IsBareIdentifier(string name) =>
        name.Length > 0
        && (char.IsAsciiLetter(name[0]) || name[0] == '_')
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    private static void WriteEnum(StringBuilder sb, string schemaName, EnumType enumType)
    {
        WriteDocComment(sb, enumType.Comment, indent: "");
        sb.Append("CREATE ENUM ").Append(schemaName).Append('.').Append(enumType.Name);
        if (enumType.OldName is { } oldName)
        {
            sb.Append(" RENAMED FROM ").Append(oldName);
        }
        sb.Append(" (").Append(string.Join(", ", enumType.Values.Select(v => $"'{v.Replace("'", "''")}'"))).AppendLine(");");
    }

    private static void WriteSequence(StringBuilder sb, string schemaName, Sequence sequence)
    {
        WriteDocComment(sb, sequence.Comment, indent: "");
        sb.Append("CREATE SEQUENCE ").Append(schemaName).Append('.').Append(sequence.Name);
        if (sequence.OldName is { } oldName)
        {
            sb.Append(" RENAMED FROM ").Append(oldName);
        }
        if (SequenceOptionsText(sequence.Options) is { } options)
        {
            sb.Append(" (").Append(options).Append(')');
        }
        sb.AppendLine(";");
    }

    private static string? SequenceOptionsText(SequenceOptions options)
    {
        var parts = new List<string>();
        if (options.DataType is { } type)
        {
            parts.Add($"AS {type}");
        }
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
        if (options.MaxValue is { } max)
        {
            parts.Add($"MAXVALUE {max}");
        }
        if (options.Cache is { } cache)
        {
            parts.Add($"CACHE {cache}");
        }
        if (options.Cycle)
        {
            parts.Add("CYCLE");
        }
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static void WriteFunction(StringBuilder sb, string schemaName, Function function)
    {
        WriteDocComment(sb, function.Comment, indent: "");
        sb.Append("CREATE FUNCTION ").Append(schemaName).Append('.').Append(function.Name);
        if (function.OldName is { } oldName)
        {
            sb.Append(" RENAMED FROM ").Append(oldName);
        }
        // The definition is emitted verbatim (multi-line bodies keep their newlines); TrimEnd guards a
        // code-built definition ending in whitespace so the ';' lands directly after the last character.
        sb.Append('(').Append(function.Arguments).Append(") ").Append(function.Definition.TrimEnd()).AppendLine(";");
    }

    private static void WriteProcedure(StringBuilder sb, string schemaName, Procedure procedure)
    {
        WriteDocComment(sb, procedure.Comment, indent: "");
        sb.Append("CREATE PROCEDURE ").Append(schemaName).Append('.').Append(procedure.Name);
        if (procedure.OldName is { } oldName)
        {
            sb.Append(" RENAMED FROM ").Append(oldName);
        }
        sb.Append('(').Append(procedure.Arguments).Append(") ").Append(procedure.Definition.TrimEnd()).AppendLine(";");
    }

    private static void WriteView(StringBuilder sb, string schemaName, View view)
    {
        WriteDocComment(sb, view.Comment, indent: "");
        sb.Append("CREATE ");
        if (view.IsMaterialized)
        {
            sb.Append("MATERIALIZED ");
        }
        sb.Append("VIEW ").Append(schemaName).Append('.').Append(view.Name);
        if (view.OldName is { } oldName)
        {
            sb.Append(" RENAMED FROM ").Append(oldName);
        }
        sb.Append(" AS ").Append(view.Body).AppendLine(";");

        // A materialized view's indexes are standalone statements emitted after it (a plain view has none).
        foreach (var index in view.Indexes)
        {
            WriteDocComment(sb, index.Comment, indent: "");
            sb.Append("CREATE ");
            if (index.IsUnique)
            {
                sb.Append("UNIQUE ");
            }
            sb.Append("INDEX ").Append(index.Name)
                .Append(" ON ").Append(schemaName).Append('.').Append(view.Name)
                .Append(" (").Append(Columns(index.ColumnNames)).Append(')');
            if (index.Predicate is { } predicate)
            {
                sb.Append(" WHERE (").Append(predicate).Append(')');
            }
            sb.AppendLine(";");
        }
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

        // Triggers are standalone statements (like grants), emitted after their table so the table exists when
        // they are read back.
        foreach (var trigger in table.Triggers)
        {
            WriteTrigger(sb, schemaName, table.Name, trigger);
        }
    }

    private static void WriteTrigger(StringBuilder sb, string schemaName, string tableName, Trigger trigger)
    {
        WriteDocComment(sb, trigger.Comment, indent: "");
        sb.Append("CREATE TRIGGER ").Append(trigger.Name).Append(' ').Append(TriggerTimingText(trigger.Timing))
            .Append(' ').Append(TriggerEventsText(trigger))
            .Append(" ON ").Append(schemaName).Append('.').Append(tableName)
            .Append(" FOR EACH ").Append(trigger.Level == TriggerLevel.Row ? "ROW" : "STATEMENT");
        if (trigger.When is { } when)
        {
            sb.Append(" WHEN (").Append(when).Append(')');
        }
        sb.Append(" EXECUTE FUNCTION ").Append(trigger.Function)
            .Append('(').Append(trigger.FunctionArguments ?? string.Empty).Append(')').AppendLine(";");
    }

    private static string TriggerTimingText(TriggerTiming timing) => timing switch
    {
        TriggerTiming.Before => "BEFORE",
        TriggerTiming.After => "AFTER",
        TriggerTiming.InsteadOf => "INSTEAD OF",
        _ => throw new ArgumentOutOfRangeException(nameof(timing), timing, "Unknown trigger timing."),
    };

    private static string TriggerEventsText(Trigger trigger)
    {
        var parts = new List<string>(4);
        if (trigger.Events.HasFlag(TriggerEvent.Insert))
        {
            parts.Add("INSERT");
        }
        if (trigger.Events.HasFlag(TriggerEvent.Update))
        {
            parts.Add(trigger.UpdateOfColumns.Count > 0 ? $"UPDATE OF ({Columns(trigger.UpdateOfColumns)})" : "UPDATE");
        }
        if (trigger.Events.HasFlag(TriggerEvent.Delete))
        {
            parts.Add("DELETE");
        }
        if (trigger.Events.HasFlag(TriggerEvent.Truncate))
        {
            parts.Add("TRUNCATE");
        }
        return string.Join(" OR ", parts);
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
