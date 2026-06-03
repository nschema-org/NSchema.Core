using System.Text;
using Microsoft.Extensions.Options;
using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

/// <summary>
/// Default <see cref="IDiffRenderer"/> that produces a Terraform-style diff. Whether ANSI colour is
/// included is controlled by <see cref="TerraformDiffRendererOptions"/>. A pure walk of the
/// <see cref="MigrationDiff"/>: it formats each node and never reorganizes the model.
/// </summary>
internal sealed class TerraformDiffRenderer(IOptions<TerraformDiffRendererOptions> options) : IDiffRenderer
{
    private const string Indent = "    ";

    public string Render(MigrationDiff diff)
    {
        if (diff.IsEmpty)
        {
            return "No changes detected.";
        }

        var palette = Palette.For(options.Value.IncludeColour);
        var sb = new StringBuilder();

        var summary = diff.Summary;
        sb.AppendLine($"Plan: {summary.Added} to add, {summary.Modified} to change, {summary.Removed} to destroy.");

        foreach (var schema in diff.Schemas)
        {
            if (schema.Kind is { } kind)
            {
                RenderSchema(sb, palette, schema, kind);
            }

            foreach (var table in schema.Tables)
            {
                RenderTable(sb, palette, table);
            }
        }

        RenderScripts(sb, "Pre-deployment scripts:", diff.PreDeploymentScripts);
        RenderScripts(sb, "Post-deployment scripts:", diff.PostDeploymentScripts);

        return sb.ToString().TrimEnd();
    }

    private static void RenderSchema(StringBuilder sb, Palette palette, SchemaDiff schema, ChangeKind kind)
    {
        var target = schema.RenamedFrom is null ? schema.Name : $"{schema.RenamedFrom} → {schema.Name}";
        AppendHeader(sb, palette, kind, $"schema {target}{CommentSuffix(schema.Comment)}");

        foreach (var grant in schema.Grants)
        {
            var text = grant.Kind == ChangeKind.Add ? $"grant usage to {grant.Role}" : $"revoke usage from {grant.Role}";
            AppendDetail(sb, palette, grant.Kind, text);
        }
    }

    private static void RenderTable(StringBuilder sb, Palette palette, TableDiff table)
    {
        var name = table.RenamedFrom is null
            ? $"{table.Schema}.{table.Name}"
            : $"{table.Schema}.{table.RenamedFrom} → {table.Name}";
        AppendHeader(sb, palette, table.Kind, $"table {name}{CommentSuffix(table.Comment)}");

        foreach (var column in table.Columns)
        {
            RenderColumn(sb, palette, column);
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
            AppendDetail(sb, palette, constraint.Kind, $"{label} {constraint.Name}");
        }

        foreach (var index in table.Indexes)
        {
            var text = index.Kind == ChangeKind.Modify
                ? $"index {index.Name} comment: {FormatComment(index.Comment?.Old)} → {FormatComment(index.Comment?.New)}"
                : $"index {index.Name}";
            AppendDetail(sb, palette, index.Kind, text);
        }

        foreach (var grant in table.Grants)
        {
            var text = grant.Kind == ChangeKind.Add
                ? $"grant {grant.Privileges} to {grant.Role}"
                : $"revoke {grant.Privileges} from {grant.Role}";
            AppendDetail(sb, palette, grant.Kind, text);
        }
    }

    private static void RenderColumn(StringBuilder sb, Palette palette, ColumnDiff column)
    {
        if (column.Kind == ChangeKind.Add && column.Definition is { } added)
        {
            AppendDetail(sb, palette, ChangeKind.Add, FormatColumn(added) + CommentSuffix(column.Comment));
            return;
        }

        if (column.Kind == ChangeKind.Remove && column.Definition is { } removed)
        {
            AppendDetail(sb, palette, ChangeKind.Remove, FormatColumn(removed));
            return;
        }

        if (column.RenamedFrom is not null)
        {
            AppendDetail(sb, palette, ChangeKind.Modify, $"rename column: {column.RenamedFrom} → {column.Name}");
        }

        if (column.Type is { } type)
        {
            AppendDetail(sb, palette, ChangeKind.Modify, $"{column.Name} type: {type.Old} → {type.New}");
        }

        if (column.Nullability is { } nullable)
        {
            AppendDetail(sb, palette, ChangeKind.Modify, $"{column.Name} nullable: {FormatNullability(nullable.Old)} → {FormatNullability(nullable.New)}");
        }

        if (column.Default is { } @default)
        {
            AppendDetail(sb, palette, ChangeKind.Modify, $"{column.Name} default: {FormatDefault(@default.Old)} → {FormatDefault(@default.New)}");
        }

        if (column.Identity is { } identity)
        {
            AppendDetail(sb, palette, ChangeKind.Modify, $"{column.Name} identity: {FormatIdentity(identity.Old)} → {FormatIdentity(identity.New)}");
        }

        if (column.Comment is { } comment)
        {
            AppendDetail(sb, palette, ChangeKind.Modify, $"{column.Name} comment: {FormatComment(comment.Old)} → {FormatComment(comment.New)}");
        }
    }

    private static void RenderScripts(StringBuilder sb, string heading, IReadOnlyList<string> names)
    {
        if (names.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine(heading);
        foreach (var name in names)
        {
            sb.AppendLine($"  • {name}");
        }
    }

    private static void AppendHeader(StringBuilder sb, Palette palette, ChangeKind kind, string text)
    {
        sb.AppendLine();
        sb.Append(palette.For(kind)).Append(' ').AppendLine(text);
    }

    private static void AppendDetail(StringBuilder sb, Palette palette, ChangeKind kind, string text) =>
        sb.Append(Indent).Append(palette.For(kind)).Append(' ').AppendLine(text);

    // -------------------------------------------------------------------------
    // Formatters
    // -------------------------------------------------------------------------

    private static string FormatColumn(Column column) =>
        $"{column.Name} {column.Type} {(column.IsNullable ? "null" : "not null")}";

    private static string CommentSuffix(ValueChange<string>? comment) => comment is null
        ? string.Empty
        : comment.Old is null
            ? $" ({FormatComment(comment.New)})"
            : $" ({FormatComment(comment.Old)} → {FormatComment(comment.New)})";

    private static string FormatComment(string? comment) => comment is null ? "<none>" : $"\"{comment}\"";

    private static string FormatDefault(string? value) => value ?? "<none>";

    private static string FormatNullability(bool? nullable) => nullable == true ? "null" : "not null";

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

        public static Palette For(bool colour) => colour
            ? new Palette($"{Green}+{Reset}", $"{Red}-{Reset}", $"{Yellow}~{Reset}")
            : new Palette("+", "-", "~");
    }
}
