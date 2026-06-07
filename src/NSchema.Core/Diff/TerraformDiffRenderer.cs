using System.Text;
using Microsoft.Extensions.Options;
using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

/// <summary>
/// Produces a Terraform-style output of a database diff.
/// </summary>
internal sealed class TerraformDiffRenderer(IOptions<TerraformDiffRendererOptions> options) : IDiffRenderer
{
    private readonly Palette _palette = Palette.For(options.Value.IncludeColour);

    public string Render(DatabaseDiff diff)
    {
        if (diff.IsEmpty)
        {
            return "No changes detected.";
        }

        var sb = new StringBuilder();

        foreach (var schema in diff.Schemas)
        {
            if (schema.Kind is { } kind)
            {
                RenderSchema(sb, schema, kind);
            }

            foreach (var table in schema.Tables)
            {
                RenderTable(sb, table);
            }
        }

        sb.AppendLine();

        var (added, modified, removed) = diff.GetSummary();
        sb.AppendLine($"Plan: {added} to add, {modified} to change, {removed} to destroy.");

        return sb.ToString().Trim();
    }

    private void RenderSchema(StringBuilder sb, SchemaDiff schema, ChangeKind kind)
    {
        var target = schema.RenamedFrom is null ? schema.Name : $"{schema.RenamedFrom} → {schema.Name}";
        AppendHeader(sb, kind, $"schema {target}{CommentSuffix(schema.Comment)}");

        foreach (var grant in schema.Grants)
        {
            var text = grant.Kind == ChangeKind.Add ? $"grant usage to {grant.Role}" : $"revoke usage from {grant.Role}";
            AppendDetail(sb, grant.Kind, text);
        }
    }

    private void RenderTable(StringBuilder sb, TableDiff table)
    {
        var name = table.RenamedFrom is null
            ? $"{table.Schema}.{table.Name}"
            : $"{table.Schema}.{table.RenamedFrom} → {table.Name}";
        AppendHeader(sb, table.Kind, $"table {name}{CommentSuffix(table.Comment)}");

        foreach (var column in table.Columns)
        {
            RenderColumn(sb, column);
        }

        // A new table renders its columns as a block, separated from the constraint/index/grant block by a
        // blank line. An existing table lists its column changes inline with everything that follows.
        var hasTrailingBlock = table.Constraints.Count > 0 || table.Indexes.Count > 0 || table.Grants.Count > 0;
        if (table.Kind == ChangeKind.Add && table.Columns.Count > 0 && hasTrailingBlock)
        {
            sb.AppendLine();
        }

        foreach (var constraint in table.Constraints)
        {
            var label = constraint.Type == ConstraintType.PrimaryKey ? "primary key" : "foreign key";
            AppendDetail(sb, constraint.Kind, $"{label} {constraint.Name}");
        }

        foreach (var index in table.Indexes)
        {
            var text = index.Kind == ChangeKind.Modify
                ? $"index {index.Name} comment: {FormatComment(index.Comment?.Old)} → {FormatComment(index.Comment?.New)}"
                : $"index {index.Name}";
            AppendDetail(sb, index.Kind, text);
        }

        foreach (var grant in table.Grants)
        {
            var privileges = FormatPrivileges(grant.Privileges);
            var text = grant.Kind == ChangeKind.Add
                ? $"grant {privileges} to {grant.Role}"
                : $"revoke {privileges} from {grant.Role}";
            AppendDetail(sb, grant.Kind, text);
        }
    }

    private void RenderColumn(StringBuilder sb, ColumnDiff column)
    {
        if (column.Kind == ChangeKind.Add && column.Definition is { } added)
        {
            AppendDetail(sb, ChangeKind.Add, FormatColumn(added) + CommentSuffix(column.Comment));
            return;
        }

        if (column.Kind == ChangeKind.Remove && column.Definition is { } removed)
        {
            AppendDetail(sb, ChangeKind.Remove, FormatColumn(removed));
            return;
        }

        if (column.RenamedFrom is not null)
        {
            AppendDetail(sb, ChangeKind.Modify, $"rename column: {column.RenamedFrom} → {column.Name}");
        }

        if (column.Type is { } type)
        {
            AppendDetail(sb, ChangeKind.Modify, $"{column.Name} type: {type.Old} → {type.New}");
        }

        if (column.Nullability is { } nullable)
        {
            AppendDetail(sb, ChangeKind.Modify, $"{column.Name} nullable: {FormatNullability(nullable.Old)} → {FormatNullability(nullable.New)}");
        }

        if (column.Default is { } @default)
        {
            AppendDetail(sb, ChangeKind.Modify, $"{column.Name} default: {FormatDefault(@default.Old)} → {FormatDefault(@default.New)}");
        }

        if (column.Identity is { } identity)
        {
            AppendDetail(sb, ChangeKind.Modify, $"{column.Name} identity: {FormatIdentity(identity.Old)} → {FormatIdentity(identity.New)}");
        }

        if (column.Comment is { } comment)
        {
            AppendDetail(sb, ChangeKind.Modify, $"{column.Name} comment: {FormatComment(comment.Old)} → {FormatComment(comment.New)}");
        }
    }

    private void AppendHeader(StringBuilder sb, ChangeKind kind, string text)
    {
        sb.AppendLine();
        sb.Append(_palette.For(kind)).Append(' ').AppendLine(text);
    }

    private void AppendDetail(StringBuilder sb, ChangeKind kind, string text) =>
        sb.Append(options.Value.Indent).Append(_palette.For(kind)).Append(' ').AppendLine(text);

    // -------------------------------------------------------------------------
    // Formatters
    // -------------------------------------------------------------------------

    private string FormatColumn(Column column) =>
        $"{column.Name} {column.Type} {(column.IsNullable ? "null" : "not null")}";

    private string CommentSuffix(ValueChange<string>? comment) => comment is null
        ? string.Empty
        : comment.Old is null
            ? $" ({FormatComment(comment.New)})"
            : $" ({FormatComment(comment.Old)} → {FormatComment(comment.New)})";

    // Decompose the privilege flags into the underlying SQL privileges rather than rendering the enum name,
    // which would surface aliases (e.g. ReadOnly for Select) and composites (All) instead of the real grants.
    private static string FormatPrivileges(TablePrivilege? privileges)
    {
        if (privileges is not { } granted || granted == TablePrivilege.None)
        {
            return "no privileges";
        }

        var parts = new List<string>(4);
        if (granted.HasFlag(TablePrivilege.Select))
        {
            parts.Add("SELECT");
        }

        if (granted.HasFlag(TablePrivilege.Insert))
        {
            parts.Add("INSERT");
        }

        if (granted.HasFlag(TablePrivilege.Update))
        {
            parts.Add("UPDATE");
        }

        if (granted.HasFlag(TablePrivilege.Delete))
        {
            parts.Add("DELETE");
        }

        return string.Join(", ", parts);
    }

    private string FormatComment(string? comment) => comment is null ? "<none>" : $"\"{comment}\"";

    private string FormatDefault(string? value) => value ?? "<none>";

    private string FormatNullability(bool? nullable) => nullable == true ? "null" : "not null";

    private string FormatIdentity(IdentityOptions? options)
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

    // Encapsulates ANSI rendering. When color is disabled we substitute the plain marker
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

        public static Palette For(bool color) => color
            ? new Palette($"{Green}+{Reset}", $"{Red}-{Reset}", $"{Yellow}~{Reset}")
            : new Palette("+", "-", "~");
    }
}
