using Microsoft.Extensions.Logging;
using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Tables;
using NSchema.Model;
using NSchema.Model.Schemas;

namespace NSchema.Diff.Model.Services;

/// <summary>
/// Produces the structured <see cref="DatabaseDiff"/> from a current and desired schema.
/// </summary>
internal sealed partial class DatabaseComparer(ILogger<DatabaseComparer> logger) : IDatabaseComparer
{
    public DatabaseDiff Compare(AlignedDatabase current, Database desired)
    {
        LogBeginningComparison();

        var schemas = CompareSchemas(current.Database.Schemas, desired.Schemas, current.Renames);
        var extensions = CompareExtensions(current.Database.Extensions, desired.Extensions);

        LogComparisonComplete(schemas.Count);

        return new DatabaseDiff(schemas, extensions);
    }

    private List<SchemaDiff> CompareSchemas(IReadOnlyList<Schema> current, IReadOnlyList<Schema> desired, RenameLog renames)
    {
        var result = new List<SchemaDiff>();
        var (forDesired, currentMatched) = MatchEntities(current, desired);

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
            else if (BuildModifiedSchema(matchingCurrent, desired[i], renames) is { } diff)
            {
                result.Add(diff);
            }
        }

        // The diff presents schemas ordered by name (tables likewise, within each schema).
        result.Sort((a, b) => a.Name.CompareTo(b.Name));
        return result;
    }

    /// <summary>
    /// Pairs each current entity with at most one desired entity by exact name. Renames were already rewritten
    /// by the alignment, so an unmatched current entity is simply removed and an unmatched desired one is new.
    /// </summary>
    /// <param name="current">The current-state entities.</param>
    /// <param name="desired">The desired-state entities.</param>
    /// <returns>
    /// <c>ForDesired[i]</c> is the current entity matched to <c>desired[i]</c>, or <c>null</c> if it is new;
    /// <c>CurrentMatched[j]</c> is whether <c>current[j]</c> was claimed (otherwise it has been removed).
    /// </returns>
    private static (T?[] ForDesired, bool[] CurrentMatched) MatchEntities<T>(
        IReadOnlyList<T> current,
        IReadOnlyList<T> desired
    ) where T : DatabaseElement
    {
        var forDesired = new T?[desired.Count];
        var currentMatched = new bool[current.Count];

        for (var i = 0; i < desired.Count; i++)
        {
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

        return (forDesired, currentMatched);
    }

    /// <summary>
    /// The shared per-kind diffing skeleton: pairs current and desired objects via
    /// <see cref="MatchEntities{T}"/>, treats an unmatched current object as removed, and builds an add for
    /// each unmatched desired object or delegates to <paramref name="buildModified"/> for a pair (passing the
    /// name the entity was renamed from, when the alignment moved it). Only the per-kind build logic varies;
    /// the matching lives here, once.
    /// </summary>
    private static List<TDiff> CompareObjects<TModel, TDiff>(
        IReadOnlyList<TModel> current,
        IReadOnlyList<TModel> desired,
        Func<SqlIdentifier, SqlIdentifier?> renamedFrom,
        Func<TModel, TDiff> buildRemoved,
        Func<TModel, TDiff> buildNew,
        Func<TModel, TModel, SqlIdentifier?, TDiff?> buildModified
    ) where TModel : DatabaseElement where TDiff : class
    {
        var result = new List<TDiff>();
        var (forDesired, currentMatched) = MatchEntities(current, desired);

        for (var j = 0; j < current.Count; j++)
        {
            if (!currentMatched[j])
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
            else if (buildModified(matchingCurrent, desired[i], renamedFrom(desired[i].Name)) is { } diff)
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
    /// <em>excluding</em> <see cref="NSchema.Model.DatabaseElement.Comment"/>, or the comment-only branch is unreachable.
    /// </summary>
    private List<TDiff> CompareTableMembers<TModel, TDiff>(
        ObjectAddress owner,
        string memberKind,
        IReadOnlyList<TModel> current,
        IReadOnlyList<TModel> desired,
        Func<ChangeKind, SqlIdentifier, TModel?, ValueChange<string>?, TDiff> diff
    ) where TModel : DatabaseElement
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

    private SchemaDiff BuildNewSchema(Schema desired)
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
    private SchemaDiff BuildRemovedSchema(Schema current)
    {
        var empty = new Schema(current.Name);
        return new SchemaDiff(
            current.Name,
            ChangeKind.Remove,
            Tables: CompareTables(current.Name, current.Tables, empty, RenameLog.Empty).OrderBy(t => t.Name).ToList(),
            Views: CompareViews(current.Name, current.Views, empty, RenameLog.Empty).OrderBy(v => v.Name).ToList(),
            Enums: CompareEnums(current.Name, current.Enums, empty, RenameLog.Empty).OrderBy(e => e.Name).ToList(),
            Sequences: CompareSequences(current.Name, current.Sequences, empty, RenameLog.Empty).OrderBy(s => s.Name).ToList(),
            Routines: CompareRoutines(current.Name, current.Routines, empty, RenameLog.Empty).OrderBy(r => r.Name).ToList(),
            Domains: CompareDomains(current.Name, current.Domains, empty, RenameLog.Empty).OrderBy(d => d.Name).ToList(),
            CompositeTypes: CompareCompositeTypes(current.Name, current.CompositeTypes, empty, RenameLog.Empty).OrderBy(c => c.Name).ToList());
    }

    private SchemaDiff? BuildModifiedSchema(Schema current, Schema desired, RenameLog renames)
    {
        var renamedFrom = renames.RenamedFrom(desired.Name);
        if (renamedFrom is null)
        {
            LogSchemaUnchanged(desired.Name);
        }
        else
        {
            LogSchemaRenamed(renamedFrom, desired.Name);
        }

        ValueChange<string>? comment = null;
        if (current.Comment != desired.Comment)
        {
            LogSchemaCommentChanged(desired.Name);
            comment = new ValueChange<string>(current.Comment, desired.Comment);
        }

        var grants = CompareSchemaGrants(desired.Name, current.Grants, desired.Grants);
        var tables = CompareTables(desired.Name, current.Tables, desired, renames)
            .OrderBy(table => table.Name)
            .ToList();
        var views = CompareViews(desired.Name, current.Views, desired, renames)
            .OrderBy(view => view.Name)
            .ToList();
        var enums = CompareEnums(desired.Name, current.Enums, desired, renames)
            .OrderBy(enumType => enumType.Name)
            .ToList();
        var sequences = CompareSequences(desired.Name, current.Sequences, desired, renames)
            .OrderBy(sequence => sequence.Name)
            .ToList();
        var routines = CompareRoutines(desired.Name, current.Routines, desired, renames)
            .OrderBy(routine => routine.Name)
            .ToList();
        var domains = CompareDomains(desired.Name, current.Domains, desired, renames)
            .OrderBy(domain => domain.Name)
            .ToList();
        var compositeTypes = CompareCompositeTypes(desired.Name, current.CompositeTypes, desired, renames)
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
