using System.Globalization;
using System.Text;
using NSchema.Project.Ddl.Models;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.CompositeTypes;
using NSchema.Project.Domain.Models.Constraints;
using NSchema.Project.Domain.Models.Domains;
using NSchema.Project.Domain.Models.Enums;
using NSchema.Project.Domain.Models.Extensions;
using NSchema.Project.Domain.Models.Indexes;
using NSchema.Project.Domain.Models.Routines;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Scripts;
using NSchema.Project.Domain.Models.Sequences;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Triggers;
using NSchema.Project.Domain.Models.Views;

namespace NSchema.Project.Ddl;

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
    /// <param name="declareSchemas">Whether to emit a <c>CREATE SCHEMA</c> statement for each schema; pass <c>false</c> to write only the member objects (the reader vivifies the schema from their qualified names).</param>
    /// <returns>The canonical NSchema DDL for <paramref name="schema"/>.</returns>
    public string Write(DatabaseSchema schema, bool declareSchemas = true) => Write(new DdlDocument(schema, []), declareSchemas);

    /// <summary>
    /// Writes a full <see cref="DdlDocument"/> as canonical NSchema DDL.
    /// </summary>
    /// <param name="document">The document to write.</param>
    /// <returns>The canonical NSchema DDL for <paramref name="document"/>.</returns>
    public string Write(DdlDocument document) => Write(document, declareSchemas: true);

    private static string Write(DdlDocument document, bool declareSchemas)
    {
        var sb = new StringBuilder();
        var first = true;


        // Extensions are database-global and are created first, so they precede the schemas.
        foreach (var extension in document.Schema.Extensions)
        {
            Separate(sb, ref first);
            WriteExtension(sb, extension);
        }

        foreach (var definition in document.Schema.Schemas)
        {
            Separate(sb, ref first);
            WriteSchema(sb, definition, declareSchemas);
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
            sb.Append("DROP EXTENSION ").Append(ExtensionName(dropped.Value)).AppendLine(";");
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



    private static void WriteScript(StringBuilder sb, Script script)
    {
        sb.Append("SCRIPT '").Append(script.Name.Value.Replace("'", "''")).Append("' RUN");
        if (script.RunCondition == RunCondition.Once)
        {
            sb.Append(" ONCE");
        }
        sb.Append(" ON ").Append(script.Event.Description);
        WriteScriptTail(sb, script.RunOutsideTransaction, script.Sql);
    }

    private static void WriteScriptTail(StringBuilder sb, bool runOutsideTransaction, SqlText sql)
    {
        if (runOutsideTransaction)
        {
            sb.Append(" (run_outside_transaction = true)");
        }

        // Emit the body in a dollar-quoted block, choosing a tag that doesn't occur in the body so it is
        // taken verbatim. The reader strips the delimiters and trims surrounding whitespace, so a body
        // stored without its delimiters round-trips back to the same text.
        var delimiter = DollarDelimiter(sql);
        sb.Append(" AS ").AppendLine(delimiter);
        sb.AppendLine(sql.Value);
        sb.Append(delimiter).AppendLine(";");
    }

    private static string DollarDelimiter(SqlText body)
    {
        if (!body.Value.Contains("$$", StringComparison.Ordinal))
        {
            return "$$";
        }
        for (var i = 1; ; i++)
        {
            var tag = $"$body{i.ToString(CultureInfo.InvariantCulture)}$";
            if (!body.Value.Contains(tag, StringComparison.Ordinal))
            {
                return tag;
            }
        }
    }

    private static void WriteSchema(StringBuilder sb, SchemaDefinition schema, bool declare)
    {
        // With the declaration suppressed the first member opens the output, so it takes no leading separator.
        var first = !declare;
        if (declare)
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
        }

        foreach (var enumType in schema.Enums)
        {
            Separate(sb, ref first);
            WriteEnum(sb, schema.Name, enumType);
        }

        foreach (var domain in schema.Domains)
        {
            Separate(sb, ref first);
            WriteDomain(sb, schema.Name, domain);
        }

        foreach (var compositeType in schema.CompositeTypes)
        {
            Separate(sb, ref first);
            WriteCompositeType(sb, schema.Name, compositeType);
        }

        foreach (var sequence in schema.Sequences)
        {
            Separate(sb, ref first);
            WriteSequence(sb, schema.Name, sequence);
        }

        foreach (var routine in schema.Routines)
        {
            Separate(sb, ref first);
            WriteRoutine(sb, schema.Name, routine);
        }

        foreach (var table in schema.Tables)
        {
            Separate(sb, ref first);
            WriteTable(sb, schema.Name, table);
        }

        foreach (var view in schema.Views)
        {
            Separate(sb, ref first);
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

        foreach (var dropped in schema.DroppedDomains)
        {
            sb.Append("DROP DOMAIN ").Append(schema.Name).Append('.').Append(dropped).AppendLine(";");
        }

        foreach (var dropped in schema.DroppedCompositeTypes)
        {
            sb.Append("DROP TYPE ").Append(schema.Name).Append('.').Append(dropped).AppendLine(";");
        }

        foreach (var dropped in schema.DroppedSequences)
        {
            sb.Append("DROP SEQUENCE ").Append(schema.Name).Append('.').Append(dropped).AppendLine(";");
        }

        // A dropped routine is recorded by name only (functions and procedures share one name space), so it is
        // emitted with the kind-agnostic DROP ROUTINE.
        foreach (var dropped in schema.DroppedRoutines)
        {
            sb.Append("DROP ROUTINE ").Append(schema.Name).Append('.').Append(dropped).AppendLine(";");
        }
    }

    private static void WriteExtension(StringBuilder sb, Extension extension)
    {
        WriteDocComment(sb, extension.Comment, indent: "");
        sb.Append("CREATE EXTENSION ").Append(ExtensionName(extension.Name.Value));
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

    private static void WriteEnum(StringBuilder sb, SqlIdentifier schemaName, EnumType enumType)
    {
        WriteDocComment(sb, enumType.Comment, indent: "");
        sb.Append("CREATE ENUM ").Append(schemaName).Append('.').Append(enumType.Name);
        if (enumType.OldName is { } oldName)
        {
            sb.Append(" RENAMED FROM ").Append(oldName);
        }
        sb.Append(" (").Append(string.Join(", ", enumType.Values.Select(v => $"'{v.Replace("'", "''")}'"))).AppendLine(");");
    }

    private static void WriteDomain(StringBuilder sb, SqlIdentifier schemaName, DomainDefinition domain)
    {
        WriteDocComment(sb, domain.Comment, indent: "");
        sb.Append("CREATE DOMAIN ").Append(schemaName).Append('.').Append(domain.Name);
        if (domain.OldName is { } oldName)
        {
            sb.Append(" RENAMED FROM ").Append(oldName);
        }
        sb.Append(" AS ").Append(domain.DataType);
        if (domain.NotNull)
        {
            sb.Append(" NOT NULL");
        }
        foreach (var check in domain.Checks)
        {
            sb.Append(" CONSTRAINT ").Append(check.Name).Append(" CHECK (").Append(check.Expression).Append(')');
        }
        // The default, if any, comes last: its opaque expression is read back up to the terminating ';'.
        if (domain.Default is { } @default)
        {
            sb.Append(" DEFAULT ").Append(@default);
        }
        sb.AppendLine(";");
    }

    private static void WriteCompositeType(StringBuilder sb, SqlIdentifier schemaName, CompositeType compositeType)
    {
        WriteDocComment(sb, compositeType.Comment, indent: "");
        sb.Append("CREATE TYPE ").Append(schemaName).Append('.').Append(compositeType.Name);
        if (compositeType.OldName is { } oldName)
        {
            sb.Append(" RENAMED FROM ").Append(oldName);
        }
        sb.Append(" AS (").Append(string.Join(", ", compositeType.Fields.Select(f => $"{f.Name} {f.DataType}"))).AppendLine(");");
    }

    private static void WriteSequence(StringBuilder sb, SqlIdentifier schemaName, Sequence sequence)
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

    private static void WriteRoutine(StringBuilder sb, SqlIdentifier schemaName, Routine routine)
    {
        WriteDocComment(sb, routine.Comment, indent: "");
        sb.Append(routine.Kind == RoutineKind.Procedure ? "CREATE PROCEDURE " : "CREATE FUNCTION ")
            .Append(schemaName).Append('.').Append(routine.Name);
        if (routine.OldName is { } oldName)
        {
            sb.Append(" RENAMED FROM ").Append(oldName);
        }
        // The definition is emitted verbatim (multi-line bodies keep their newlines); TrimEnd guards a
        // code-built definition ending in whitespace so the ';' lands directly after the last character.
        sb.Append('(').Append(routine.Arguments.Value).Append(") ").Append(routine.Definition.Value.TrimEnd()).AppendLine(";");
    }

    private static void WriteView(StringBuilder sb, SqlIdentifier schemaName, View view)
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
                .Append(" ON ").Append(schemaName).Append('.').Append(view.Name);
            if (index.Method is { } method)
            {
                sb.Append(" USING ").Append(method);
            }
            sb.Append(" (").Append(IndexKeys(index.Columns)).Append(')').Append(IncludeClause(index.Include));
            if (index.Predicate is { } predicate)
            {
                sb.Append(" WHERE (").Append(predicate).Append(')');
            }
            sb.AppendLine(";");
        }
    }

    private static void WriteTable(StringBuilder sb, SqlIdentifier schemaName, Table table)
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
        foreach (var exclusion in table.ExclusionConstraints)
        {
            members.Add((exclusion.Comment, ExclusionText(exclusion)));
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

    private static void WriteTrigger(StringBuilder sb, SqlIdentifier schemaName, SqlIdentifier tableName, Trigger trigger)
    {
        WriteDocComment(sb, trigger.Comment, indent: "");
        sb.Append("CREATE TRIGGER ").Append(trigger.Name).Append(' ').Append(TriggerTimingText(trigger.Timing))
            .Append(' ').Append(TriggerEventsText(trigger))
            .Append(" ON ").Append(schemaName).Append('.').Append(tableName);

        // An inline-body trigger (SQL Server) carries its action in a dollar-quoted block; it has no FOR EACH / WHEN /
        // function clauses. A function trigger (PostgreSQL) keeps those.
        if (trigger.Body is { } body)
        {
            var delimiter = DollarDelimiter(body);
            sb.Append(" AS ").AppendLine(delimiter);
            sb.AppendLine(body.Value);
            sb.Append(delimiter).AppendLine(";");
            return;
        }

        sb.Append(" FOR EACH ").Append(trigger.Level == TriggerLevel.Row ? "ROW" : "STATEMENT");
        if (trigger.When is { } when)
        {
            sb.Append(" WHEN (").Append(when).Append(')');
        }
        sb.Append(" EXECUTE FUNCTION ").Append(trigger.Function)
            .Append('(').Append(trigger.FunctionArguments?.Value ?? string.Empty).Append(')').AppendLine(";");
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
        if (column.GeneratedExpression is { } generated)
        {
            sb.Append(" GENERATED ALWAYS AS (").Append(generated).Append(") STORED");
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

    private static string ExclusionText(ExclusionConstraint exclusion)
    {
        var sb = new StringBuilder();
        sb.Append("CONSTRAINT ").Append(exclusion.Name).Append(" EXCLUDE");
        if (exclusion.Method is { } method)
        {
            sb.Append(" USING ").Append(method);
        }
        sb.Append(" (").Append(string.Join(", ", exclusion.Elements.Select(ExclusionElementText))).Append(')');
        if (exclusion.Predicate is { } predicate)
        {
            sb.Append(" WHERE (").Append(predicate).Append(')');
        }
        return sb.ToString();
    }

    private static string ExclusionElementText(ExclusionElement element) =>
        $"{(element.Column is { } column ? column.Value : $"({element.Expression})")} WITH {element.Operator}";

    private static string IndexText(TableIndex index)
    {
        var sb = new StringBuilder();
        if (index.IsUnique)
        {
            sb.Append("UNIQUE ");
        }
        sb.Append("INDEX ").Append(index.Name);
        if (index.Method is { } method)
        {
            sb.Append(" USING ").Append(method);
        }
        sb.Append(" (").Append(IndexKeys(index.Columns)).Append(')').Append(IncludeClause(index.Include));
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

    private static string Columns(IReadOnlyList<SqlIdentifier> columns) => string.Join(", ", columns);

    /// <summary>Renders an index's key list: each column or parenthesised expression with optional sort/null ordering.</summary>
    private static string IndexKeys(IReadOnlyList<IndexColumn> columns) => string.Join(", ", columns.Select(IndexKey));

    private static string IndexKey(IndexColumn column)
    {
        var sb = new StringBuilder();
        sb.Append(column.Column is { } name ? name.Value : $"({column.Expression})");
        sb.Append(column.Sort switch
        {
            IndexSort.Ascending => " ASC",
            IndexSort.Descending => " DESC",
            _ => string.Empty,
        });
        sb.Append(column.Nulls switch
        {
            IndexNulls.First => " NULLS FIRST",
            IndexNulls.Last => " NULLS LAST",
            _ => string.Empty,
        });
        return sb.ToString();
    }

    private static string IncludeClause(IReadOnlyList<SqlIdentifier> include) =>
        include.Count > 0 ? $" INCLUDE ({Columns(include)})" : string.Empty;

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
