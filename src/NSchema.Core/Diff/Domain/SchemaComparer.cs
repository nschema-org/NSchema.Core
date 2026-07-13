using Microsoft.Extensions.Logging;
using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Schemas;

namespace NSchema.Diff.Domain;

/// <summary>
/// Produces the structured <see cref="DatabaseDiff"/> from a current and desired schema.
/// </summary>
internal sealed partial class SchemaComparer(ILogger<SchemaComparer> logger) : ISchemaComparer
{
    public DatabaseDiff Compare(DatabaseSchema current, DatabaseSchema desired)
    {
        LogBeginningComparison();

        var schemas = CompareSchemas(current.Schemas, desired.Schemas);
        var extensions = CompareExtensions(current.Extensions, desired.Extensions, desired.DroppedExtensions);

        LogComparisonComplete(schemas.Count);

        return new DatabaseDiff(schemas, extensions);
    }

    private List<SchemaDiff> CompareSchemas(IReadOnlyList<SchemaDefinition> current, IReadOnlyList<SchemaDefinition> desired)
    {
        var result = new List<SchemaDiff>();
        var (forDesired, currentMatched) = MatchEntities(current, desired, "schema");

        for (var j = 0; j < current.Count; j++)
        {
            if (currentMatched[j])
            {
                LogSchemaExists(current[j].Name);
            }
            else
            {
                LogSchemaNotInDesired(current[j].Name);
                result.Add(BuildRemovedSchema(current[j]));
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
        result.Sort((a, b) => a.Name.CompareTo(b.Name));
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
    /// <param name="entityKind">The noun used in the ambiguity error (e.g. <c>"table"</c>).</param>
    /// <param name="container">An optional qualifier for the entity in the error (e.g. the schema name).</param>
    /// <returns>
    /// <c>ForDesired[i]</c> is the current entity matched to <c>desired[i]</c>, or <c>null</c> if it is new;
    /// <c>CurrentMatched[j]</c> is whether <c>current[j]</c> was claimed (otherwise it has been removed).
    /// </returns>
    private static (T?[] ForDesired, bool[] CurrentMatched) MatchEntities<T>(
        IReadOnlyList<T> current,
        IReadOnlyList<T> desired,
        string entityKind,
        string? container = null
    ) where T : class, IRenameableObject
    {
        var forDesired = new T?[desired.Count];
        var currentMatched = new bool[current.Count];
        var matchedByRename = new bool[desired.Count];

        // Pass 1: explicit renames take priority.
        for (var i = 0; i < desired.Count; i++)
        {
            if (desired[i].OldName is not { } renamedFrom)
            {
                continue;
            }

            for (var j = 0; j < current.Count; j++)
            {
                if (!currentMatched[j] && current[j].Name == renamedFrom)
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
                if (!currentMatched[j] && current[j].Name == desired[i].Name)
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

            var newName = desired[i].Name;
            var renamedFrom = desired[i].OldName!;
            var index = i;

            var oldNameStillDeclared = desired.Where((_, d) => d != index).Any(d => d.Name == renamedFrom);
            var newNameAlreadyTaken = current.Any(c => c.Name == newName && !ReferenceEquals(c, forDesired[index]));

            if (oldNameStillDeclared || newNameAlreadyTaken)
            {
                var qualified = container is null ? newName.Value : $"{container}.{newName}";
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

    /// <summary>
    /// The shared per-kind diffing skeleton: pairs current and desired objects via
    /// <see cref="MatchEntities{T}"/>, treats an unmatched current object as removed — unless the schema is
    /// partial and the object was not explicitly dropped, mirroring how unmanaged tables are left alone — and
    /// builds an add for each unmatched desired object or delegates to <paramref name="buildModified"/> for a
    /// pair. Only the per-kind build logic varies; the matching and partial-schema semantics live here, once.
    /// </summary>
    private static List<TDiff> CompareObjects<TModel, TDiff>(
        SqlIdentifier schemaName,
        string entityKind,
        IReadOnlyList<TModel> current,
        IReadOnlyList<TModel> desired,
        IReadOnlyList<SqlIdentifier> droppedNames,
        bool isPartial,
        Func<TModel, TDiff> buildRemoved,
        Func<TModel, TDiff> buildNew,
        Func<TModel, TModel, TDiff?> buildModified
    ) where TModel : class, IRenameableObject where TDiff : class
    {
        var result = new List<TDiff>();
        var (forDesired, currentMatched) = MatchEntities(current, desired, entityKind, schemaName.Value);

        for (var j = 0; j < current.Count; j++)
        {
            if (currentMatched[j])
            {
                continue;
            }

            if (droppedNames.Contains(current[j].Name) || !isPartial)
            {
                result.Add(buildRemoved(current[j]));
            }
        }

        for (var i = 0; i < desired.Count; i++)
        {
            if (forDesired[i] is not { } matchingCurrent)
            {
                result.Add(buildNew(desired[i]));
            }
            else if (buildModified(matchingCurrent, desired[i]) is { } diff)
            {
                result.Add(diff);
            }
        }

        return result;
    }

    /// <summary>
    /// The shared diffing skeleton for a table's list members (foreign keys, unique and check constraints,
    /// indexes): match by exact name — members don't rename — then a structurally changed or missing member is
    /// removed, a changed or new one is added (with its comment folded in as a trailing Modify), and a
    /// comment-only change is a Modify in place. Relies on <typeparamref name="TModel"/>'s equality
    /// <em>excluding</em> <see cref="INamedObject.Comment"/>, or the comment-only branch is unreachable.
    /// </summary>
    private List<TDiff> CompareTableMembers<TModel, TDiff>(
        ObjectReference owner,
        string memberKind,
        IReadOnlyList<TModel> current,
        IReadOnlyList<TModel> desired,
        Func<ChangeKind, SqlIdentifier, TModel?, ValueChange<string>?, TDiff> diff
    ) where TModel : class, INamedObject
    {
        var result = new List<TDiff>();

        foreach (var currentMember in current)
        {
            var matchingDesired = desired.FirstOrDefault(d => d.Name == currentMember.Name);
            if (matchingDesired is null || !currentMember.Equals(matchingDesired))
            {
                LogTableMemberMissingOrChanged(memberKind, currentMember.Name, owner);
                result.Add(diff(ChangeKind.Remove, currentMember.Name, null, null));
            }
            else if (currentMember.Comment != matchingDesired.Comment)
            {
                LogTableMemberCommentChanged(memberKind, currentMember.Name, owner);
                result.Add(diff(ChangeKind.Modify, currentMember.Name, null, new ValueChange<string>(currentMember.Comment, matchingDesired.Comment)));
            }
            else
            {
                LogTableMemberUnchanged(memberKind, currentMember.Name, owner);
            }
        }

        foreach (var desiredMember in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredMember.Name);
            if (matchingCurrent is null || !matchingCurrent.Equals(desiredMember))
            {
                LogTableMemberNewOrChanged(memberKind, desiredMember.Name, owner);
                result.Add(diff(ChangeKind.Add, desiredMember.Name, desiredMember, null));
                if (desiredMember.Comment is not null)
                {
                    result.Add(diff(ChangeKind.Modify, desiredMember.Name, null, new ValueChange<string>(null, desiredMember.Comment)));
                }
            }
            else
            {
                LogTableMemberUnchanged(memberKind, desiredMember.Name, owner);
            }
        }

        return result;
    }

    private SchemaDiff BuildNewSchema(SchemaDefinition desired)
    {
        var tables = desired.Tables
            .Select(table => BuildNewTable(desired.Name, table))
            .OrderBy(table => table.Name)
            .ToList();

        var views = desired.Views
            .Select(view => BuildNewView(desired.Name, view))
            .OrderBy(view => view.Name)
            .ToList();

        var enums = desired.Enums
            .Select(enumType => BuildNewEnum(desired.Name, enumType))
            .OrderBy(enumType => enumType.Name)
            .ToList();

        var sequences = desired.Sequences
            .Select(sequence => BuildNewSequence(desired.Name, sequence))
            .OrderBy(sequence => sequence.Name)
            .ToList();

        var routines = desired.Routines
            .Select(routine => BuildNewRoutine(desired.Name, routine))
            .OrderBy(routine => routine.Name)
            .ToList();

        var domains = desired.Domains
            .Select(domain => BuildNewDomain(desired.Name, domain))
            .OrderBy(domain => domain.Name)
            .ToList();

        var compositeTypes = desired.CompositeTypes
            .Select(type => BuildNewCompositeType(desired.Name, type))
            .OrderBy(type => type.Name)
            .ToList();

        var comment = desired.Comment is not null ? new ValueChange<string>(null, desired.Comment) : null;
        var grants = desired.Grants.Select(grant => new GrantChange(ChangeKind.Add, grant.Role, null)).ToList();

        return new SchemaDiff(desired.Name, ChangeKind.Add, null, comment, grants, tables, views, enums, sequences, routines, domains, compositeTypes);
    }

    // A removed schema takes all of its objects with it. Rather than lean on a provider-specific DROP SCHEMA CASCADE
    // (which Postgres has but SQL Server and SQLite do not), emit an explicit Remove for every contained object — by
    // diffing the schema against an empty one — so the linearizer drops the objects before the schema itself.
    private SchemaDiff BuildRemovedSchema(SchemaDefinition current)
    {
        var empty = new SchemaDefinition(current.Name);
        return new SchemaDiff(
            current.Name,
            ChangeKind.Remove,
            Tables: CompareTables(current.Name, current.Tables, empty).OrderBy(t => t.Name).ToList(),
            Views: CompareViews(current.Name, current.Views, empty).OrderBy(v => v.Name).ToList(),
            Enums: CompareEnums(current.Name, current.Enums, empty).OrderBy(e => e.Name).ToList(),
            Sequences: CompareSequences(current.Name, current.Sequences, empty).OrderBy(s => s.Name).ToList(),
            Routines: CompareRoutines(current.Name, current.Routines, empty).OrderBy(r => r.Name).ToList(),
            Domains: CompareDomains(current.Name, current.Domains, empty).OrderBy(d => d.Name).ToList(),
            CompositeTypes: CompareCompositeTypes(current.Name, current.CompositeTypes, empty).OrderBy(c => c.Name).ToList());
    }

    private SchemaDiff? BuildModifiedSchema(SchemaDefinition current, SchemaDefinition desired)
    {
        SqlIdentifier? renamedFrom = null;
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
            .OrderBy(table => table.Name)
            .ToList();
        var views = CompareViews(desired.Name, current.Views, desired)
            .OrderBy(view => view.Name)
            .ToList();
        var enums = CompareEnums(desired.Name, current.Enums, desired)
            .OrderBy(enumType => enumType.Name)
            .ToList();
        var sequences = CompareSequences(desired.Name, current.Sequences, desired)
            .OrderBy(sequence => sequence.Name)
            .ToList();
        var routines = CompareRoutines(desired.Name, current.Routines, desired)
            .OrderBy(routine => routine.Name)
            .ToList();
        var domains = CompareDomains(desired.Name, current.Domains, desired)
            .OrderBy(domain => domain.Name)
            .ToList();
        var compositeTypes = CompareCompositeTypes(desired.Name, current.CompositeTypes, desired)
            .OrderBy(type => type.Name)
            .ToList();

        // The schema entity itself only changes when it is renamed or its comment/grants change; a schema that
        // merely contains changed tables or views has a null Kind.
        var schemaLevelChange = renamedFrom is not null || comment is not null || grants.Count > 0;
        if (!schemaLevelChange && tables.Count == 0 && views.Count == 0 && enums.Count == 0 && sequences.Count == 0
            && routines.Count == 0 && domains.Count == 0 && compositeTypes.Count == 0)
        {
            return null;
        }

        return new SchemaDiff(desired.Name, schemaLevelChange ? ChangeKind.Modify : null, renamedFrom, comment, grants, tables, views, enums, sequences, routines, domains, compositeTypes);
    }
}
