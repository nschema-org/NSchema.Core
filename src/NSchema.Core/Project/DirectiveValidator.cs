using NSchema.Model;
using NSchema.Model.Services;
using NSchema.Project.Model.Directives;
using NSchema.Project.Model.Services;

namespace NSchema.Project;

/// <summary>
/// Validates the project's directives against its declarations.
/// </summary>
internal static class DirectiveValidator
{
    public static IEnumerable<Diagnostic> Validate(ProjectDefinition project)
    {
        var directives = project.Directives;
        var index = new DatabaseLookup(project.Database);

        // A current schema name maps to its declared name through the schema renames. First wins on a
        // duplicate source — the duplicate itself is reported below, and validation continues best-effort.
        var declaredNames = new Dictionary<SqlIdentifier, SqlIdentifier>();
        foreach (var rename in directives.Schemas.Renames)
        {
            declaredNames.TryAdd(rename.From, rename.To);
        }
        SqlIdentifier DeclaredSchema(SqlIdentifier currentSchema) => declaredNames.GetValueOrDefault(currentSchema, currentSchema);

        // ── Schema directives ─────────────────────────────────────────────────────────────
        foreach (var d in ValidateRenameShape("schema",
            directives.Schemas.Renames.Select(r => (Container: 0, r.From, r.To)).ToList(),
            (_, name) => name.Value))
        {
            yield return d;
        }

        foreach (var rename in directives.Schemas.Renames)
        {
            if (index.FindSchema(rename.To) is null)
            {
                yield return ProjectDiagnostics.RenameTargetNotDeclared("schema", rename.From.Value, rename.To);
            }
            if (index.FindSchema(rename.From) is not null)
            {
                yield return ProjectDiagnostics.RenameSourceStillDeclared("schema", rename.From.Value, rename.To);
            }
            if (directives.Schemas.Drops.Any(d => d.Name == rename.From))
            {
                yield return ProjectDiagnostics.RenameOfDropped("schema", rename.From.Value);
            }
        }

        foreach (var drop in directives.Schemas.Drops)
        {
            if (index.FindSchema(drop.Name) is not null)
            {
                yield return ProjectDiagnostics.DropOfDeclared("schema", drop.Name.Value);
            }
        }

        foreach (var partial in directives.Schemas.Partials)
        {
            if (index.FindSchema(partial.Schema) is null)
            {
                yield return ProjectDiagnostics.DirectiveSchemaNotDeclared($"PARTIAL SCHEMA {partial.Schema}", partial.Schema);
            }
        }

        // ── Object directives, one uniform rule set per kind ─────────────────────────────
        foreach (var kind in Enum.GetValues<ObjectKind>())
        {
            var kindName = kind.Display();
            var renames = directives.Renames.Where(r => r.Kind == kind).ToList();
            var drops = directives.Drops.Where(d => d.Kind == kind).ToList();

            foreach (var d in ValidateRenameShape(kindName,
                renames.Select(r => (Container: r.From.Schema, From: r.From.Name, r.To)).ToList(),
                (schema, name) => $"{schema}.{name}"))
            {
                yield return d;
            }

            foreach (var rename in renames)
            {
                var declaredSchema = DeclaredSchema(rename.From.Schema);
                if (index.FindSchema(declaredSchema) is null)
                {
                    yield return ProjectDiagnostics.DirectiveSchemaNotDeclared($"RENAME of {kindName} '{rename.From}'", rename.From.Schema);
                }
                else if (!index.Has(kind, new ObjectReference(declaredSchema, rename.To)))
                {
                    yield return ProjectDiagnostics.RenameTargetNotDeclared(kindName, rename.From.ToString(), rename.To);
                }
                if (index.Has(kind, new ObjectReference(declaredSchema, rename.From.Name)))
                {
                    yield return ProjectDiagnostics.RenameSourceStillDeclared(kindName, rename.From.ToString(), rename.To);
                }
                if (drops.Any(d => d.Address == rename.From))
                {
                    yield return ProjectDiagnostics.RenameOfDropped(kindName, rename.From.ToString());
                }
            }

            foreach (var drop in drops)
            {
                var declaredSchema = DeclaredSchema(drop.Address.Schema);
                if (index.FindSchema(declaredSchema) is null)
                {
                    yield return ProjectDiagnostics.DirectiveSchemaNotDeclared($"DROP of {kindName} '{drop.Address}'", drop.Address.Schema);
                }
                else if (index.Has(kind, new ObjectReference(declaredSchema, drop.Address.Name)))
                {
                    yield return ProjectDiagnostics.DropOfDeclared(kindName, drop.Address.ToString());
                }
            }
        }

        // ── Column renames ────────────────────────────────────────────────────────────────
        foreach (var d in ValidateRenameShape("column",
            directives.ColumnRenames.Select(r => (Container: (r.From.Schema, r.From.Object), From: r.From.Member, r.To)).ToList(),
            (table, name) => $"{table.Item1}.{table.Item2}.{name}"))
        {
            yield return d;
        }

        var tableRenames = new Dictionary<ObjectReference, SqlIdentifier>();
        foreach (var rename in directives.Renames.Where(r => r.Kind == ObjectKind.Table))
        {
            tableRenames.TryAdd(rename.From, rename.To);
        }
        foreach (var rename in directives.ColumnRenames)
        {
            // The path names current reality: resolve the schema and the table through their own renames to
            // find the declared table the column must be declared on.
            var declaredSchema = DeclaredSchema(rename.From.Schema);
            var declaredTable = tableRenames.GetValueOrDefault(new ObjectReference(rename.From.Schema, rename.From.Object), rename.From.Object);
            if (index.FindTable(new ObjectReference(declaredSchema, declaredTable)) is not { } table)
            {
                yield return ProjectDiagnostics.DirectiveTableNotDeclared(rename.From);
                continue;
            }
            if (table.Columns.All(c => c.Name != rename.To))
            {
                yield return ProjectDiagnostics.RenameTargetNotDeclared("column", rename.From.ToString(), rename.To);
            }
            if (table.Columns.Any(c => c.Name == rename.From.Member))
            {
                yield return ProjectDiagnostics.RenameSourceStillDeclared("column", rename.From.ToString(), rename.To);
            }
        }
    }

    /// <summary>
    /// The container-independent rename rules: no self-renames, no two renames sharing a source or a target,
    /// and no chains (one rename's target being another's source is unordered and therefore ambiguous).
    /// Containers partition the checks — the same bare names in different containers never interact.
    /// </summary>
    private static IEnumerable<Diagnostic> ValidateRenameShape<TContainer>(
        string kind,
        IReadOnlyList<(TContainer Container, SqlIdentifier From, SqlIdentifier To)> renames,
        Func<TContainer, SqlIdentifier, string> display
    ) where TContainer : notnull
    {
        for (var i = 0; i < renames.Count; i++)
        {
            var (container, from, to) = renames[i];
            if (from == to)
            {
                yield return ProjectDiagnostics.SelfRename(kind, display(container, from));
            }

            for (var j = i + 1; j < renames.Count; j++)
            {
                if (!EqualityComparer<TContainer>.Default.Equals(container, renames[j].Container))
                {
                    continue;
                }

                var (_, otherFrom, otherTo) = renames[j];
                if (from == otherFrom)
                {
                    yield return ProjectDiagnostics.DuplicateRenameSource(kind, display(container, from));
                }
                else if (to == otherTo)
                {
                    yield return ProjectDiagnostics.DuplicateRenameTarget(kind, display(container, to));
                }
                else if (to == otherFrom || from == otherTo)
                {
                    yield return ProjectDiagnostics.RenameChain(kind, display(container, to == otherFrom ? to : from));
                }
            }
        }
    }
}
