using System.Text;
using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Migration;

/// <summary>
/// Default <see cref="IMigrationPlanRenderer"/> that produces a Terraform-style diff.
/// Uses ANSI colour when the output is going to a TTY and <c>NO_COLOR</c> is not set.
/// </summary>
internal sealed class DefaultMigrationPlanRenderer : IMigrationPlanRenderer
{
    public string Render(MigrationPlan plan)
    {
        if (plan.IsEmpty)
        {
            return "No changes detected.";
        }

        var model = BuildDiffModel(plan);
        return RenderDiffModel(model, Palette.ForCurrentEnvironment());
    }

    // -------------------------------------------------------------------------
    // Model building — translates a flat action list into a structured diff tree
    // -------------------------------------------------------------------------

    private static DiffModel BuildDiffModel(MigrationPlan plan)
    {
        var scripts = plan.Actions.OfType<RunScript>().ToList();

        var groups = plan.Actions
            .Where(a => a is not RunScript)
            .GroupBy(GroupKeyFor)
            .Select(g => BuildGroup(g.Key, g.ToList()))
            .OrderBy(g => g.SortKey, StringComparer.Ordinal)
            .Select(g => g.Group)
            .ToList();

        return new DiffModel(
            Adds: groups.Count(g => g.Kind == ChangeKind.Add),
            Changes: groups.Count(g => g.Kind == ChangeKind.Modify),
            Removes: groups.Count(g => g.Kind == ChangeKind.Remove),
            Groups: groups,
            PreDeploymentScripts: scripts
                .Where(s => s.Script.Type == ScriptType.PreDeployment)
                .Select(s => s.Script.Name).ToList(),
            PostDeploymentScripts: scripts
                .Where(s => s.Script.Type == ScriptType.PostDeployment)
                .Select(s => s.Script.Name).ToList());
    }

    private static (string SortKey, DiffGroup Group) BuildGroup(GroupKey key, List<MigrationAction> actions)
    {
        var (kind, header, absorbedComment) = ClassifyGroup(key, actions);

        // Column comments are pre-indexed so BuildColumnLines and BuildDetailLines can both
        // look them up without coordinating through shared mutable state.
        var columnComments = actions
            .OfType<SetColumnComment>()
            .ToDictionary(c => c.ColumnName, c => c);

        var (columnLines, consumedByColumns) = BuildColumnLines(kind, actions, columnComments);
        var detailLines = BuildDetailLines(kind, actions, absorbedComment, columnComments, consumedByColumns);

        var sortKey = key.TableName is null
            ? $"{key.SchemaName}:0"
            : $"{key.SchemaName}:1:{key.TableName}";

        return (sortKey, new DiffGroup(kind, header, columnLines, detailLines));
    }

    /// <summary>
    /// Determines the change kind and builds the full header string for a group, including
    /// any rename notation and the comment folded in. Returns the comment action that was
    /// absorbed so the detail-line builder can skip it.
    /// </summary>
    private static (ChangeKind Kind, string Header, MigrationAction? AbsorbedComment) ClassifyGroup(
        GroupKey key, IReadOnlyList<MigrationAction> actions)
    {
        if (key.TableName is null)
        {
            ChangeKind kind;
            string target;

            if (actions.Any(a => a is CreateSchema))
            {
                (kind, target) = (ChangeKind.Add, key.SchemaName);
            }
            else if (actions.Any(a => a is DropSchema))
            {
                (kind, target) = (ChangeKind.Remove, key.SchemaName);
            }
            else if (actions.OfType<RenameSchema>().FirstOrDefault() is { } sr)
            {
                (kind, target) = (ChangeKind.Modify, $"{sr.OldName} → {sr.NewName}");
            }
            else
            {
                (kind, target) = (ChangeKind.Modify, key.SchemaName);
            }

            var comment = actions.OfType<SetSchemaComment>().FirstOrDefault();
            var header = comment is null
                ? $"schema {target}"
                : $"schema {target}{CommentSuffix(comment.OldComment, comment.NewComment)}";

            return (kind, header, comment);
        }
        else
        {
            ChangeKind kind;
            string target;

            if (actions.Any(a => a is CreateTable))
            {
                (kind, target) = (ChangeKind.Add, $"{key.SchemaName}.{key.TableName}");
            }
            else if (actions.Any(a => a is DropTable))
            {
                (kind, target) = (ChangeKind.Remove, $"{key.SchemaName}.{key.TableName}");
            }
            else if (actions.OfType<RenameTable>().FirstOrDefault() is { } tr)
            {
                (kind, target) = (ChangeKind.Modify, $"{key.SchemaName}.{tr.OldName} → {tr.NewName}");
            }
            else
            {
                (kind, target) = (ChangeKind.Modify, $"{key.SchemaName}.{key.TableName}");
            }

            var comment = actions.OfType<SetTableComment>().FirstOrDefault();
            var header = comment is null
                ? $"table {target}"
                : $"table {target}{CommentSuffix(comment.OldComment, comment.NewComment)}";

            return (kind, header, comment);
        }
    }

    /// <summary>
    /// Produces the inline column list for a new-table group. Returns the set of column names
    /// whose comments were consumed here so the detail builder can skip the corresponding
    /// SetColumnComment actions.
    /// </summary>
    private static (IReadOnlyList<DiffLine> Lines, HashSet<string> ConsumedColumnNames) BuildColumnLines(
        ChangeKind kind,
        IReadOnlyList<MigrationAction> actions,
        Dictionary<string, SetColumnComment> columnComments)
    {
        if (kind != ChangeKind.Add || actions.OfType<CreateTable>().FirstOrDefault() is not { } createTable)
        {
            return ([], []);
        }

        var lines = new List<DiffLine>(createTable.Table.Columns.Count);
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var column in createTable.Table.Columns)
        {
            var text = FormatColumn(column);
            if (columnComments.TryGetValue(column.Name, out var comment))
            {
                text += CommentSuffix(comment.OldComment, comment.NewComment);
                consumed.Add(column.Name);
            }
            lines.Add(new DiffLine(ChangeKind.Add, text));
        }
        return (lines, consumed);
    }

    /// <summary>
    /// Produces detail lines for a group, skipping implicit/absorbed actions and folding
    /// column comments into their AddColumn lines.
    /// </summary>
    private static IReadOnlyList<DiffLine> BuildDetailLines(
        ChangeKind kind,
        IReadOnlyList<MigrationAction> actions,
        MigrationAction? absorbedComment,
        Dictionary<string, SetColumnComment> columnComments,
        HashSet<string> consumedByColumns)
    {
        // Pre-compute which SetColumnComment actions will be folded into AddColumn lines so we
        // can skip them unconditionally regardless of action ordering in the list.
        var consumedColumnNames = new HashSet<string>(consumedByColumns, StringComparer.Ordinal);
        foreach (var add in actions.OfType<AddColumn>())
        {
            if (columnComments.ContainsKey(add.Column.Name))
            {
                consumedColumnNames.Add(add.Column.Name);
            }
        }

        var lines = new List<DiffLine>();
        foreach (var action in actions)
        {
            if (action == absorbedComment)
            {
                continue;
            }

            if (kind == ChangeKind.Add && action is CreateSchema or CreateTable)
            {
                continue;
            }

            if (kind == ChangeKind.Remove && action is DropSchema or DropTable)
            {
                continue;
            }

            if (action is RenameSchema or RenameTable)
            {
                continue;
            }

            if (action is SetColumnComment scc && consumedColumnNames.Contains(scc.ColumnName))
            {
                continue;
            }

            if (action is AddColumn add && columnComments.TryGetValue(add.Column.Name, out var addComment))
            {
                lines.Add(new DiffLine(ChangeKind.Add, FormatColumn(add.Column) + CommentSuffix(addComment.OldComment, addComment.NewComment)));
                continue;
            }

            var (lineKind, lineText) = DescribeAction(action);
            lines.Add(new DiffLine(lineKind, lineText));
        }
        return lines;
    }

    // -------------------------------------------------------------------------
    // Rendering — pure string formatting, no decisions
    // -------------------------------------------------------------------------

    private static string RenderDiffModel(DiffModel model, Palette palette)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Plan: {model.Adds} to add, {model.Changes} to change, {model.Removes} to destroy.");

        foreach (var group in model.Groups)
        {
            sb.AppendLine();
            sb.Append(palette.For(group.Kind)).Append(' ').AppendLine(group.Header);

            foreach (var line in group.ColumnLines)
            {
                sb.Append("    ").Append(palette.For(line.Kind)).Append(' ').AppendLine(line.Text);
            }

            if (group.ColumnLines.Count > 0 && group.DetailLines.Count > 0)
            {
                sb.AppendLine();
            }

            foreach (var line in group.DetailLines)
            {
                sb.Append("    ").Append(palette.For(line.Kind)).Append(' ').AppendLine(line.Text);
            }
        }

        if (model.PreDeploymentScripts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Pre-deployment scripts:");
            foreach (var name in model.PreDeploymentScripts)
            {
                sb.AppendLine($"  • {name}");
            }
        }

        if (model.PostDeploymentScripts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Post-deployment scripts:");
            foreach (var name in model.PostDeploymentScripts)
            {
                sb.AppendLine($"  • {name}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    // -------------------------------------------------------------------------
    // Action routing
    // -------------------------------------------------------------------------

    private static GroupKey GroupKeyFor(MigrationAction action) => action switch
    {
        CreateSchema a => new GroupKey(a.SchemaName, null),
        DropSchema a => new GroupKey(a.SchemaName, null),
        RenameSchema a => new GroupKey(a.NewName, null),
        SetSchemaComment a => new GroupKey(a.SchemaName, null),
        GrantSchemaUsage a => new GroupKey(a.SchemaName, null),
        RevokeSchemaUsage a => new GroupKey(a.SchemaName, null),

        CreateTable a => new GroupKey(a.SchemaName, a.Table.Name),
        DropTable a => new GroupKey(a.SchemaName, a.TableName),
        RenameTable a => new GroupKey(a.SchemaName, a.NewName),
        SetTableComment a => new GroupKey(a.SchemaName, a.TableName),
        GrantTablePrivileges a => new GroupKey(a.SchemaName, a.TableName),
        RevokeTablePrivileges a => new GroupKey(a.SchemaName, a.TableName),

        AddColumn a => new GroupKey(a.SchemaName, a.TableName),
        DropColumn a => new GroupKey(a.SchemaName, a.TableName),
        RenameColumn a => new GroupKey(a.SchemaName, a.TableName),
        AlterColumnType a => new GroupKey(a.SchemaName, a.TableName),
        AlterColumnNullability a => new GroupKey(a.SchemaName, a.TableName),
        AlterIdentitySequence a => new GroupKey(a.SchemaName, a.TableName),
        SetColumnDefault a => new GroupKey(a.SchemaName, a.TableName),
        SetColumnComment a => new GroupKey(a.SchemaName, a.TableName),

        CreateIndex a => new GroupKey(a.SchemaName, a.TableName),
        DropIndex a => new GroupKey(a.SchemaName, a.TableName),
        SetIndexComment a => new GroupKey(a.SchemaName, a.TableName),

        AddPrimaryKey a => new GroupKey(a.SchemaName, a.TableName),
        DropPrimaryKey a => new GroupKey(a.SchemaName, a.TableName),
        AddForeignKey a => new GroupKey(a.SchemaName, a.TableName),
        DropForeignKey a => new GroupKey(a.SchemaName, a.TableName),

        _ => throw new NotSupportedException($"Renderer cannot group action of type {action.GetType().Name}."),
    };

    private static (ChangeKind Kind, string Text) DescribeAction(MigrationAction action) => action switch
    {
        CreateSchema a => (ChangeKind.Add, $"schema {a.SchemaName}"),
        DropSchema a => (ChangeKind.Remove, $"schema {a.SchemaName}"),
        RenameSchema a => (ChangeKind.Modify, $"rename: {a.OldName} → {a.NewName}"),
        SetSchemaComment a => (ChangeKind.Modify, $"comment: {FormatComment(a.OldComment)} → {FormatComment(a.NewComment)}"),
        GrantSchemaUsage a => (ChangeKind.Add, $"grant usage to {a.Role}"),
        RevokeSchemaUsage a => (ChangeKind.Remove, $"revoke usage from {a.Role}"),

        CreateTable a => (ChangeKind.Add, $"table {a.Table.Name}"),
        DropTable a => (ChangeKind.Remove, $"table {a.TableName}"),
        RenameTable a => (ChangeKind.Modify, $"rename: {a.OldName} → {a.NewName}"),
        SetTableComment a => (ChangeKind.Modify, $"comment: {FormatComment(a.OldComment)} → {FormatComment(a.NewComment)}"),
        GrantTablePrivileges a => (ChangeKind.Add, $"grant {a.Privileges} to {a.Role}"),
        RevokeTablePrivileges a => (ChangeKind.Remove, $"revoke {a.Privileges} from {a.Role}"),

        AddColumn a => (ChangeKind.Add, FormatColumn(a.Column)),
        DropColumn a => (ChangeKind.Remove, FormatColumn(a.Column)),
        RenameColumn a => (ChangeKind.Modify, $"rename column: {a.OldName} → {a.NewName}"),
        AlterColumnType a => (ChangeKind.Modify, $"{a.ColumnName} type: {a.OldType} → {a.NewType}"),
        AlterColumnNullability a => (ChangeKind.Modify, $"{a.ColumnName} nullable: {FormatNullability(a.OldNullable)} → {FormatNullability(a.NewNullable)}"),
        AlterIdentitySequence a => (ChangeKind.Modify, $"{a.ColumnName} identity: {FormatIdentity(a.OldOptions)} → {FormatIdentity(a.NewOptions)}"),
        SetColumnDefault a => (ChangeKind.Modify, $"{a.ColumnName} default: {FormatDefault(a.OldDefault)} → {FormatDefault(a.NewDefault)}"),
        SetColumnComment a => (ChangeKind.Modify, $"{a.ColumnName} comment: {FormatComment(a.OldComment)} → {FormatComment(a.NewComment)}"),

        CreateIndex a => (ChangeKind.Add, $"index {a.Index.Name}"),
        DropIndex a => (ChangeKind.Remove, $"index {a.IndexName}"),
        SetIndexComment a => (ChangeKind.Modify, $"index {a.IndexName} comment: {FormatComment(a.OldComment)} → {FormatComment(a.NewComment)}"),

        AddPrimaryKey a => (ChangeKind.Add, $"primary key {a.PrimaryKey.Name}"),
        DropPrimaryKey a => (ChangeKind.Remove, $"primary key {a.PrimaryKeyName}"),
        AddForeignKey a => (ChangeKind.Add, $"foreign key {a.ForeignKey.Name}"),
        DropForeignKey a => (ChangeKind.Remove, $"foreign key {a.ForeignKeyName}"),

        _ => throw new NotSupportedException($"Renderer cannot describe action of type {action.GetType().Name}."),
    };

    // -------------------------------------------------------------------------
    // Formatters
    // -------------------------------------------------------------------------

    private static string FormatColumn(Column column) =>
        $"{column.Name} {column.Type} {(column.IsNullable ? "null" : "not null")}";

    private static string CommentSuffix(string? oldComment, string? newComment) => oldComment is null
        ? $" ({FormatComment(newComment)})"
        : $" ({FormatComment(oldComment)} → {FormatComment(newComment)})";

    private static string FormatComment(string? comment) => comment is null ? "<none>" : $"\"{comment}\"";

    private static string FormatDefault(string? value) => value ?? "<none>";

    private static string FormatNullability(bool nullable) => nullable ? "null" : "not null";

    private static string FormatIdentity(IdentityOptions? options)
    {
        if (options is null)
        {
            return "<none>";
        }

        var parts = new List<string>(3);
        if (options.StartWith.HasValue)
        {
            parts.Add($"start={options.StartWith}");
        }

        if (options.MinValue.HasValue)
        {
            parts.Add($"min={options.MinValue}");
        }

        if (options.IncrementBy.HasValue)
        {
            parts.Add($"step={options.IncrementBy}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "<default>";
    }

    // -------------------------------------------------------------------------
    // Private types
    // -------------------------------------------------------------------------

    private enum ChangeKind { Add, Modify, Remove }

    private readonly record struct GroupKey(string SchemaName, string? TableName);

    private sealed record DiffModel(
        int Adds,
        int Changes,
        int Removes,
        IReadOnlyList<DiffGroup> Groups,
        IReadOnlyList<string> PreDeploymentScripts,
        IReadOnlyList<string> PostDeploymentScripts);

    private sealed record DiffGroup(
        ChangeKind Kind,
        string Header,
        IReadOnlyList<DiffLine> ColumnLines,
        IReadOnlyList<DiffLine> DetailLines);

    private sealed record DiffLine(ChangeKind Kind, string Text);

    // Encapsulates ANSI rendering. When colour is disabled we substitute the plain marker
    // characters so the rest of the renderer doesn't have to branch on environment state.
    private sealed class Palette
    {
        private const string Reset = "\x1b[0m";
        private const string Green = "\x1b[32m";
        private const string Red = "\x1b[31m";
        private const string Yellow = "\x1b[33m";

        private readonly string _add;
        private readonly string _remove;
        private readonly string _modify;

        private Palette(string add, string remove, string modify)
        {
            _add = add;
            _remove = remove;
            _modify = modify;
        }

        public string For(ChangeKind kind) => kind switch
        {
            ChangeKind.Add => _add,
            ChangeKind.Remove => _remove,
            ChangeKind.Modify => _modify,
            _ => "?",
        };

        public static Palette ForCurrentEnvironment()
        {
            var noColor = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
            var tty = !Console.IsOutputRedirected;
            return noColor || !tty
                ? new Palette("+", "-", "~")
                : new Palette($"{Green}+{Reset}", $"{Red}-{Reset}", $"{Yellow}~{Reset}");
        }
    }
}
