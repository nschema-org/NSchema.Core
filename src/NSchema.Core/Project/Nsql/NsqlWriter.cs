using System.Globalization;
using System.Text;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;
using NSchema.Project.Nsql.Syntax;
using Syn = NSchema.Project.Nsql.Syntax;

namespace NSchema.Project.Nsql;

/// <summary>
/// Renders syntax documents as canonical NSchema source.
/// </summary>
public static class NsqlWriter
{
    /// <summary>
    /// Writes <paramref name="schema"/> as canonical NSchema source.
    /// </summary>
    /// <param name="schema">The schema to write.</param>
    /// <param name="declareSchemas">Whether to emit a <c>CREATE SCHEMA</c> statement for each schema; pass <c>false</c> to write only the member objects (the reader vivifies the schema from their qualified names).</param>
    public static string Write(DatabaseSchema schema, bool declareSchemas = true) =>
        Write(SyntaxBuilder.Build(schema, [], declareSchemas));

    /// <summary>
    /// Writes a schema and its scripts as canonical NSchema source.
    /// </summary>
    /// <param name="schema">The schema to write.</param>
    /// <param name="scripts">The scripts to write after the schema.</param>
    public static string Write(DatabaseSchema schema, IReadOnlyList<Script> scripts) =>
        Write(SyntaxBuilder.Build(schema, scripts));

    /// <summary>
    /// Renders a syntax document as canonical NSchema source.
    /// </summary>
    /// <param name="document">The document to render.</param>
    public static string Write(NsqlDocument document)
    {
        var sb = new StringBuilder();
        var first = true;
        foreach (var statement in document.Statements)
        {
            // A statement attached to its subject (a grant, a trigger, a standalone index, an object-level
            // drop) renders directly under it; everything else is separated by a blank line.
            if (!first && !Attached(statement))
            {
                sb.AppendLine();
            }
            first = false;
            WriteStatement(sb, statement);
        }
        return sb.ToString();
    }

    private static bool Attached(NsqlStatement statement) => statement
        is Syn.Schemas.GrantSchemaUsageStatement
        or Syn.Tables.GrantTableStatement
        or Syn.Triggers.CreateTriggerStatement
        or Syn.Indexes.CreateIndexStatement
        or Syn.Tables.DropTableStatement
        or Syn.Views.DropViewStatement
        or Syn.Enums.DropEnumStatement
        or Syn.Domains.DropDomainStatement
        or Syn.CompositeTypes.DropCompositeTypeStatement
        or Syn.Sequences.DropSequenceStatement
        or Syn.Routines.DropRoutineStatement;

    private static void WriteStatement(StringBuilder sb, NsqlStatement statement)
    {
        switch (statement)
        {
            case Syn.Schemas.CreateSchemaStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append("CREATE ");
                if (s.IsPartial)
                {
                    sb.Append("PARTIAL ");
                }
                sb.Append("SCHEMA ").Append(s.Name.Text);
                AppendRenamedFrom(sb, s.RenamedFrom);
                sb.AppendLine(";");
                break;
            case Syn.Schemas.GrantSchemaUsageStatement s:
                sb.Append("GRANT USAGE ON SCHEMA ").Append(s.Schema.Text).Append(" TO ").Append(s.Role.Text).AppendLine(";");
                break;
            case Syn.Enums.CreateEnumStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append("CREATE ENUM ").Append(Qualified(s.Name));
                AppendRenamedFrom(sb, s.RenamedFrom);
                sb.Append(" (").Append(string.Join(", ", s.Values.Select(v => $"'{v.Replace("'", "''")}'"))).AppendLine(");");
                break;
            case Syn.Domains.CreateDomainStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append("CREATE DOMAIN ").Append(Qualified(s.Name));
                AppendRenamedFrom(sb, s.RenamedFrom);
                sb.Append(" AS ").Append(TypeText(s.Type));
                if (s.NotNull)
                {
                    sb.Append(" NOT NULL");
                }
                foreach (var check in s.Checks)
                {
                    sb.Append(" CONSTRAINT ").Append(check.Name.Text).Append(" CHECK (").Append(check.Expression.Value).Append(')');
                }
                // The default, if any, comes last: its opaque expression is read back up to the terminating ';'.
                if (s.Default is { } @default)
                {
                    sb.Append(" DEFAULT ").Append(@default.Value);
                }
                sb.AppendLine(";");
                break;
            case Syn.CompositeTypes.CreateCompositeTypeStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append("CREATE TYPE ").Append(Qualified(s.Name));
                AppendRenamedFrom(sb, s.RenamedFrom);
                sb.Append(" AS (").Append(string.Join(", ", s.Fields.Select(f => $"{f.Name.Text} {TypeText(f.Type)}"))).AppendLine(");");
                break;
            case Syn.Sequences.CreateSequenceStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append("CREATE SEQUENCE ").Append(Qualified(s.Name));
                AppendRenamedFrom(sb, s.RenamedFrom);
                if (s.Options is { } options && SequenceOptionsText(options) is { } text)
                {
                    sb.Append(" (").Append(text).Append(')');
                }
                sb.AppendLine(";");
                break;
            case Syn.Routines.CreateRoutineStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append(s.Kind == Syn.Routines.RoutineKind.Procedure ? "CREATE PROCEDURE " : "CREATE FUNCTION ").Append(Qualified(s.Name));
                AppendRenamedFrom(sb, s.RenamedFrom);
                // The definition is emitted verbatim (multi-line bodies keep their newlines); TrimEnd guards a
                // code-built definition ending in whitespace so the ';' lands directly after the last character.
                sb.Append('(').Append(s.Arguments.Value).Append(") ").Append(s.Definition.Value.TrimEnd()).AppendLine(";");
                break;
            case Syn.Tables.CreateTableStatement s:
                WriteTable(sb, s);
                break;
            case Syn.Tables.GrantTableStatement s:
                sb.Append("GRANT ").Append(PrivilegesText(s.Privileges))
                    .Append(" ON ").Append(Qualified(s.On))
                    .Append(" TO ").Append(s.Role.Text).AppendLine(";");
                break;
            case Syn.Triggers.CreateTriggerStatement s:
                WriteTrigger(sb, s);
                break;
            case Syn.Views.CreateViewStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append("CREATE ");
                if (s.IsMaterialized)
                {
                    sb.Append("MATERIALIZED ");
                }
                sb.Append("VIEW ").Append(Qualified(s.Name));
                AppendRenamedFrom(sb, s.RenamedFrom);
                sb.Append(" AS ").Append(s.Body.Value).AppendLine(";");
                break;
            case Syn.Indexes.CreateIndexStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append("CREATE ");
                if (s.IsUnique)
                {
                    sb.Append("UNIQUE ");
                }
                sb.Append("INDEX ").Append(s.Name.Text).Append(" ON ").Append(Qualified(s.On));
                AppendIndexTail(sb, s.Method, s.Columns, s.Include, s.Predicate);
                sb.AppendLine(";");
                break;
            case Syn.Extensions.CreateExtensionStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append("CREATE EXTENSION ").Append(ExtensionName(s.Name.Text));
                if (s.Version is { } version)
                {
                    sb.Append(" VERSION '").Append(version.Replace("'", "''")).Append('\'');
                }
                sb.AppendLine(";");
                break;
            case Syn.Scripts.ScriptStatement s:
                WriteScript(sb, s);
                break;
            case Syn.Schemas.DropSchemaStatement s:
                sb.Append("DROP SCHEMA ").Append(s.Name.Text).AppendLine(";");
                break;
            case Syn.Extensions.DropExtensionStatement s:
                sb.Append("DROP EXTENSION ").Append(ExtensionName(s.Name.Text)).AppendLine(";");
                break;
            case Syn.Tables.DropTableStatement s:
                sb.Append("DROP TABLE ").Append(Qualified(s.Name)).AppendLine(";");
                break;
            case Syn.Views.DropViewStatement s:
                sb.Append("DROP VIEW ").Append(Qualified(s.Name)).AppendLine(";");
                break;
            case Syn.Enums.DropEnumStatement s:
                sb.Append("DROP ENUM ").Append(Qualified(s.Name)).AppendLine(";");
                break;
            case Syn.Domains.DropDomainStatement s:
                sb.Append("DROP DOMAIN ").Append(Qualified(s.Name)).AppendLine(";");
                break;
            case Syn.CompositeTypes.DropCompositeTypeStatement s:
                sb.Append("DROP TYPE ").Append(Qualified(s.Name)).AppendLine(";");
                break;
            case Syn.Sequences.DropSequenceStatement s:
                sb.Append("DROP SEQUENCE ").Append(Qualified(s.Name)).AppendLine(";");
                break;
            case Syn.Routines.DropRoutineStatement s:
                sb.Append("DROP ROUTINE ").Append(Qualified(s.Name)).AppendLine(";");
                break;
            default:
                throw new NotSupportedException($"Statement '{statement.GetType().Name}' is not rendered.");
        }
    }

    private static void WriteTable(StringBuilder sb, Syn.Tables.CreateTableStatement statement)
    {
        WriteDocComment(sb, statement.Doc, indent: "");
        sb.Append("CREATE TABLE ").Append(Qualified(statement.Name));
        AppendRenamedFrom(sb, statement.RenamedFrom);
        sb.AppendLine(" (");

        for (var i = 0; i < statement.Members.Count; i++)
        {
            var member = statement.Members[i];
            WriteDocComment(sb, member.Doc, indent: "  ");
            sb.Append("  ").Append(MemberText(member)).AppendLine(i < statement.Members.Count - 1 ? "," : string.Empty);
        }
        sb.AppendLine(");");
    }

    private static string MemberText(Syn.Tables.TableMember member)
    {
        switch (member)
        {
            case Syn.Tables.ColumnDefinition m:
            {
                var sb = new StringBuilder();
                sb.Append(m.Name.Text).Append(' ').Append(TypeText(m.Type));
                if (!m.IsNullable)
                {
                    sb.Append(" NOT NULL");
                }
                if (m.IsIdentity)
                {
                    sb.Append(" IDENTITY");
                    if (m.IdentityOptions is { } options && IdentityOptionsText(options) is { } text)
                    {
                        sb.Append(" (").Append(text).Append(')');
                    }
                }
                if (m.Default is { } @default)
                {
                    sb.Append(" DEFAULT ").Append(@default.Value);
                }
                if (m.Generated is { } generated)
                {
                    sb.Append(" GENERATED ALWAYS AS (").Append(generated.Value).Append(") STORED");
                }
                if (m.RenamedFrom is { } oldName)
                {
                    sb.Append(" RENAMED FROM ").Append(oldName.Text);
                }
                return sb.ToString();
            }
            case Syn.Constraints.PrimaryKeyDefinition m:
                return $"CONSTRAINT {m.Name.Text} PRIMARY KEY ({ColumnsText(m.Columns)})";
            case Syn.Constraints.ForeignKeyDefinition m:
            {
                var sb = new StringBuilder();
                sb.Append("CONSTRAINT ").Append(m.Name.Text)
                    .Append(" FOREIGN KEY (").Append(ColumnsText(m.Columns)).Append(')')
                    .Append(" REFERENCES ").Append(Qualified(m.References))
                    .Append(" (").Append(ColumnsText(m.ReferencedColumns)).Append(')');
                if (m.OnDelete != Syn.Constraints.ReferentialAction.NoAction)
                {
                    sb.Append(" ON DELETE ").Append(ActionText(m.OnDelete));
                }
                if (m.OnUpdate != Syn.Constraints.ReferentialAction.NoAction)
                {
                    sb.Append(" ON UPDATE ").Append(ActionText(m.OnUpdate));
                }
                return sb.ToString();
            }
            case Syn.Constraints.UniqueDefinition m:
                return $"CONSTRAINT {m.Name.Text} UNIQUE ({ColumnsText(m.Columns)})";
            case Syn.Constraints.CheckDefinition m:
                return $"CONSTRAINT {m.Name.Text} CHECK ({m.Expression.Value})";
            case Syn.Constraints.ExclusionDefinition m:
            {
                var sb = new StringBuilder();
                sb.Append("CONSTRAINT ").Append(m.Name.Text).Append(" EXCLUDE");
                if (m.Method is { } method)
                {
                    sb.Append(" USING ").Append(method.Text);
                }
                sb.Append(" (").Append(string.Join(", ", m.Elements.Select(ExclusionElementText))).Append(')');
                if (m.Predicate is { } predicate)
                {
                    sb.Append(" WHERE (").Append(predicate.Value).Append(')');
                }
                return sb.ToString();
            }
            case Syn.Indexes.IndexDefinition m:
            {
                var sb = new StringBuilder();
                if (m.IsUnique)
                {
                    sb.Append("UNIQUE ");
                }
                sb.Append("INDEX ").Append(m.Name.Text);
                AppendIndexTail(sb, m.Method, m.Columns, m.Include, m.Predicate);
                return sb.ToString();
            }
            default:
                throw new NotSupportedException($"Table member '{member.GetType().Name}' is not rendered.");
        }
    }

    private static void WriteTrigger(StringBuilder sb, Syn.Triggers.CreateTriggerStatement statement)
    {
        WriteDocComment(sb, statement.Doc, indent: "");
        sb.Append("CREATE TRIGGER ").Append(statement.Name.Text).Append(' ').Append(TimingText(statement.Timing))
            .Append(' ').Append(EventsText(statement))
            .Append(" ON ").Append(Qualified(statement.On));

        // An inline-body trigger (SQL Server) carries its action in a dollar-quoted block; it has no FOR EACH /
        // WHEN / function clauses. A function trigger (PostgreSQL) keeps those.
        if (statement.Action is Syn.Triggers.InlineBodyAction inline)
        {
            var delimiter = DollarDelimiter(inline.Body);
            sb.Append(" AS ").AppendLine(delimiter);
            sb.AppendLine(inline.Body.Value);
            sb.Append(delimiter).AppendLine(";");
            return;
        }

        var action = (Syn.Triggers.ExecuteFunctionAction)statement.Action;
        sb.Append(" FOR EACH ").Append(statement.Level == Syn.Triggers.TriggerLevel.Row ? "ROW" : "STATEMENT");
        if (statement.When is { } when)
        {
            sb.Append(" WHEN (").Append(when.Value).Append(')');
        }
        sb.Append(" EXECUTE FUNCTION ").Append(Reference(action.Function))
            .Append('(').Append(action.Arguments.Value).Append(')').AppendLine(";");
    }

    private static void WriteScript(StringBuilder sb, Syn.Scripts.ScriptStatement statement)
    {
        sb.Append("SCRIPT '").Append(statement.Name.Replace("'", "''")).Append("' RUN");
        if (statement.RunCondition == Syn.Scripts.RunCondition.Once)
        {
            sb.Append(" ONCE");
        }
        sb.Append(" ON ").Append(EventText(statement.Event));
        if (statement.RunOutsideTransaction)
        {
            sb.Append(" (run_outside_transaction = true)");
        }

        // Emit the body in a dollar-quoted block, choosing a tag that doesn't occur in the body so it is
        // taken verbatim. The reader strips the delimiters and trims surrounding whitespace, so a body
        // stored without its delimiters round-trips back to the same text.
        var delimiter = DollarDelimiter(statement.Body);
        sb.Append(" AS ").AppendLine(delimiter);
        sb.AppendLine(statement.Body.Value);
        sb.Append(delimiter).AppendLine(";");
    }

    private static string EventText(Syn.Scripts.ScriptEventClause clause) => clause switch
    {
        Syn.Scripts.DeploymentEventClause deployment =>
            deployment.Phase == Syn.Scripts.DeploymentPhase.Pre ? "PRE DEPLOYMENT" : "POST DEPLOYMENT",
        Syn.Scripts.ChangeEventClause change => $"{TriggerText(change.Trigger)} {PathText(change.Path)}",
        _ => throw new NotSupportedException($"Script event '{clause.GetType().Name}' is not rendered."),
    };

    private static string TriggerText(Syn.Scripts.ChangeTrigger trigger) => trigger switch
    {
        Syn.Scripts.ChangeTrigger.AddColumn => "ADD COLUMN",
        Syn.Scripts.ChangeTrigger.AlterColumnType => "ALTER COLUMN TYPE",
        _ => "ADD CONSTRAINT",
    };

    private static string PathText(MemberPath path) =>
        path.Schema is { } schema ? $"{schema.Text}.{path.Table.Text}.{path.Member.Text}" : $"{path.Table.Text}.{path.Member.Text}";

    // --- clause helpers ----------------------------------------------------------------

    private static void AppendRenamedFrom(StringBuilder sb, Identifier? oldName)
    {
        if (oldName is not null)
        {
            sb.Append(" RENAMED FROM ").Append(oldName.Text);
        }
    }

    private static void AppendIndexTail(StringBuilder sb, Identifier? method, IReadOnlyList<Syn.Indexes.IndexElement> columns,
        IReadOnlyList<Identifier>? include, SqlText? predicate)
    {
        if (method is not null)
        {
            sb.Append(" USING ").Append(method.Text);
        }
        sb.Append(" (").Append(string.Join(", ", columns.Select(IndexKeyText))).Append(')');
        if (include is { Count: > 0 })
        {
            sb.Append(" INCLUDE (").Append(ColumnsText(include)).Append(')');
        }
        if (predicate is { } p)
        {
            sb.Append(" WHERE (").Append(p.Value).Append(')');
        }
    }

    private static string IndexKeyText(Syn.Indexes.IndexElement element)
    {
        var sb = new StringBuilder();
        sb.Append(element.Column is { } name ? name.Text : $"({element.Expression!.Value})");
        sb.Append(element.Sort switch
        {
            Syn.Indexes.IndexSort.Ascending => " ASC",
            Syn.Indexes.IndexSort.Descending => " DESC",
            _ => string.Empty,
        });
        sb.Append(element.Nulls switch
        {
            Syn.Indexes.IndexNulls.First => " NULLS FIRST",
            Syn.Indexes.IndexNulls.Last => " NULLS LAST",
            _ => string.Empty,
        });
        return sb.ToString();
    }

    private static string ExclusionElementText(Syn.Constraints.ExclusionElement element) =>
        $"{(element.Column is { } column ? column.Text : $"({element.Expression!.Value})")} WITH {element.Operator}";

    private static string? SequenceOptionsText(Syn.Sequences.SequenceOptionsClause options)
    {
        var parts = new List<string>();
        if (options.As is { } type)
        {
            parts.Add($"AS {TypeText(type)}");
        }
        if (options.Start is { } start)
        {
            parts.Add($"START {start}");
        }
        if (options.Increment is { } increment)
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

    private static string? IdentityOptionsText(Syn.Tables.IdentityOptionsClause options)
    {
        var parts = new List<string>();
        if (options.Start is { } start)
        {
            parts.Add($"START {start}");
        }
        if (options.Increment is { } increment)
        {
            parts.Add($"INCREMENT {increment}");
        }
        if (options.MinValue is { } min)
        {
            parts.Add($"MINVALUE {min}");
        }
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string TimingText(Syn.Triggers.TriggerTiming timing) => timing switch
    {
        Syn.Triggers.TriggerTiming.Before => "BEFORE",
        Syn.Triggers.TriggerTiming.After => "AFTER",
        _ => "INSTEAD OF",
    };

    private static string EventsText(Syn.Triggers.CreateTriggerStatement statement)
    {
        var parts = new List<string>(4);
        if (statement.Events.HasFlag(Syn.Triggers.TriggerEvent.Insert))
        {
            parts.Add("INSERT");
        }
        if (statement.Events.HasFlag(Syn.Triggers.TriggerEvent.Update))
        {
            parts.Add(statement.UpdateOfColumns is { Count: > 0 } columns ? $"UPDATE OF ({ColumnsText(columns)})" : "UPDATE");
        }
        if (statement.Events.HasFlag(Syn.Triggers.TriggerEvent.Delete))
        {
            parts.Add("DELETE");
        }
        if (statement.Events.HasFlag(Syn.Triggers.TriggerEvent.Truncate))
        {
            parts.Add("TRUNCATE");
        }
        return string.Join(" OR ", parts);
    }

    private static string ActionText(Syn.Constraints.ReferentialAction action) => action switch
    {
        Syn.Constraints.ReferentialAction.Cascade => "CASCADE",
        Syn.Constraints.ReferentialAction.SetNull => "SET NULL",
        Syn.Constraints.ReferentialAction.SetDefault => "SET DEFAULT",
        _ => "NO ACTION",
    };

    private static string PrivilegesText(Syn.Tables.TablePrivilege privileges)
    {
        var parts = new List<string>();
        if (privileges.HasFlag(Syn.Tables.TablePrivilege.Select))
        {
            parts.Add("SELECT");
        }
        if (privileges.HasFlag(Syn.Tables.TablePrivilege.Insert))
        {
            parts.Add("INSERT");
        }
        if (privileges.HasFlag(Syn.Tables.TablePrivilege.Update))
        {
            parts.Add("UPDATE");
        }
        if (privileges.HasFlag(Syn.Tables.TablePrivilege.Delete))
        {
            parts.Add("DELETE");
        }
        return string.Join(", ", parts);
    }

    private static string Qualified(QualifiedName name) =>
        name.Schema is { } schema ? $"{schema.Text}.{name.Name.Text}" : name.Name.Text;

    private static string Reference(QualifiedName name) => Qualified(name);

    private static string TypeText(TypeName type)
    {
        var text = type.Schema is { } schema ? $"{schema.Text}.{type.Name.Text}" : type.Name.Text;
        return type.Arguments is { } arguments ? $"{text}({arguments})" : text;
    }

    private static string ColumnsText(IReadOnlyList<Identifier> columns) => string.Join(", ", columns.Select(c => c.Text));

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
