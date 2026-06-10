using Microsoft.Extensions.Logging;
using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

/// <summary>
/// Produces the structured <see cref="DatabaseDiff"/> from a current and desired schema.
/// </summary>
internal sealed partial class SchemaComparer(ILogger<SchemaComparer> logger) : ISchemaComparer
{
    public DatabaseDiff Compare(DatabaseSchema current, DatabaseSchema desired)
    {
        LogBeginningComparison();

        var schemas = CompareSchemas(current.Schemas, desired.Schemas);

        LogComparisonComplete(schemas.Count);

        return new DatabaseDiff(schemas);
    }

    private List<SchemaDiff> CompareSchemas(IReadOnlyList<SchemaDefinition> current, IReadOnlyList<SchemaDefinition> desired)
    {
        var result = new List<SchemaDiff>();
        var (forDesired, currentMatched) = MatchEntities(current, desired, s => s.Name, s => s.OldName, "schema");

        for (var j = 0; j < current.Count; j++)
        {
            if (currentMatched[j])
            {
                LogSchemaExists(current[j].Name);
            }
            else
            {
                LogSchemaNotInDesired(current[j].Name);
                result.Add(new SchemaDiff(current[j].Name, ChangeKind.Remove, null, null, [], []));
            }
        }

        for (var i = 0; i < desired.Count; i++)
        {
            if (forDesired[i] is not { } matchingCurrent)
            {
                LogSchemaNew(desired[i].Name);
                result.Add(BuildNewSchema(desired[i]));
            }
            else if (BuildModifiedSchema(matchingCurrent, desired[i]) is { } diff)
            {
                result.Add(diff);
            }
        }

        // The diff presents schemas ordered by name (tables likewise, within each schema).
        result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return result;
    }

    /// <summary>
    /// Pairs each current entity with at most one desired entity. Renames (an explicit <c>OldName</c>) are
    /// matched first, then remaining desired entities claim a current entity by exact name.
    /// </summary>
    /// <remarks>
    /// A rename is rejected when it collides with a surviving entity, because the result cannot be ordered
    /// safely and is indistinguishable from an add/drop pair: either the previous name is still declared in
    /// the desired set (so we cannot tell a rename from a retain-plus-create), or the new name is already
    /// taken by another current entity. The user must split the rename and the conflicting change into
    /// separate migrations.
    /// </remarks>
    /// <param name="current">The current-state entities.</param>
    /// <param name="desired">The desired-state entities.</param>
    /// <param name="name">Projects an entity's name.</param>
    /// <param name="oldName">Projects an entity's previous name (its rename source), or <c>null</c>.</param>
    /// <param name="entityKind">The noun used in the ambiguity error (e.g. <c>"table"</c>).</param>
    /// <param name="container">An optional qualifier for the entity in the error (e.g. the schema name).</param>
    /// <returns>
    /// <c>ForDesired[i]</c> is the current entity matched to <c>desired[i]</c>, or <c>null</c> if it is new;
    /// <c>CurrentMatched[j]</c> is whether <c>current[j]</c> was claimed (otherwise it has been removed).
    /// </returns>
    private static (T?[] ForDesired, bool[] CurrentMatched) MatchEntities<T>(
        IReadOnlyList<T> current,
        IReadOnlyList<T> desired,
        Func<T, string> name,
        Func<T, string?> oldName,
        string entityKind,
        string? container = null
    ) where T : class
    {
        var forDesired = new T?[desired.Count];
        var currentMatched = new bool[current.Count];
        var matchedByRename = new bool[desired.Count];

        // Pass 1: explicit renames take priority.
        for (var i = 0; i < desired.Count; i++)
        {
            if (oldName(desired[i]) is not { } renamedFrom)
            {
                continue;
            }

            for (var j = 0; j < current.Count; j++)
            {
                if (!currentMatched[j] && name(current[j]) == renamedFrom)
                {
                    forDesired[i] = current[j];
                    currentMatched[j] = true;
                    matchedByRename[i] = true;
                    break;
                }
            }
        }

        // Pass 2: exact name matches for whatever current entities remain unclaimed.
        for (var i = 0; i < desired.Count; i++)
        {
            if (forDesired[i] is not null)
            {
                continue;
            }

            for (var j = 0; j < current.Count; j++)
            {
                if (!currentMatched[j] && name(current[j]) == name(desired[i]))
                {
                    forDesired[i] = current[j];
                    currentMatched[j] = true;
                    break;
                }
            }
        }

        // Reject renames that collide with a surviving entity (see remarks).
        for (var i = 0; i < desired.Count; i++)
        {
            if (!matchedByRename[i])
            {
                continue;
            }

            var newName = name(desired[i]);
            var renamedFrom = oldName(desired[i])!;
            var index = i;

            var oldNameStillDeclared = desired.Where((_, d) => d != index).Any(d => name(d) == renamedFrom);
            var newNameAlreadyTaken = current.Any(c => name(c) == newName && !ReferenceEquals(c, forDesired[index]));

            if (oldNameStillDeclared || newNameAlreadyTaken)
            {
                var qualified = container is null ? newName : $"{container}.{newName}";
                var reason = oldNameStillDeclared
                    ? $"its previous name '{renamedFrom}' is still declared"
                    : $"a {entityKind} named '{newName}' already exists";
                throw new InvalidOperationException(
                    $"Ambiguous rename of {entityKind} '{qualified}' from '{renamedFrom}': {reason}. " +
                    "Perform the rename and the conflicting change in separate migrations.");
            }
        }

        return (forDesired, currentMatched);
    }

    private SchemaDiff BuildNewSchema(SchemaDefinition desired)
    {
        var tables = desired.Tables
            .Select(table => BuildNewTable(desired.Name, table))
            .OrderBy(table => table.Name, StringComparer.Ordinal)
            .ToList();

        var views = desired.Views
            .Select(view => BuildNewView(desired.Name, view))
            .OrderBy(view => view.Name, StringComparer.Ordinal)
            .ToList();

        var enums = desired.Enums
            .Select(enumType => BuildNewEnum(desired.Name, enumType))
            .OrderBy(enumType => enumType.Name, StringComparer.Ordinal)
            .ToList();

        var sequences = desired.Sequences
            .Select(sequence => BuildNewSequence(desired.Name, sequence))
            .OrderBy(sequence => sequence.Name, StringComparer.Ordinal)
            .ToList();

        var functions = desired.Functions
            .Select(function => BuildNewFunction(desired.Name, function))
            .OrderBy(function => function.Name, StringComparer.Ordinal)
            .ToList();

        var procedures = desired.Procedures
            .Select(procedure => BuildNewProcedure(desired.Name, procedure))
            .OrderBy(procedure => procedure.Name, StringComparer.Ordinal)
            .ToList();

        var comment = desired.Comment is not null ? new ValueChange<string>(null, desired.Comment) : null;
        var grants = desired.Grants.Select(grant => new GrantChange(ChangeKind.Add, grant.Role, null)).ToList();

        return new SchemaDiff(desired.Name, ChangeKind.Add, null, comment, grants, tables, views, enums, sequences, functions, procedures);
    }

    private SchemaDiff? BuildModifiedSchema(SchemaDefinition current, SchemaDefinition desired)
    {
        string? renamedFrom = null;
        if (current.Name == desired.Name)
        {
            LogSchemaUnchanged(desired.Name);
        }
        else
        {
            LogSchemaRenamed(current.Name, desired.Name);
            renamedFrom = current.Name;
        }

        ValueChange<string>? comment = null;
        if (current.Comment != desired.Comment)
        {
            LogSchemaCommentChanged(desired.Name);
            comment = new ValueChange<string>(current.Comment, desired.Comment);
        }

        var grants = CompareSchemaGrants(desired.Name, current.Grants, desired.Grants);
        var tables = CompareTables(desired.Name, current.Tables, desired)
            .OrderBy(table => table.Name, StringComparer.Ordinal)
            .ToList();
        var views = CompareViews(desired.Name, current.Views, desired)
            .OrderBy(view => view.Name, StringComparer.Ordinal)
            .ToList();
        var enums = CompareEnums(desired.Name, current.Enums, desired)
            .OrderBy(enumType => enumType.Name, StringComparer.Ordinal)
            .ToList();
        var sequences = CompareSequences(desired.Name, current.Sequences, desired)
            .OrderBy(sequence => sequence.Name, StringComparer.Ordinal)
            .ToList();
        var functions = CompareFunctions(desired.Name, current.Functions, desired)
            .OrderBy(function => function.Name, StringComparer.Ordinal)
            .ToList();
        var procedures = CompareProcedures(desired.Name, current.Procedures, desired)
            .OrderBy(procedure => procedure.Name, StringComparer.Ordinal)
            .ToList();

        // The schema entity itself only changes when it is renamed or its comment/grants change; a schema that
        // merely contains changed tables or views has a null Kind.
        var schemaLevelChange = renamedFrom is not null || comment is not null || grants.Count > 0;
        if (!schemaLevelChange && tables.Count == 0 && views.Count == 0 && enums.Count == 0 && sequences.Count == 0
            && functions.Count == 0 && procedures.Count == 0)
        {
            return null;
        }

        return new SchemaDiff(desired.Name, schemaLevelChange ? ChangeKind.Modify : null, renamedFrom, comment, grants, tables, views, enums, sequences, functions, procedures);
    }
}
