using NSchema.Project.Ddl;
using NSchema.Project.Nsql;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.CompositeTypes;
using NSchema.Project.Domain.Models.Domains;
using NSchema.Project.Domain.Models.Enums;
using NSchema.Project.Domain.Models.Extensions;
using NSchema.Project.Domain.Models.Indexes;
using NSchema.Project.Domain.Models.Routines;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Sequences;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Triggers;
using NSchema.Project.Domain.Models.Views;

namespace NSchema.Project.Domain;

/// <summary>
/// Accumulates parsed statements into a <see cref="DatabaseSchema"/>. Schema entries are vivified on demand so
/// a <c>DROP TABLE app.x</c> can record the drop even when <c>app</c> was never explicitly declared.
/// </summary>
internal sealed class SchemaAccumulator
{
    private readonly List<Entry> _entries = [];
    private readonly Dictionary<SqlIdentifier, Entry> _byName = new();
    private readonly List<SqlIdentifier> _droppedSchemas = [];
    private readonly List<Extension> _extensions = [];
    private readonly List<SqlIdentifier> _droppedExtensions = [];
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

    public void DeclareSchema(SqlIdentifier name, SqlIdentifier? oldName, bool isPartial, string? comment, SourcePosition position)
    {
        var entry = GetOrAdd(name);
        if (entry.Declared)
        {
            AddError($"Schema '{name}' is already declared.", position);
            return;
        }

        entry.Declared = true;
        entry.OldName = oldName;
        entry.IsPartial = isPartial;
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

    public void AddDomain(SqlIdentifier schema, DomainDefinition domain, SourcePosition position)
    {
        var entry = GetOrAdd(schema);
        if (entry.Domains.Any(d => d.Name == domain.Name))
        {
            AddError($"DomainDefinition '{schema}.{domain.Name}' is already declared.", position);
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

    public void DropSchema(SqlIdentifier name)
    {
        if (!_droppedSchemas.Contains(name))
        {
            _droppedSchemas.Add(name);
        }
    }

    public void DropExtension(SqlIdentifier name)
    {
        if (!_droppedExtensions.Contains(name))
        {
            _droppedExtensions.Add(name);
        }
    }

    public void DropTable(SqlIdentifier schema, SqlIdentifier table)
    {
        var entry = GetOrAdd(schema);
        if (!entry.DroppedTables.Contains(table))
        {
            entry.DroppedTables.Add(table);
        }
    }

    public void DropView(SqlIdentifier schema, SqlIdentifier view)
    {
        var entry = GetOrAdd(schema);
        if (!entry.DroppedViews.Contains(view))
        {
            entry.DroppedViews.Add(view);
        }
    }

    public void DropEnum(SqlIdentifier schema, SqlIdentifier enumName)
    {
        var entry = GetOrAdd(schema);
        if (!entry.DroppedEnums.Contains(enumName))
        {
            entry.DroppedEnums.Add(enumName);
        }
    }

    public void DropDomain(SqlIdentifier schema, SqlIdentifier domain)
    {
        var entry = GetOrAdd(schema);
        if (!entry.DroppedDomains.Contains(domain))
        {
            entry.DroppedDomains.Add(domain);
        }
    }

    public void DropCompositeType(SqlIdentifier schema, SqlIdentifier compositeType)
    {
        var entry = GetOrAdd(schema);
        if (!entry.DroppedCompositeTypes.Contains(compositeType))
        {
            entry.DroppedCompositeTypes.Add(compositeType);
        }
    }

    public void DropSequence(SqlIdentifier schema, SqlIdentifier sequence)
    {
        var entry = GetOrAdd(schema);
        if (!entry.DroppedSequences.Contains(sequence))
        {
            entry.DroppedSequences.Add(sequence);
        }
    }

    public void DropRoutine(SqlIdentifier schema, SqlIdentifier routine)
    {
        var entry = GetOrAdd(schema);
        if (!entry.DroppedRoutines.Contains(routine))
        {
            entry.DroppedRoutines.Add(routine);
        }
    }

    public DatabaseSchema Build()
    {
        ApplyTableGrants();
        ApplyTriggers();
        ApplyIndexes();
        var schemas = _entries
            .Select(e => new SchemaDefinition(e.Name, e.OldName, e.IsPartial, e.Comment, e.Tables, e.DroppedTables, e.Grants, e.Views, e.DroppedViews,
                e.Enums, e.DroppedEnums, e.Sequences, e.DroppedSequences,
                e.Routines, e.DroppedRoutines, e.Domains, e.DroppedDomains, e.CompositeTypes, e.DroppedCompositeTypes))
            .ToList();
        return new DatabaseSchema(schemas, _droppedSchemas, _extensions, _droppedExtensions);
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
            entry.Tables[index] = table with { Grants = [.. table.Grants, pending.Grant] };
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

            entry.Tables[index] = table with { Triggers = [.. table.Triggers, pending.Trigger] };
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

                entry.Tables[tableIndex] = table with { Indexes = [.. table.Indexes, pending.Index] };
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

            entry.Views[viewIndex] = view with { Indexes = [.. view.Indexes, pending.Index] };
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
        public SqlIdentifier? OldName { get; set; }
        public bool IsPartial { get; set; }
        public string? Comment { get; set; }
        public List<Table> Tables { get; } = [];
        public List<SqlIdentifier> DroppedTables { get; } = [];
        public List<SchemaGrant> Grants { get; } = [];
        public List<View> Views { get; } = [];
        public List<SqlIdentifier> DroppedViews { get; } = [];
        public List<EnumType> Enums { get; } = [];
        public List<SqlIdentifier> DroppedEnums { get; } = [];
        public List<Sequence> Sequences { get; } = [];
        public List<SqlIdentifier> DroppedSequences { get; } = [];
        public List<Routine> Routines { get; } = [];
        public List<SqlIdentifier> DroppedRoutines { get; } = [];
        public List<DomainDefinition> Domains { get; } = [];
        public List<SqlIdentifier> DroppedDomains { get; } = [];
        public List<CompositeType> CompositeTypes { get; } = [];
        public List<SqlIdentifier> DroppedCompositeTypes { get; } = [];
    }

    private readonly record struct PendingGrant(SqlIdentifier Schema, SqlIdentifier Table, TableGrant Grant, SourcePosition Position);

    private readonly record struct PendingTrigger(SqlIdentifier Schema, SqlIdentifier Table, Trigger Trigger, SourcePosition Position);

    private readonly record struct PendingIndex(SqlIdentifier Schema, SqlIdentifier Relation, TableIndex Index, SourcePosition Position);
}
