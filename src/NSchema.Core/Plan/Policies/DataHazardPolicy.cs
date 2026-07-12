using Microsoft.Extensions.Options;
using NSchema.Diff.Domain.Models;
using NSchema.Plan.Domain.Models;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Columns;

namespace NSchema.Plan.Policies;

/// <summary>
/// A plan policy that flags changes which are valid against the schema but can fail at apply time depending on the data already in the table.
/// </summary>
/// <remarks>
/// Examples of diagnostics: required columns added without a default, columns tightened to NOT NULL, type changes
/// whose cast can fail, and uniqueness added over pre-existing columns. Each hazard is reported as its own
/// diagnostic, at the severity configured by <see cref="DataHazardOptions"/>.
/// </remarks>
internal sealed class DataHazardPolicy(IOptions<DataHazardOptions> options) : IPlanPolicy
{
    private const string PolicyName = "data-hazards";

    public IEnumerable<Diagnostic> Validate(MigrationPlan plan)
    {
        var diff = plan.Diff;
        if (options.Value.Policy == PolicyEnforcement.Ignore)
        {
            return [];
        }

        var severity = options.Value.Policy switch
        {
            PolicyEnforcement.Allow => DiagnosticSeverity.Info,
            PolicyEnforcement.Warn => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Error,
        };

        return Hazards(diff).Select(message => new Diagnostic(PolicyName, message, severity));
    }

    private static IEnumerable<string> Hazards(DatabaseDiff diff)
    {
        // Only a modified table can hold data at apply time: an added table is empty and a removed one is gone,
        // so hazards exist nowhere else.
        foreach (var schema in diff.Schemas)
        {
            foreach (var table in schema.Tables.Where(t => t.Kind == ChangeKind.Modify))
            {
                foreach (var hazard in TableHazards(schema.Name, table))
                {
                    yield return hazard;
                }
            }
        }
    }

    private static IEnumerable<string> TableHazards(string schemaName, TableDiff table)
    {
        var qualified = $"{schemaName}.{table.Name}";

        foreach (var column in table.Columns)
        {
            var path = $"{qualified}.{column.Name}";

            // Identity and generated columns compute their own values for existing rows, so only a plain required column with no default can fail the add.
            // A matched backfill migration handles the transition (the planner decomposes the add around it), so it silences this hazard.
            if (column is { Kind: ChangeKind.Add, Definition: { IsNullable: false, DefaultExpression: null, IsIdentity: false, GeneratedExpression: null }, MigrationScript: null })
            {
                yield return $"Column '{path}' is added NOT NULL without a default; the migration will fail if the " +
                             "table holds rows. Declaring a DEFAULT is usually the whole fix — PostgreSQL 11+ fills " +
                             "existing rows from it without rewriting the table.";
            }

            if (column.Kind != ChangeKind.Modify)
            {
                continue;
            }

            if (column.Nullability is { New: false })
            {
                yield return $"Column '{path}' becomes NOT NULL; the migration will fail if existing rows hold " +
                             "NULLs. Backfill them first.";
            }

            if (column.Type is { Old: { } oldType, New: { } newType } && CanCastFail(oldType, newType) && column.MigrationScript is null)
            {
                yield return $"Column '{path}' changes type from {oldType} to {newType}; the cast will fail for " +
                             "existing values that do not fit the new type.";
            }
        }

        // Uniqueness added over columns the table already had can collide with existing rows. Columns added in
        // this same diff start empty, so uniqueness confined to them cannot.
        var addedColumns = table.Columns
            .Where(c => c.Kind == ChangeKind.Add)
            .Select(c => c.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A matched migration means the user has declared how the data gets into shape (de-duplicated, backfilled) before the constraint lands, so it silences the hazard.
        foreach (var primaryKey in table.PrimaryKey.Where(p => p is { Kind: ChangeKind.Add, MigrationScript: null }))
        {
            var existing = ExistingColumns(primaryKey.Definition?.ColumnNames, addedColumns);
            if (existing.Count > 0)
            {
                yield return $"Primary key '{primaryKey.Name}' on '{qualified}' is added over existing " +
                             $"{Columns(existing)}; the migration will fail if existing rows hold duplicates or NULLs.";
            }
        }

        foreach (var constraint in table.UniqueConstraints.Where(u => u is { Kind: ChangeKind.Add, MigrationScript: null }))
        {
            var existing = ExistingColumns(constraint.Definition?.ColumnNames, addedColumns);
            if (existing.Count > 0)
            {
                yield return $"Unique constraint '{constraint.Name}' on '{qualified}' is added over existing " +
                             $"{Columns(existing)}; the migration will fail if existing rows hold duplicates.";
            }
        }

        foreach (var index in table.Indexes.Where(i => i is { Kind: ChangeKind.Add, Definition.IsUnique: true }))
        {
            // An expression key is opaque, so it is assumed to read pre-existing data.
            if (index.Definition!.Columns.Any(k => k.IsExpression || !addedColumns.Contains(k.Expression)))
            {
                yield return $"Unique index '{index.Name}' on '{qualified}' is added over existing data; the " +
                             "migration will fail if existing rows hold duplicates.";
            }
        }
    }

    private static List<string> ExistingColumns(IReadOnlyList<string>? columnNames, IReadOnlySet<string> addedColumns) =>
        columnNames?.Where(c => !addedColumns.Contains(c)).ToList() ?? [];

    private static string Columns(List<string> names) =>
        (names.Count == 1 ? "column " : "columns ") + string.Join(", ", names.Select(n => $"'{n}'"));

    /// <summary>
    /// Whether altering a column from <paramref name="old"/> to <paramref name="new"/> can fail on values already
    /// stored. Only combinations the model has knowledge of are flagged; anything involving a custom type stays
    /// silent rather than guessing. Lossy-but-succeeding casts (rounding, truncation to a coarser date/time type)
    /// are not hazards — they are already surfaced as destructive type changes.
    /// </summary>
    private static bool CanCastFail(SqlType old, SqlType @new)
    {
        var from = FamilyOf(old);
        var to = FamilyOf(@new);
        if (from == TypeFamily.Unknown || to == TypeFamily.Unknown)
        {
            return false;
        }

        return (from, to) switch
        {
            (TypeFamily.String, TypeFamily.String) => Capacity(old) > Capacity(@new),
            (TypeFamily.Binary, TypeFamily.Binary) => Capacity(old) > Capacity(@new),
            (TypeFamily.String, _) => true, // parsing text into a structured type can fail on any malformed value
            (TypeFamily.Integer, TypeFamily.Integer) => IntegerRank(@new) < IntegerRank(old),
            (TypeFamily.Integer, TypeFamily.Decimal) => IntegerDigits(old) > WholeDigits(@new),
            (TypeFamily.Decimal, TypeFamily.Decimal) => WholeDigits(@new) < WholeDigits(old),
            (TypeFamily.Decimal, TypeFamily.Integer) => true,
            (TypeFamily.Float, TypeFamily.Integer) => true,
            (TypeFamily.Float, TypeFamily.Decimal) => true,
            (TypeFamily.Float, TypeFamily.Float) => old.Name == "double" && @new.Name == "float",
            _ => false,
        };
    }

    private enum TypeFamily { String, Binary, Integer, Decimal, Float, Other, Unknown }

    private static TypeFamily FamilyOf(SqlType type) => type.Name switch
    {
        "char" or "nchar" or "varchar" or "nvarchar" or "text" => TypeFamily.String,
        "binary" or "varbinary" => TypeFamily.Binary,
        "tinyint" or "smallint" or "int" or "bigint" => TypeFamily.Integer,
        "decimal" => TypeFamily.Decimal,
        "float" or "double" => TypeFamily.Float,
        "boolean" or "date" or "time" or "datetime" or "datetimeoffset" or "guid" => TypeFamily.Other,
        _ => TypeFamily.Unknown,
    };

    private static int Capacity(SqlType type) => type.Length ?? int.MaxValue;

    private static int IntegerRank(SqlType type) => type.Name switch
    {
        "tinyint" => 0,
        "smallint" => 1,
        "int" => 2,
        _ => 3,
    };

    /// <summary>The number of decimal digits needed to hold the integer type's full range.</summary>
    private static int IntegerDigits(SqlType type) => type.Name switch
    {
        "tinyint" => 3,
        "smallint" => 5,
        "int" => 10,
        _ => 19,
    };

    /// <summary>Digits available to the left of the decimal point; an unbounded decimal has no limit.</summary>
    private static int WholeDigits(SqlType type) =>
        type.Precision is { } precision ? precision - (type.Scale ?? 0) : int.MaxValue;
}
