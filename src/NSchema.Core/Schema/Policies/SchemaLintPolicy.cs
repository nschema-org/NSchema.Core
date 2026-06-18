using NSchema.Policies;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Tables;

namespace NSchema.Schema.Policies;

/// <summary>
/// Reports likely schema mistakes that are still valid DDL:
/// - tables without a primary key,
/// - nullable columns used in a primary key,
/// - and columns repeated within a single key or index.
/// These are advisory, so they are reported as <see cref="PolicyDiagnosticSeverity.Warning"/> and do not fail validation.
/// </summary>
public sealed class SchemaLintPolicy : ISchemaPolicy
{
    private const string PolicyName = "schema-lint";

    /// <inheritdoc />
    public IEnumerable<PolicyDiagnostic> Validate(DatabaseSchema schema)
    {
        var diagnostics = new List<PolicyDiagnostic>();
        foreach (var definition in schema.Schemas)
        {
            foreach (var table in definition.Tables)
            {
                ValidateTable(definition, table, diagnostics);
            }
        }

        return diagnostics;
    }

    private static void ValidateTable(SchemaDefinition definition, Table table, List<PolicyDiagnostic> diagnostics)
    {
        var qualified = $"{definition.Name}.{table.Name}";

        if (table.PrimaryKey is not { } primaryKey)
        {
            diagnostics.Add(Warning($"Table '{qualified}' has no primary key."));
        }
        else
        {
            var nullableColumns = new HashSet<string>(table.Columns.Where(c => c.IsNullable).Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var column in primaryKey.ColumnNames.Where(nullableColumns.Contains))
            {
                diagnostics.Add(Warning($"Column '{column}' on '{qualified}' is part of the primary key but is declared nullable; it will be forced NOT NULL."));
            }

            ReportDuplicates(diagnostics, qualified, $"primary key '{primaryKey.Name}'", primaryKey.ColumnNames);
        }

        foreach (var index in table.Indexes)
        {
            ReportDuplicates(diagnostics, qualified, $"index '{index.Name}'", index.ColumnNames);
        }

        foreach (var foreignKey in table.ForeignKeys)
        {
            ReportDuplicates(diagnostics, qualified, $"foreign key '{foreignKey.Name}'", foreignKey.ColumnNames);
        }
    }

    private static void ReportDuplicates(
        List<PolicyDiagnostic> diagnostics, string qualified, string owner, IEnumerable<string> columnNames)
    {
        var duplicates = columnNames
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicate in duplicates)
        {
            diagnostics.Add(Warning($"The {owner} on '{qualified}' lists column '{duplicate}' more than once."));
        }
    }

    private static PolicyDiagnostic Warning(string message) => PolicyDiagnostic.Warning(PolicyName, message);
}
