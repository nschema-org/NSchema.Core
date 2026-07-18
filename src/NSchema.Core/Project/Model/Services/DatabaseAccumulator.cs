using NSchema.Model;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Extensions;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;
using NSchema.Model.Views;
using NSchema.Project.Nsql;

namespace NSchema.Project.Model.Services;

/// <summary>
/// Accumulates parsed statements into a <see cref="Database"/>. Schema entries are vivified on demand so
/// a <c>DROP TABLE app.x</c> can record the drop even when <c>app</c> was never explicitly declared.
/// </summary>
internal sealed class DatabaseAccumulator
{
    private readonly List<Entry> _entries = [];
    private readonly Dictionary<SqlIdentifier, Entry> _byName = new();
    private readonly List<Extension> _extensions = [];
    private readonly List<PendingGrant> _tableGrants = [];
    private readonly List<PendingTrigger> _triggers = [];
    private readonly List<PendingIndex> _standaloneIndexes = [];
    private readonly List<TemplateInclude> _includes = [];
    private readonly List<NsqlDiagnostic> _diagnostics = [];

    /// <summary>
    /// The assembly findings recorded while accumulating — duplicate declarations, references to unknown
    /// tables. Positioned like syntax errors, but they are project semantics, not grammar: the accumulator
    /// records and carries on, so one pass reports every finding.
    /// </summary>
    public IReadOnlyList<NsqlDiagnostic> Diagnostics => _diagnostics;

    private void AddError(string message, SourcePosition position) =>
        _diagnostics.Add(new NsqlDiagnostic("project", $"{message} (at {position}).", DiagnosticSeverity.Error, position));

    /// <summary>
    /// The <c>INCLUDE</c> members collected from table bodies. Unlike the other pending lists these are not
    /// resolved at <see cref="Build"/> — they resolve against the aggregate at template expansion, so they are
    /// exported alongside the built schema rather than folded into it.
    /// </summary>
    public IReadOnlyList<TemplateInclude> Includes => _includes;

    public void AddInclude(TemplateInclude include) => _includes.Add(include);

    public void DeclareSchema(SqlIdentifier name, string? comment, SourcePosition position)
    {
        var entry = GetOrAdd(name);
        if (entry.Declared)
        {
            AddError($"Schema '{name}' is already declared.", position);
            return;
        }

        entry.Declared = true;
        entry.Comment = comment;
    }

    public void AddTable(SqlIdentifier schema, Table table, SourcePosition position)
    {
        var entry = GetOrAdd(schema);
        if (entry.Tables.Any(t => t.Name == table.Name))
        {
            AddError($"Table '{schema}.{table.Name}' is already declared.", position);
            return;
        }

        entry.Tables.Add(table);
    }

    public void AddView(SqlIdentifier schema, View view, SourcePosition position)
    {
        var entry = GetOrAdd(schema);
        if (entry.Views.Any(v => v.Name == view.Name))
        {
            AddError($"View '{schema}.{view.Name}' is already declared.", position);
            return;
        }

        entry.Views.Add(view);
    }

    public void AddEnum(SqlIdentifier schema, EnumType enumType, SourcePosition position)
    {
        var entry = GetOrAdd(schema);
        if (entry.Enums.Any(e => e.Name == enumType.Name))
        {
            AddError($"Enum '{schema}.{enumType.Name}' is already declared.", position);
            return;
        }

        entry.Enums.Add(enumType);
    }

    public void AddSequence(SqlIdentifier schema, Sequence sequence, SourcePosition position)
    {
        var entry = GetOrAdd(schema);
        if (entry.Sequences.Any(s => s.Name == sequence.Name))
        {
            AddError($"Sequence '{schema}.{sequence.Name}' is already declared.", position);
            return;
        }

        entry.Sequences.Add(sequence);
    }

    public void AddDomain(SqlIdentifier schema, DomainType domain, SourcePosition position)
    {
        var entry = GetOrAdd(schema);
        if (entry.Domains.Any(d => d.Name == domain.Name))
        {
            AddError($"DomainType '{schema}.{domain.Name}' is already declared.", position);
            return;
        }

        entry.Domains.Add(domain);
    }

    public void AddCompositeType(SqlIdentifier schema, CompositeType compositeType, SourcePosition position)
    {
        var entry = GetOrAdd(schema);
        if (entry.CompositeTypes.Any(t => t.Name == compositeType.Name))
        {
            AddError($"Composite type '{schema}.{compositeType.Name}' is already declared.", position);
            return;
        }

        entry.CompositeTypes.Add(compositeType);
    }

    // Functions and procedures are one routine pool sharing a single name space, as they do in the database:
    // a function and a procedure with the same name cannot coexist in a schema, which a single list enforces.
    public void AddRoutine(SqlIdentifier schema, Routine routine, SourcePosition position)
    {
        var entry = GetOrAdd(schema);
        if (entry.Routines.Any(r => r.Name == routine.Name))
        {
            AddError($"Routine '{schema}.{routine.Name}' is already declared (functions and procedures share one name space).", position);
            return;
        }

        entry.Routines.Add(routine);
    }

    public void AddSchemaGrant(SqlIdentifier schema, SqlIdentifier role)
    {
        var entry = GetOrAdd(schema);
        if (entry.Grants.All(g => g.Role != role))
        {
            entry.Grants.Add(new SchemaGrant(role));
        }
    }

    // Table grants are resolved at Build, so a grant may appear before or after the table it targets.
    public void AddTableGrant(SqlIdentifier schema, SqlIdentifier table, TableGrant grant, SourcePosition position)
        => _tableGrants.Add(new PendingGrant(schema, table, grant, position));

    // Triggers are standalone statements attached to their table at Build, so a trigger may appear before or
    // after the CREATE TABLE it targets.
    public void AddTrigger(SqlIdentifier schema, SqlIdentifier table, Trigger trigger, SourcePosition position)
        => _triggers.Add(new PendingTrigger(schema, table, trigger, position));

    // Standalone indexes attach to their relation (a table or a materialized view) at Build, so an index may
    // appear before or after the CREATE that declares its target.
    public void AddIndex(SqlIdentifier schema, SqlIdentifier relation, TableIndex index, SourcePosition position)
        => _standaloneIndexes.Add(new PendingIndex(schema, relation, index, position));

    // Extensions are database-global, so they live on the accumulator itself rather than a per-schema entry.
    public void AddExtension(Extension extension, SourcePosition position)
    {
        if (_extensions.Any(e => e.Name == extension.Name))
        {
            AddError($"Extension '{extension.Name}' is already declared.", position);
            return;
        }

        _extensions.Add(extension);
    }

    public Database Build()
    {
        ApplyTableGrants();
        ApplyTriggers();
        ApplyIndexes();
        var schemas = _entries
            .Select(e => new Schema(e.Name, e.Tables, e.Grants, e.Views,
                e.Enums, e.Sequences, e.Routines, e.Domains, e.CompositeTypes)
            { Comment = e.Comment })
            .ToList();
        return new Database(schemas, _extensions);
    }

    private void ApplyTableGrants()
    {
        foreach (var pending in _tableGrants)
        {
            if (!_byName.TryGetValue(pending.Schema, out var entry))
            {
                AddError($"GRANT references unknown table '{pending.Schema}.{pending.Table}'.", pending.Position);
                continue;
            }

            var index = entry.Tables.FindIndex(t => t.Name == pending.Table);
            if (index < 0)
            {
                AddError($"GRANT references unknown table '{pending.Schema}.{pending.Table}'.", pending.Position);
                continue;
            }

            var table = entry.Tables[index];
            entry.Tables[index] = table.With(grants: [.. table.Grants, pending.Grant]);
        }
    }

    private void ApplyTriggers()
    {
        foreach (var pending in _triggers)
        {
            if (!_byName.TryGetValue(pending.Schema, out var entry))
            {
                AddError($"CREATE TRIGGER references unknown table '{pending.Schema}.{pending.Table}'.", pending.Position);
                continue;
            }

            var index = entry.Tables.FindIndex(t => t.Name == pending.Table);
            if (index < 0)
            {
                AddError($"CREATE TRIGGER references unknown table '{pending.Schema}.{pending.Table}'.", pending.Position);
                continue;
            }

            var table = entry.Tables[index];
            if (table.Triggers.Any(t => t.Name == pending.Trigger.Name))
            {
                AddError($"Trigger '{pending.Trigger.Name}' on '{pending.Schema}.{pending.Table}' is already declared.",
                    pending.Position);
            }

            entry.Tables[index] = table.With(triggers: [.. table.Triggers, pending.Trigger]);
        }
    }

    private void ApplyIndexes()
    {
        foreach (var pending in _standaloneIndexes)
        {
            var qualified = $"{pending.Schema}.{pending.Relation}";
            if (!_byName.TryGetValue(pending.Schema, out var entry))
            {
                AddError($"CREATE INDEX references unknown table or materialized view '{qualified}'.", pending.Position);
                continue;
            }

            // A standalone index attaches to a table (the same as an inline index) or a materialized view.
            var tableIndex = entry.Tables.FindIndex(t => t.Name == pending.Relation);
            if (tableIndex >= 0)
            {
                var table = entry.Tables[tableIndex];
                if (table.Indexes.Any(i => i.Name == pending.Index.Name))
                {
                    AddError($"Index '{pending.Index.Name}' on '{qualified}' is already declared.", pending.Position);
                    continue;
                }

                entry.Tables[tableIndex] = table.With(indexes: [.. table.Indexes, pending.Index]);
                continue;
            }

            var viewIndex = entry.Views.FindIndex(v => v.Name == pending.Relation);
            if (viewIndex < 0)
            {
                AddError($"CREATE INDEX references unknown table or materialized view '{qualified}'.", pending.Position);
                continue;
            }

            var view = entry.Views[viewIndex];
            if (!view.IsMaterialized)
            {
                AddError($"CREATE INDEX targets '{qualified}', which is not a materialized view (a plain view cannot be indexed).", pending.Position);
                continue;
            }

            if (view.Indexes.Any(i => i.Name == pending.Index.Name))
            {
                AddError($"Index '{pending.Index.Name}' on '{qualified}' is already declared.", pending.Position);
                continue;
            }

            entry.Views[viewIndex] = view.With(indexes: [.. view.Indexes, pending.Index]);
        }
    }

    private Entry GetOrAdd(SqlIdentifier name)
    {
        if (_byName.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var entry = new Entry(name);
        _entries.Add(entry);
        _byName[name] = entry;
        return entry;
    }

    private sealed class Entry(SqlIdentifier name)
    {
        public SqlIdentifier Name { get; } = name;
        public bool Declared { get; set; }
        public string? Comment { get; set; }
        public List<Table> Tables { get; } = [];
        public List<SchemaGrant> Grants { get; } = [];
        public List<View> Views { get; } = [];
        public List<EnumType> Enums { get; } = [];
        public List<Sequence> Sequences { get; } = [];
        public List<Routine> Routines { get; } = [];
        public List<DomainType> Domains { get; } = [];
        public List<CompositeType> CompositeTypes { get; } = [];
    }

    private readonly record struct PendingGrant(SqlIdentifier Schema, SqlIdentifier Table, TableGrant Grant, SourcePosition Position);

    private readonly record struct PendingTrigger(SqlIdentifier Schema, SqlIdentifier Table, Trigger Trigger, SourcePosition Position);

    private readonly record struct PendingIndex(SqlIdentifier Schema, SqlIdentifier Relation, TableIndex Index, SourcePosition Position);
}
