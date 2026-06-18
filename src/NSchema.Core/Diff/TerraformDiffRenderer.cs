using System.Text;
using Microsoft.Extensions.Options;
using NSchema.Diff.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Routines;
using NSchema.Schema.Model.Sequences;
using NSchema.Schema.Model.Tables;

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

        // Extensions are database-global, so they render at the root before any schema.
        foreach (var extension in diff.Extensions)
        {
            RenderExtension(sb, extension);
        }

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

            foreach (var view in schema.Views)
            {
                RenderView(sb, view);
            }

            foreach (var enumDiff in schema.Enums)
            {
                RenderEnum(sb, enumDiff);
            }

            foreach (var sequence in schema.Sequences)
            {
                RenderSequence(sb, sequence);
            }

            foreach (var routine in schema.Routines)
            {
                RenderRoutine(sb, routine);
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
        var hasTrailingBlock = table.PrimaryKey.Count > 0 || table.ForeignKeys.Count > 0
            || table.UniqueConstraints.Count > 0 || table.Checks.Count > 0
            || table.Indexes.Count > 0 || table.Triggers.Count > 0 || table.Grants.Count > 0;
        if (table is { Kind: ChangeKind.Add, Columns.Count: > 0 } && hasTrailingBlock)
        {
            sb.AppendLine();
        }

        foreach (var pk in table.PrimaryKey)
        {
            AppendDetail(sb, pk.Kind, ConstraintText("primary key", pk.Name, pk.Kind, pk.Comment));
        }

        foreach (var fk in table.ForeignKeys)
        {
            AppendDetail(sb, fk.Kind, ConstraintText("foreign key", fk.Name, fk.Kind, fk.Comment));
        }

        foreach (var unique in table.UniqueConstraints)
        {
            AppendDetail(sb, unique.Kind, ConstraintText("unique constraint", unique.Name, unique.Kind, unique.Comment));
        }

        foreach (var check in table.Checks)
        {
            AppendDetail(sb, check.Kind, ConstraintText("check constraint", check.Name, check.Kind, check.Comment));
        }

        foreach (var index in table.Indexes)
        {
            var text = index.Kind == ChangeKind.Modify
                ? $"index {index.Name} comment: {FormatComment(index.Comment?.Old)} → {FormatComment(index.Comment?.New)}"
                : $"index {index.Name}";
            AppendDetail(sb, index.Kind, text);
        }

        foreach (var trigger in table.Triggers)
        {
            var text = trigger.Kind == ChangeKind.Modify
                ? $"trigger {trigger.Name} comment: {FormatComment(trigger.Comment?.Old)} → {FormatComment(trigger.Comment?.New)}"
                : $"trigger {trigger.Name}";
            AppendDetail(sb, trigger.Kind, text);
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

    private void RenderView(StringBuilder sb, ViewDiff view)
    {
        var label = view.IsMaterialized ? "materialized view" : "view";
        var name = view.RenamedFrom is null
            ? $"{view.Schema}.{view.Name}"
            : $"{view.Schema}.{view.RenamedFrom} → {view.Name}";

        // A comment-only modify (no body or index change) reports the comment transition rather than a bare header.
        if (view is { Kind: ChangeKind.Modify, Definition: null, RenamedFrom: null, Comment: { } only } && view.Indexes.Count == 0)
        {
            AppendHeader(sb, ChangeKind.Modify, $"{label} {name} comment: {FormatComment(only.Old)} → {FormatComment(only.New)}");
            return;
        }

        AppendHeader(sb, view.Kind, $"{label} {name}{CommentSuffix(view.Comment)}");

        // In-place index changes on a materialized view.
        foreach (var index in view.Indexes)
        {
            var text = index.Kind == ChangeKind.Modify
                ? $"index {index.Name} comment: {FormatComment(index.Comment?.Old)} → {FormatComment(index.Comment?.New)}"
                : $"index {index.Name}";
            AppendDetail(sb, index.Kind, text);
        }
    }

    private void RenderEnum(StringBuilder sb, EnumDiff enumDiff)
    {
        var name = enumDiff.RenamedFrom is null
            ? $"{enumDiff.Schema}.{enumDiff.Name}"
            : $"{enumDiff.Schema}.{enumDiff.RenamedFrom} → {enumDiff.Name}";

        // A comment-only modify reports the comment transition rather than a bare header.
        if (enumDiff is { Kind: ChangeKind.Modify, Values: null, RenamedFrom: null, Comment: { } only })
        {
            AppendHeader(sb, ChangeKind.Modify, $"enum {name} comment: {FormatComment(only.Old)} → {FormatComment(only.New)}");
            return;
        }

        var values = enumDiff is { Kind: ChangeKind.Add, Definition: { } definition }
            ? $" ({string.Join(", ", definition.Values)})"
            : string.Empty;
        AppendHeader(sb, enumDiff.Kind, $"enum {name}{values}{CommentSuffix(enumDiff.Comment)}");

        foreach (var addition in enumDiff.AddedValues)
        {
            var anchor = addition switch
            {
                { After: { } after } => $" (after '{after}')",
                { Before: { } before } => $" (before '{before}')",
                _ => " (append)",
            };
            AppendDetail(sb, ChangeKind.Add, $"value '{addition.Value}'{anchor}");
        }

        if (enumDiff.RequiresRecreate)
        {
            AppendDetail(sb, ChangeKind.Modify,
                $"values: [{string.Join(", ", enumDiff.Values!.Old ?? [])}] → [{string.Join(", ", enumDiff.Values!.New ?? [])}]"
                + " (removal/reorder; requires manual recreate)");
        }
    }

    private void RenderSequence(StringBuilder sb, SequenceDiff sequence)
    {
        var name = sequence.RenamedFrom is null
            ? $"{sequence.Schema}.{sequence.Name}"
            : $"{sequence.Schema}.{sequence.RenamedFrom} → {sequence.Name}";

        // A comment-only modify reports the comment transition rather than a bare header.
        if (sequence is { Kind: ChangeKind.Modify, Options: null, RenamedFrom: null, Comment: { } only })
        {
            AppendHeader(sb, ChangeKind.Modify, $"sequence {name} comment: {FormatComment(only.Old)} → {FormatComment(only.New)}");
            return;
        }

        var options = sequence is { Kind: ChangeKind.Add, Definition: { } definition }
            && FormatSequenceOptions(definition.Options) != "<default>"
            ? $" ({FormatSequenceOptions(definition.Options)})"
            : string.Empty;
        AppendHeader(sb, sequence.Kind, $"sequence {name}{options}{CommentSuffix(sequence.Comment)}");

        if (sequence.Options is { } change)
        {
            AppendDetail(sb, ChangeKind.Modify, $"options: {FormatSequenceOptions(change.Old)} → {FormatSequenceOptions(change.New)}");
        }
    }

    private void RenderRoutine(StringBuilder sb, RoutineDiff routine)
    {
        var label = routine.RoutineKind == RoutineKind.Procedure ? "procedure" : "function";
        var name = routine.RenamedFrom is null
            ? $"{routine.Schema}.{routine.Name}"
            : $"{routine.Schema}.{routine.RenamedFrom} → {routine.Name}";

        // A comment-only modify reports the comment transition rather than a bare header.
        if (routine is { Kind: ChangeKind.Modify, Definition: null, RenamedFrom: null, Comment: { } only })
        {
            AppendHeader(sb, ChangeKind.Modify, $"{label} {name} comment: {FormatComment(only.Old)} → {FormatComment(only.New)}");
            return;
        }

        var arguments = routine is { Kind: ChangeKind.Add, Definition: { } definition } ? $"({definition.Arguments})" : string.Empty;
        AppendHeader(sb, routine.Kind, $"{label} {name}{arguments}{CommentSuffix(routine.Comment)}");

        if (routine.Arguments is { } change)
        {
            AppendDetail(sb, ChangeKind.Modify, $"arguments: ({change.Old}) → ({change.New}) (recreate)");
        }
    }

    private void RenderExtension(StringBuilder sb, ExtensionDiff extension)
    {
        // A comment-only modify reports the comment transition rather than a bare header.
        if (extension is { Kind: ChangeKind.Modify, Version: null, Comment: { } only })
        {
            AppendHeader(sb, ChangeKind.Modify, $"extension {extension.Name} comment: {FormatComment(only.Old)} → {FormatComment(only.New)}");
            return;
        }

        var version = extension is { Kind: ChangeKind.Add, Definition.Version: { } added } ? $" version {added}" : string.Empty;
        AppendHeader(sb, extension.Kind, $"extension {extension.Name}{version}{CommentSuffix(extension.Comment)}");

        if (extension.Version is { } change)
        {
            AppendDetail(sb, ChangeKind.Modify, $"version: {FormatVersion(change.Old)} → {FormatVersion(change.New)}");
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

    private static string FormatVersion(string? version) => version ?? "<default>";

    // A constraint Modify is always a comment-only change (structural changes are Remove + Add).
    private string ConstraintText(string label, string name, ChangeKind kind, ValueChange<string>? comment) =>
        kind == ChangeKind.Modify
            ? $"{label} {name} comment: {FormatComment(comment?.Old)} → {FormatComment(comment?.New)}"
            : $"{label} {name}";

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

    private static string FormatSequenceOptions(SequenceOptions? options)
    {
        if (options is null)
        {
            return "<none>";
        }

        var parts = new List<string>(7);
        if (options.DataType is { } type)
        {
            parts.Add($"as={type}");
        }

        if (options.StartWith.HasValue)
        {
            parts.Add($"start={options.StartWith}");
        }

        if (options.IncrementBy.HasValue)
        {
            parts.Add($"step={options.IncrementBy}");
        }

        if (options.MinValue.HasValue)
        {
            parts.Add($"min={options.MinValue}");
        }

        if (options.MaxValue.HasValue)
        {
            parts.Add($"max={options.MaxValue}");
        }

        if (options.Cache.HasValue)
        {
            parts.Add($"cache={options.Cache}");
        }

        if (options.Cycle)
        {
            parts.Add("cycle");
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
