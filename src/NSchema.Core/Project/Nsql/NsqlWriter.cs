using System.Globalization;
using System.Text;
using NSchema.Model;
using NSchema.Project.Model.Directives;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Tokens;
using Syn = NSchema.Project.Nsql.Syntax;

namespace NSchema.Project.Nsql;

/// <summary>
/// Renders syntax documents as canonical NSchema source.
/// </summary>
public static class NsqlWriter
{
    /// <summary>
    /// Writes <paramref name="database"/> as canonical NSchema source.
    /// </summary>
    /// <param name="database">The schema to write.</param>
    public static string Write(Database database) => Write(SyntaxBuilder.Build(database));

    /// <summary>
    /// Writes a whole project as canonical NSchema source.
    /// </summary>
    /// <param name="database">The database to write.</param>
    /// <param name="directives">The directives to write after the schema.</param>
    public static string Write(Database database, ProjectDirectives directives) => Write(SyntaxBuilder.Build(database, directives));

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
            // A statement attached to its subject (a grant, a trigger, a standalone index, a rename)
            // renders directly under it; everything else is separated by a blank line.
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
        or RenameObjectStatement
        or Syn.Schemas.RenameSchemaStatement
        or Syn.Tables.RenameColumnStatement;

    private static void WriteStatement(StringBuilder sb, NsqlStatement statement)
    {
        switch (statement)
        {
            case Syn.Schemas.CreateSchemaStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append($"{NsqlKeywords.Create} {NsqlKeywords.Schema} ").Append(EscapedIdentifier(s.Name)).AppendLine(";");
                break;
            case Syn.Schemas.GrantSchemaUsageStatement s:
                sb.Append($"{NsqlKeywords.Grant} {NsqlKeywords.Usage} {NsqlKeywords.On} {NsqlKeywords.Schema} ").Append(EscapedIdentifier(s.Schema)).Append($" {NsqlKeywords.To} ").Append(EscapedIdentifier(s.Role)).AppendLine(";");
                break;
            case Syn.Enums.CreateEnumStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append($"{NsqlKeywords.Create} {NsqlKeywords.Enum} ").Append(Qualified(s.Name));
                sb.Append(" (").Append(string.Join(", ", s.Values.Select(v => $"'{v.Replace("'", "''")}'"))).AppendLine(");");
                break;
            case Syn.Domains.CreateDomainStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append($"{NsqlKeywords.Create} {NsqlKeywords.Domain} ").Append(Qualified(s.Name));
                sb.Append($" {NsqlKeywords.As} ").Append(TypeText(s.Type));
                if (s.NotNull)
                {
                    sb.Append($" {NsqlKeywords.Not} {NsqlKeywords.Null}");
                }
                foreach (var check in s.Checks)
                {
                    sb.Append($" {NsqlKeywords.Constraint} ").Append(check.Name.Value).Append($" {NsqlKeywords.Check} (").Append(check.Expression.Value).Append(')');
                }
                // The default, if any, comes last: its opaque expression is read back up to the terminating ';'.
                if (s.Default is { } @default)
                {
                    sb.Append($" {NsqlKeywords.Default} ").Append(@default.Value);
                }
                sb.AppendLine(";");
                break;
            case Syn.CompositeTypes.CreateCompositeTypeStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append($"{NsqlKeywords.Create} {NsqlKeywords.Type} ").Append(Qualified(s.Name));
                sb.Append($" {NsqlKeywords.As} (").Append(string.Join(", ", s.Fields.Select(f => $"{f.Name.Value} {TypeText(f.Type)}"))).AppendLine(");");
                break;
            case Syn.Sequences.CreateSequenceStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append($"{NsqlKeywords.Create} {NsqlKeywords.Sequence} ").Append(Qualified(s.Name));
                if (s.Options is { } options && SequenceOptionsText(options) is { } text)
                {
                    sb.Append(" (").Append(text).Append(')');
                }
                sb.AppendLine(";");
                break;
            case Syn.Routines.CreateRoutineStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append(s.Kind == Syn.Routines.RoutineKind.Procedure ? $"{NsqlKeywords.Create} {NsqlKeywords.Procedure} " : $"{NsqlKeywords.Create} {NsqlKeywords.Function} ").Append(Qualified(s.Name));
                // The definition is emitted verbatim (multi-line bodies keep their newlines); TrimEnd guards a
                // code-built definition ending in whitespace so the ';' lands directly after the last character.
                sb.Append('(').Append(s.Arguments.Value).Append(") ").Append(s.Definition.Value.TrimEnd()).AppendLine(";");
                break;
            case Syn.Tables.CreateTableStatement s:
                WriteTable(sb, s);
                break;
            case Syn.Tables.GrantTableStatement s:
                sb.Append($"{NsqlKeywords.Grant} ").Append(PrivilegesText(s.Privileges))
                    .Append($" {NsqlKeywords.On} ").Append(Qualified(s.On))
                    .Append($" {NsqlKeywords.To} ").Append(EscapedIdentifier(s.Role)).AppendLine(";");
                break;
            case Syn.Triggers.CreateTriggerStatement s:
                WriteTrigger(sb, s);
                break;
            case Syn.Views.CreateViewStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append($"{NsqlKeywords.Create} ");
                if (s.IsMaterialized)
                {
                    sb.Append($"{NsqlKeywords.Materialized} ");
                }
                sb.Append($"{NsqlKeywords.View} ").Append(Qualified(s.Name));
                sb.Append($" {NsqlKeywords.As} ").Append(s.Body.Value).AppendLine(";");
                break;
            case Syn.Indexes.CreateIndexStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append($"{NsqlKeywords.Create} ");
                if (s.IsUnique)
                {
                    sb.Append($"{NsqlKeywords.Unique} ");
                }
                sb.Append($"{NsqlKeywords.Index} ").Append(EscapedIdentifier(s.Name)).Append($" {NsqlKeywords.On} ").Append(Qualified(s.On));
                AppendIndexTail(sb, s.Method, s.Columns, s.Include, s.Predicate);
                sb.AppendLine(";");
                break;
            case Syn.Extensions.CreateExtensionStatement s:
                WriteDocComment(sb, s.Doc, indent: "");
                sb.Append($"{NsqlKeywords.Create} {NsqlKeywords.Extension} ").Append(EscapedIdentifier(s.Name));
                if (s.Version is { } version)
                {
                    sb.Append($" {NsqlKeywords.Version} '").Append(version.Replace("'", "''")).Append('\'');
                }
                sb.AppendLine(";");
                break;
            case Syn.Scripts.ScriptStatement s:
                WriteScript(sb, s);
                break;
            case Syn.Schemas.RenameSchemaStatement s:
                sb.Append($"{NsqlKeywords.Rename} {NsqlKeywords.Schema} ").Append(EscapedIdentifier(s.From)).Append($" {NsqlKeywords.To} ").Append(EscapedIdentifier(s.To)).AppendLine(";");
                break;
            case RenameObjectStatement s:
                sb.Append($"{NsqlKeywords.Rename} {KindKeyword(s.Kind)} ").Append(Qualified(s.From)).Append($" {NsqlKeywords.To} ").Append(EscapedIdentifier(s.To)).AppendLine(";");
                break;
            case Syn.Tables.RenameColumnStatement s:
                sb.Append($"{NsqlKeywords.Rename} {NsqlKeywords.Column} ").Append(EscapedIdentifier(s.From.Schema!)).Append('.').Append(EscapedIdentifier(s.From.Table)).Append('.').Append(EscapedIdentifier(s.From.Member))
                    .Append($" {NsqlKeywords.To} ").Append(EscapedIdentifier(s.To)).AppendLine(";");
                break;
            default:
                throw new NotSupportedException($"Statement '{statement.GetType().Name}' is not rendered.");
        }
    }

    private static void WriteTable(StringBuilder sb, Syn.Tables.CreateTableStatement statement)
    {
        WriteDocComment(sb, statement.Doc, indent: "");
        sb.Append($"{NsqlKeywords.Create} {NsqlKeywords.Table} ").Append(Qualified(statement.Name));
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
                    sb.Append(EscapedIdentifier(m.Name)).Append(' ').Append(TypeText(m.Type));
                    if (!m.IsNullable)
                    {
                        sb.Append($" {NsqlKeywords.Not} {NsqlKeywords.Null}");
                    }
                    if (m.IsIdentity)
                    {
                        sb.Append($" {NsqlKeywords.Identity}");
                        if (m.IdentityOptions is { } options && IdentityOptionsText(options) is { } text)
                        {
                            sb.Append(" (").Append(text).Append(')');
                        }
                    }
                    if (m.Default is { } @default)
                    {
                        sb.Append($" {NsqlKeywords.Default} ").Append(@default.Value);
                    }
                    if (m.Generated is { } generated)
                    {
                        sb.Append($" {NsqlKeywords.Generated} {NsqlKeywords.Always} {NsqlKeywords.As} (").Append(generated.Value).Append($") {NsqlKeywords.Stored}");
                    }
                    return sb.ToString();
                }
            case Syn.Constraints.PrimaryKeyDefinition m:
                return $"{NsqlKeywords.Constraint} {EscapedIdentifier(m.Name)} {NsqlKeywords.Primary} {NsqlKeywords.Key} ({ColumnsText(m.Columns)})";
            case Syn.Constraints.ForeignKeyDefinition m:
                {
                    var sb = new StringBuilder();
                    sb.Append($"{NsqlKeywords.Constraint} ").Append(EscapedIdentifier(m.Name))
                        .Append($" {NsqlKeywords.Foreign} {NsqlKeywords.Key} (").Append(ColumnsText(m.Columns)).Append(')')
                        .Append($" {NsqlKeywords.References} ").Append(Qualified(m.References))
                        .Append(" (").Append(ColumnsText(m.ReferencedColumns)).Append(')');
                    if (m.OnDelete != Syn.Constraints.ReferentialAction.NoAction)
                    {
                        sb.Append($" {NsqlKeywords.On} {NsqlKeywords.Delete} ").Append(ActionText(m.OnDelete));
                    }
                    if (m.OnUpdate != Syn.Constraints.ReferentialAction.NoAction)
                    {
                        sb.Append($" {NsqlKeywords.On} {NsqlKeywords.Update} ").Append(ActionText(m.OnUpdate));
                    }
                    return sb.ToString();
                }
            case Syn.Constraints.UniqueDefinition m:
                return $"{NsqlKeywords.Constraint} {EscapedIdentifier(m.Name)} {NsqlKeywords.Unique} ({ColumnsText(m.Columns)})";
            case Syn.Constraints.CheckDefinition m:
                return $"{NsqlKeywords.Constraint} {EscapedIdentifier(m.Name)} {NsqlKeywords.Check} ({m.Expression.Value})";
            case Syn.Constraints.ExclusionDefinition m:
                {
                    var sb = new StringBuilder();
                    sb.Append($"{NsqlKeywords.Constraint} ").Append(EscapedIdentifier(m.Name)).Append($" {NsqlKeywords.Exclude}");
                    if (m.Method is { } method)
                    {
                        sb.Append($" {NsqlKeywords.Using} ").Append(EscapedIdentifier(method));
                    }
                    sb.Append(" (").Append(string.Join(", ", m.Elements.Select(ExclusionElementText))).Append(')');
                    if (m.Predicate is { } predicate)
                    {
                        sb.Append($" {NsqlKeywords.Where} (").Append(predicate.Value).Append(')');
                    }
                    return sb.ToString();
                }
            case Syn.Indexes.IndexDefinition m:
                {
                    var sb = new StringBuilder();
                    if (m.IsUnique)
                    {
                        sb.Append($"{NsqlKeywords.Unique} ");
                    }
                    sb.Append($"{NsqlKeywords.Index} ").Append(EscapedIdentifier(m.Name));
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
        sb.Append($"{NsqlKeywords.Create} {NsqlKeywords.Trigger} ").Append(EscapedIdentifier(statement.Name)).Append(' ').Append(TimingText(statement.Timing))
            .Append(' ').Append(EventsText(statement))
            .Append($" {NsqlKeywords.On} ").Append(Qualified(statement.On));

        // An inline-body trigger (SQL Server) carries its action in a dollar-quoted block; it has no FOR EACH /
        // WHEN / function clauses. A function trigger (PostgreSQL) keeps those.
        if (statement.Action is Syn.Triggers.InlineBodyAction inline)
        {
            var delimiter = DollarDelimiter(inline.Body);
            sb.Append($" {NsqlKeywords.As} ").AppendLine(delimiter);
            sb.AppendLine(inline.Body.Value);
            sb.Append(delimiter).AppendLine(";");
            return;
        }

        var action = (Syn.Triggers.ExecuteFunctionAction)statement.Action;
        sb.Append($" {NsqlKeywords.For} {NsqlKeywords.Each} ").Append(statement.Level == Syn.Triggers.TriggerLevel.Row ? NsqlKeywords.Row : NsqlKeywords.Statement);
        if (statement.When is { } when)
        {
            sb.Append($" {NsqlKeywords.When} (").Append(when.Value).Append(')');
        }
        sb.Append($" {NsqlKeywords.Execute} {NsqlKeywords.Function} ").Append(Qualified(action.Function))
            .Append('(').Append(action.Arguments.Value).Append(')').AppendLine(";");
    }

    private static void WriteScript(StringBuilder sb, Syn.Scripts.ScriptStatement statement)
    {
        sb.Append($"{NsqlKeywords.Script} ").Append(EscapedIdentifier(statement.Name)).Append($" {NsqlKeywords.Run}");
        if (statement.RunCondition == Syn.Scripts.RunCondition.Once)
        {
            sb.Append($" {NsqlKeywords.Once}");
        }
        sb.Append($" {NsqlKeywords.On} ").Append(EventText(statement.Event));
        if (statement.RunOutsideTransaction)
        {
            sb.Append(" (run_outside_transaction = true)");
        }

        // Emit the body in a dollar-quoted block, choosing a tag that doesn't occur in the body so it is
        // taken verbatim. The reader strips the delimiters and trims surrounding whitespace, so a body
        // stored without its delimiters round-trips back to the same text.
        var delimiter = DollarDelimiter(statement.Body);
        sb.Append($" {NsqlKeywords.As} ").AppendLine(delimiter);
        sb.AppendLine(statement.Body.Value);
        sb.Append(delimiter).AppendLine(";");
    }

    private static string EventText(Syn.Scripts.ScriptEventClause clause) => clause switch
    {
        Syn.Scripts.DeploymentEventClause deployment =>
            deployment.Phase == Syn.Scripts.DeploymentPhase.Pre ? $"{NsqlKeywords.Pre} {NsqlKeywords.Deployment}" : $"{NsqlKeywords.Post} {NsqlKeywords.Deployment}",
        Syn.Scripts.ChangeEventClause change => $"{TriggerText(change.Trigger)} {PathText(change.Path)}",
        _ => throw new NotSupportedException($"Script event '{clause.GetType().Name}' is not rendered."),
    };

    private static string TriggerText(Syn.Scripts.ChangeTrigger trigger) => trigger switch
    {
        Syn.Scripts.ChangeTrigger.AddColumn => $"{NsqlKeywords.Add} {NsqlKeywords.Column}",
        Syn.Scripts.ChangeTrigger.AlterColumnType => $"{NsqlKeywords.Alter} {NsqlKeywords.Column} {NsqlKeywords.Type}",
        _ => $"{NsqlKeywords.Add} {NsqlKeywords.Constraint}",
    };

    private static string PathText(MemberPath path) =>
        path.Schema is { } schema ? $"{EscapedIdentifier(schema)}.{EscapedIdentifier(path.Table)}.{EscapedIdentifier(path.Member)}" : $"{EscapedIdentifier(path.Table)}.{EscapedIdentifier(path.Member)}";

    // --- clause helpers ----------------------------------------------------------------


    private static void AppendIndexTail(StringBuilder sb, Identifier? method, IReadOnlyList<Syn.Indexes.IndexElement> columns,
        IReadOnlyList<Identifier>? include, SqlText? predicate)
    {
        if (method is not null)
        {
            sb.Append($" {NsqlKeywords.Using} ").Append(EscapedIdentifier(method));
        }
        sb.Append(" (").Append(string.Join(", ", columns.Select(IndexKeyText))).Append(')');
        if (include is { Count: > 0 })
        {
            sb.Append($" {NsqlKeywords.Include} (").Append(ColumnsText(include)).Append(')');
        }
        if (predicate is { } p)
        {
            sb.Append($" {NsqlKeywords.Where} (").Append(p.Value).Append(')');
        }
    }

    private static string IndexKeyText(Syn.Indexes.IndexElement element)
    {
        var sb = new StringBuilder();
        sb.Append(element.Column is { } name ? EscapedIdentifier(name) : $"({element.Expression!.Value})");
        sb.Append(element.Sort switch
        {
            Syn.Indexes.IndexSort.Ascending => $" {NsqlKeywords.Asc}",
            Syn.Indexes.IndexSort.Descending => $" {NsqlKeywords.Desc}",
            _ => string.Empty,
        });
        sb.Append(element.Nulls switch
        {
            Syn.Indexes.IndexNulls.First => $" {NsqlKeywords.Nulls} {NsqlKeywords.First}",
            Syn.Indexes.IndexNulls.Last => $" {NsqlKeywords.Nulls} {NsqlKeywords.Last}",
            _ => string.Empty,
        });
        return sb.ToString();
    }

    private static string ExclusionElementText(Syn.Constraints.ExclusionElement element) =>
        $"{(element.Column is { } column ? EscapedIdentifier(column) : $"({element.Expression!.Value})")} {NsqlKeywords.With} {element.Operator}";

    private static string? SequenceOptionsText(Syn.Sequences.SequenceOptionsClause options)
    {
        var parts = new List<string>();
        if (options.As is { } type)
        {
            parts.Add($"{NsqlKeywords.As} {TypeText(type)}");
        }
        if (options.Start is { } start)
        {
            parts.Add($"{NsqlKeywords.Start} {start}");
        }
        if (options.Increment is { } increment)
        {
            parts.Add($"{NsqlKeywords.Increment} {increment}");
        }
        if (options.MinValue is { } min)
        {
            parts.Add($"{NsqlKeywords.MinValue} {min}");
        }
        if (options.MaxValue is { } max)
        {
            parts.Add($"{NsqlKeywords.MaxValue} {max}");
        }
        if (options.Cache is { } cache)
        {
            parts.Add($"{NsqlKeywords.Cache} {cache}");
        }
        if (options.Cycle)
        {
            parts.Add(NsqlKeywords.Cycle);
        }
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string? IdentityOptionsText(Syn.Tables.IdentityOptionsClause options)
    {
        var parts = new List<string>();
        if (options.Start is { } start)
        {
            parts.Add($"{NsqlKeywords.Start} {start}");
        }
        if (options.Increment is { } increment)
        {
            parts.Add($"{NsqlKeywords.Increment} {increment}");
        }
        if (options.MinValue is { } min)
        {
            parts.Add($"{NsqlKeywords.MinValue} {min}");
        }
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string TimingText(Syn.Triggers.TriggerTiming timing) => timing switch
    {
        Syn.Triggers.TriggerTiming.Before => NsqlKeywords.Before,
        Syn.Triggers.TriggerTiming.After => NsqlKeywords.After,
        _ => $"{NsqlKeywords.Instead} {NsqlKeywords.Of}",
    };

    private static string EventsText(Syn.Triggers.CreateTriggerStatement statement)
    {
        var parts = new List<string>(4);
        if (statement.Events.HasFlag(Syn.Triggers.TriggerEvent.Insert))
        {
            parts.Add(NsqlKeywords.Insert);
        }
        if (statement.Events.HasFlag(Syn.Triggers.TriggerEvent.Update))
        {
            parts.Add(statement.UpdateOfColumns is { Count: > 0 } columns ? $"{NsqlKeywords.Update} {NsqlKeywords.Of} ({ColumnsText(columns)})" : NsqlKeywords.Update);
        }
        if (statement.Events.HasFlag(Syn.Triggers.TriggerEvent.Delete))
        {
            parts.Add(NsqlKeywords.Delete);
        }
        if (statement.Events.HasFlag(Syn.Triggers.TriggerEvent.Truncate))
        {
            parts.Add(NsqlKeywords.Truncate);
        }
        return string.Join($" {NsqlKeywords.Or} ", parts);
    }

    private static string ActionText(Syn.Constraints.ReferentialAction action) => action switch
    {
        Syn.Constraints.ReferentialAction.Cascade => NsqlKeywords.Cascade,
        Syn.Constraints.ReferentialAction.SetNull => $"{NsqlKeywords.Set} {NsqlKeywords.Null}",
        Syn.Constraints.ReferentialAction.SetDefault => $"{NsqlKeywords.Set} {NsqlKeywords.Default}",
        _ => $"{NsqlKeywords.No} {NsqlKeywords.Action}",
    };

    private static string PrivilegesText(Syn.Tables.TablePrivilege privileges)
    {
        var parts = new List<string>();
        if (privileges.HasFlag(Syn.Tables.TablePrivilege.Select))
        {
            parts.Add(NsqlKeywords.Select);
        }
        if (privileges.HasFlag(Syn.Tables.TablePrivilege.Insert))
        {
            parts.Add(NsqlKeywords.Insert);
        }
        if (privileges.HasFlag(Syn.Tables.TablePrivilege.Update))
        {
            parts.Add(NsqlKeywords.Update);
        }
        if (privileges.HasFlag(Syn.Tables.TablePrivilege.Delete))
        {
            parts.Add(NsqlKeywords.Delete);
        }
        return string.Join(", ", parts);
    }

    /// <summary>
    /// The canonical keyword for an object kind; the spelling variants (MATERIALIZED VIEW, FUNCTION,
    /// PROCEDURE) normalize to it.
    /// </summary>
    private static string KindKeyword(ObjectKind kind) => kind switch
    {
        ObjectKind.Table => NsqlKeywords.Table,
        ObjectKind.View => NsqlKeywords.View,
        ObjectKind.Enum => NsqlKeywords.Enum,
        ObjectKind.Sequence => NsqlKeywords.Sequence,
        ObjectKind.Routine => NsqlKeywords.Routine,
        ObjectKind.Domain => NsqlKeywords.Domain,
        ObjectKind.CompositeType => NsqlKeywords.Type,
        ObjectKind.Extension => NsqlKeywords.Extension,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static string Qualified(QualifiedName name) =>
        name.Schema is { } schema ? $"{EscapedIdentifier(schema)}.{EscapedIdentifier(name.Name)}" : EscapedIdentifier(name.Name);

    private static string TypeText(TypeName type)
    {
        var text = type.Schema is { } schema ? $"{EscapedIdentifier(schema)}.{EscapedIdentifier(type.Name)}" : EscapedIdentifier(type.Name);
        return type.Arguments is { } arguments ? $"{text}({arguments})" : text;
    }

    private static string ColumnsText(IReadOnlyList<Identifier> columns) => string.Join(", ", columns.Select(EscapedIdentifier));

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

    private static string EscapedIdentifier(Identifier identifier) => NeedsQuoting(identifier)
        ? $"\"{identifier.Value.Replace("\"", "\"\"")}\""
        : identifier.Value;

    private static bool NeedsQuoting(Identifier identifier)
    {
        if (identifier.Value.Length == 0 || !NsqlLexer.IsIdentifierStart(identifier.Value[0]) || NsqlKeywords.MemberOpeners.Contains(identifier.Value))
        {
            return true;
        }

        return !identifier.Value.All(NsqlLexer.IsIdentifierPart);
    }
}
