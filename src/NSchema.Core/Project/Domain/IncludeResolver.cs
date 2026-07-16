using NSchema.Model;
using NSchema.Model.Tables;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Templates;

namespace NSchema.Project.Domain;

/// <summary>
/// Resolves <c>INCLUDE name</c> members: merges each referenced table template's members into the table the
/// include was written in. Runs over the aggregate after schema-template application, so an instantiated table
/// can itself include a template. The template registry, the projected-member cache, and the accumulating
/// diagnostics are the resolver's own state rather than threaded arguments.
/// </summary>
/// <param name="templates">The project's template registry, shared with the expander.</param>
internal sealed class IncludeResolver(IReadOnlyDictionary<SqlIdentifier, TemplateStatement> templates)
{
    private readonly Dictionary<SqlIdentifier, Table> _memberCache = [];
    private readonly List<Diagnostic> _diagnostics = [];

    /// <summary>
    /// The schema name table-template members project against; foreign keys referencing it re-point at the
    /// including table's schema when the include merges.
    /// </summary>
    private static readonly SqlIdentifier IncludePlaceholder = SchemaToken.TargetSchemaPlaceholder;

    /// <summary>
    /// The findings accumulated while resolving.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public Database Resolve(Database database, IReadOnlyList<TemplateInclude> includes)
    {
        if (includes.Count == 0)
        {
            return database;
        }

        var byTable = includes
            .GroupBy(i => (i.SchemaName, i.TableName))
            .ToDictionary(g => g.Key, g => g.ToList());

        var consumed = new HashSet<(SqlIdentifier Schema, SqlIdentifier Table)>();
        var resolved = database.Schemas
            .Select(definition => definition with
            {
                Tables = definition.Tables
                    .Select(table =>
                    {
                        var key = (definition.Name, table.Name);
                        if (!byTable.TryGetValue(key, out var tableIncludes))
                        {
                            return table;
                        }
                        consumed.Add(key);
                        return MergeIncludes(definition.Name, table, tableIncludes);
                    })
                    .ToList(),
            })
            .ToList();

        // Parsed includes always name the table whose body they were written in, so a dangling one can only come
        // from a hand-built document; fail rather than drop it silently.
        foreach (var dangling in byTable.Keys.Where(key => !consumed.Contains(key)))
        {
            var include = byTable[dangling][0];
            _diagnostics.Add(TemplateDiagnostics.IncludeUnknownTable(include.TemplateName, include.SchemaName, include.TableName));
        }

        return database with { Schemas = resolved };
    }

    /// <summary>
    /// Merges each included table template's members into <paramref name="table"/>: columns land at the position
    /// the include was written, foreign keys referencing the include placeholder re-point at the including table's
    /// schema, and everything else appends. A member the table already declares is rejected.
    /// </summary>
    private Table MergeIncludes(SqlIdentifier schemaName, Table table, List<TemplateInclude> includes)
    {
        var columns = table.Columns.ToList();
        var foreignKeys = table.ForeignKeys.ToList();
        var uniqueConstraints = table.UniqueConstraints.ToList();
        var checkConstraints = table.CheckConstraints.ToList();
        var exclusionConstraints = table.ExclusionConstraints.ToList();
        var indexes = table.Indexes.ToList();
        var primaryKey = table.PrimaryKey;

        var offset = 0;
        foreach (var include in includes)
        {
            if (!templates.TryGetValue(include.TemplateName, out var template))
            {
                _diagnostics.Add(TemplateDiagnostics.IncludeUnknownTemplate(schemaName, table.Name, include.TemplateName));
                continue;
            }
            if (template is not TableTemplateStatement tableTemplate)
            {
                _diagnostics.Add(TemplateDiagnostics.IncludedSchemaTemplate(schemaName, table.Name, include.TemplateName));
                continue;
            }

            if (!_memberCache.TryGetValue(include.TemplateName, out var members))
            {
                // The members project once against the placeholder; placeholder references (an unqualified
                // REFERENCES in the body) re-point per including table below.
                (members, _) = DocumentProjector.ProjectTableMembers(IncludePlaceholder, null, tableTemplate.Members);
                _memberCache[include.TemplateName] = members;
            }

            // Validate before merging anything, so a conflicted include is skipped whole rather than half-applied.
            var conflicts = new List<Diagnostic>();
            foreach (var column in members.Columns)
            {
                if (columns.Any(c => c.Name == column.Name))
                {
                    conflicts.Add(TemplateDiagnostics.IncludeColumnConflict(include.TemplateName, column.Name, schemaName, table.Name));
                }
            }
            if (members.PrimaryKey is not null && primaryKey is not null)
            {
                conflicts.Add(TemplateDiagnostics.IncludePrimaryKeyConflict(include.TemplateName, schemaName, table.Name));
            }
            if (conflicts.Count > 0)
            {
                _diagnostics.AddRange(conflicts);
                continue;
            }

            columns.InsertRange(include.ColumnPosition + offset, members.Columns);
            offset += members.Columns.Count;

            if (members.PrimaryKey is not null)
            {
                primaryKey = members.PrimaryKey;
            }

            foreignKeys.AddRange(members.ForeignKeys.Select(fk =>
                fk.ReferencedSchema == IncludePlaceholder
                    ? fk with { ReferencedSchema = schemaName }
                    : fk));
            uniqueConstraints.AddRange(members.UniqueConstraints);
            checkConstraints.AddRange(members.CheckConstraints);
            exclusionConstraints.AddRange(members.ExclusionConstraints);
            indexes.AddRange(members.Indexes);
        }

        return table with
        {
            Columns = columns,
            PrimaryKey = primaryKey,
            ForeignKeys = foreignKeys,
            UniqueConstraints = uniqueConstraints,
            CheckConstraints = checkConstraints,
            ExclusionConstraints = exclusionConstraints,
            Indexes = indexes,
        };
    }
}
