using NSchema.Schema.Ddl.Model;
using NSchema.Schema.Model;
using NSchema.Schema.Model.CompositeTypes;
using NSchema.Schema.Model.Domains;
using NSchema.Schema.Model.Enums;
using NSchema.Schema.Model.Extensions;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Routines;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Sequences;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Templates;
using NSchema.Schema.Model.Triggers;
using NSchema.Schema.Model.Views;

namespace NSchema.Schema.Ddl;

internal sealed partial class DdlParser
{
    /// <summary>
    /// Accumulates parsed statements into a <see cref="DatabaseSchema"/>. Schema entries are vivified on demand so
    /// a <c>DROP TABLE app.x</c> can record the drop even when <c>app</c> was never explicitly declared.
    /// </summary>
    private sealed class SchemaAccumulator
    {
        private readonly List<Entry> _entries = [];
        private readonly Dictionary<string, Entry> _byName = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _droppedSchemas = [];
        private readonly List<Extension> _extensions = [];
        private readonly List<string> _droppedExtensions = [];
        private readonly List<PendingGrant> _tableGrants = [];
        private readonly List<PendingTrigger> _triggers = [];
        private readonly List<PendingIndex> _standaloneIndexes = [];
        private readonly List<TemplateInclude> _includes = [];

        /// <summary>
        /// The <c>INCLUDE</c> members collected from table bodies. Unlike the other pending lists these are not
        /// resolved at <see cref="Build"/> — they resolve against the aggregate at template expansion, so they are
        /// exported alongside the built schema rather than folded into it.
        /// </summary>
        public IReadOnlyList<TemplateInclude> Includes => _includes;

        public void AddInclude(TemplateInclude include) => _includes.Add(include);

        public void DeclareSchema(string name, string? oldName, bool isPartial, string? comment, SourcePosition position)
        {
            var entry = GetOrAdd(name);
            if (entry.Declared)
            {
                throw new DdlSyntaxException($"Schema '{name}' is already declared.", position);
            }
            entry.Declared = true;
            entry.OldName = oldName;
            entry.IsPartial = isPartial;
            entry.Comment = comment;
        }

        public void AddTable(string schema, Table table, SourcePosition position)
        {
            var entry = GetOrAdd(schema);
            if (entry.Tables.Any(t => t.Name == table.Name))
            {
                throw new DdlSyntaxException($"Table '{schema}.{table.Name}' is already declared.", position);
            }
            entry.Tables.Add(table);
        }

        public void AddView(string schema, View view, SourcePosition position)
        {
            var entry = GetOrAdd(schema);
            if (entry.Views.Any(v => v.Name == view.Name))
            {
                throw new DdlSyntaxException($"View '{schema}.{view.Name}' is already declared.", position);
            }
            entry.Views.Add(view);
        }

        public void AddEnum(string schema, EnumType enumType, SourcePosition position)
        {
            var entry = GetOrAdd(schema);
            if (entry.Enums.Any(e => e.Name == enumType.Name))
            {
                throw new DdlSyntaxException($"Enum '{schema}.{enumType.Name}' is already declared.", position);
            }
            entry.Enums.Add(enumType);
        }

        public void AddSequence(string schema, Sequence sequence, SourcePosition position)
        {
            var entry = GetOrAdd(schema);
            if (entry.Sequences.Any(s => s.Name == sequence.Name))
            {
                throw new DdlSyntaxException($"Sequence '{schema}.{sequence.Name}' is already declared.", position);
            }
            entry.Sequences.Add(sequence);
        }

        public void AddDomain(string schema, Domain domain, SourcePosition position)
        {
            var entry = GetOrAdd(schema);
            if (entry.Domains.Any(d => d.Name == domain.Name))
            {
                throw new DdlSyntaxException($"Domain '{schema}.{domain.Name}' is already declared.", position);
            }
            entry.Domains.Add(domain);
        }

        public void AddCompositeType(string schema, CompositeType compositeType, SourcePosition position)
        {
            var entry = GetOrAdd(schema);
            if (entry.CompositeTypes.Any(t => t.Name == compositeType.Name))
            {
                throw new DdlSyntaxException($"Composite type '{schema}.{compositeType.Name}' is already declared.", position);
            }
            entry.CompositeTypes.Add(compositeType);
        }

        // Functions and procedures are one routine pool sharing a single name space, as they do in the database:
        // a function and a procedure with the same name cannot coexist in a schema, which a single list enforces.
        public void AddRoutine(string schema, Routine routine, SourcePosition position)
        {
            var entry = GetOrAdd(schema);
            if (entry.Routines.Any(r => string.Equals(r.Name, routine.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DdlSyntaxException(
                    $"Routine '{schema}.{routine.Name}' is already declared (functions and procedures share one name space).", position);
            }
            entry.Routines.Add(routine);
        }

        public void AddSchemaGrant(string schema, string role)
        {
            var entry = GetOrAdd(schema);
            if (entry.Grants.All(g => g.Role != role))
            {
                entry.Grants.Add(new SchemaGrant(role));
            }
        }

        // Table grants are resolved at Build, so a grant may appear before or after the table it targets.
        public void AddTableGrant(string schema, string table, TableGrant grant, SourcePosition position)
            => _tableGrants.Add(new PendingGrant(schema, table, grant, position));

        // Triggers are standalone statements attached to their table at Build, so a trigger may appear before or
        // after the CREATE TABLE it targets.
        public void AddTrigger(string schema, string table, Trigger trigger, SourcePosition position)
            => _triggers.Add(new PendingTrigger(schema, table, trigger, position));

        // Standalone indexes attach to their relation (a table or a materialized view) at Build, so an index may
        // appear before or after the CREATE that declares its target.
        public void AddIndex(string schema, string relation, TableIndex index, SourcePosition position)
            => _standaloneIndexes.Add(new PendingIndex(schema, relation, index, position));

        // Extensions are database-global, so they live on the accumulator itself rather than a per-schema entry.
        public void AddExtension(Extension extension, SourcePosition position)
        {
            if (_extensions.Any(e => string.Equals(e.Name, extension.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DdlSyntaxException($"Extension '{extension.Name}' is already declared.", position);
            }
            _extensions.Add(extension);
        }

        public void DropSchema(string name)
        {
            if (!_droppedSchemas.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                _droppedSchemas.Add(name);
            }
        }

        public void DropExtension(string name)
        {
            if (!_droppedExtensions.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                _droppedExtensions.Add(name);
            }
        }

        public void DropTable(string schema, string table)
        {
            var entry = GetOrAdd(schema);
            if (!entry.DroppedTables.Contains(table, StringComparer.OrdinalIgnoreCase))
            {
                entry.DroppedTables.Add(table);
            }
        }

        public void DropView(string schema, string view)
        {
            var entry = GetOrAdd(schema);
            if (!entry.DroppedViews.Contains(view, StringComparer.OrdinalIgnoreCase))
            {
                entry.DroppedViews.Add(view);
            }
        }

        public void DropEnum(string schema, string enumName)
        {
            var entry = GetOrAdd(schema);
            if (!entry.DroppedEnums.Contains(enumName, StringComparer.OrdinalIgnoreCase))
            {
                entry.DroppedEnums.Add(enumName);
            }
        }

        public void DropDomain(string schema, string domain)
        {
            var entry = GetOrAdd(schema);
            if (!entry.DroppedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            {
                entry.DroppedDomains.Add(domain);
            }
        }

        public void DropCompositeType(string schema, string compositeType)
        {
            var entry = GetOrAdd(schema);
            if (!entry.DroppedCompositeTypes.Contains(compositeType, StringComparer.OrdinalIgnoreCase))
            {
                entry.DroppedCompositeTypes.Add(compositeType);
            }
        }

        public void DropSequence(string schema, string sequence)
        {
            var entry = GetOrAdd(schema);
            if (!entry.DroppedSequences.Contains(sequence, StringComparer.OrdinalIgnoreCase))
            {
                entry.DroppedSequences.Add(sequence);
            }
        }

        public void DropRoutine(string schema, string routine)
        {
            var entry = GetOrAdd(schema);
            if (!entry.DroppedRoutines.Contains(routine, StringComparer.OrdinalIgnoreCase))
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
                    throw new DdlSyntaxException($"GRANT references unknown table '{pending.Schema}.{pending.Table}'.", pending.Position);
                }

                var index = entry.Tables.FindIndex(t => t.Name == pending.Table);
                if (index < 0)
                {
                    throw new DdlSyntaxException($"GRANT references unknown table '{pending.Schema}.{pending.Table}'.", pending.Position);
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
                    throw new DdlSyntaxException($"CREATE TRIGGER references unknown table '{pending.Schema}.{pending.Table}'.", pending.Position);
                }

                var index = entry.Tables.FindIndex(t => t.Name == pending.Table);
                if (index < 0)
                {
                    throw new DdlSyntaxException($"CREATE TRIGGER references unknown table '{pending.Schema}.{pending.Table}'.", pending.Position);
                }

                var table = entry.Tables[index];
                if (table.Triggers.Any(t => string.Equals(t.Name, pending.Trigger.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new DdlSyntaxException($"Trigger '{pending.Trigger.Name}' on '{pending.Schema}.{pending.Table}' is already declared.", pending.Position);
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
                    throw new DdlSyntaxException($"CREATE INDEX references unknown table or materialized view '{qualified}'.", pending.Position);
                }

                // A standalone index attaches to a table (the same as an inline index) or a materialized view.
                var tableIndex = entry.Tables.FindIndex(t => t.Name == pending.Relation);
                if (tableIndex >= 0)
                {
                    var table = entry.Tables[tableIndex];
                    if (table.Indexes.Any(i => string.Equals(i.Name, pending.Index.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new DdlSyntaxException($"Index '{pending.Index.Name}' on '{qualified}' is already declared.", pending.Position);
                    }
                    entry.Tables[tableIndex] = table with { Indexes = [.. table.Indexes, pending.Index] };
                    continue;
                }

                var viewIndex = entry.Views.FindIndex(v => v.Name == pending.Relation);
                if (viewIndex < 0)
                {
                    throw new DdlSyntaxException($"CREATE INDEX references unknown table or materialized view '{qualified}'.", pending.Position);
                }

                var view = entry.Views[viewIndex];
                if (!view.IsMaterialized)
                {
                    throw new DdlSyntaxException($"CREATE INDEX targets '{qualified}', which is not a materialized view (a plain view cannot be indexed).", pending.Position);
                }
                if (view.Indexes.Any(i => string.Equals(i.Name, pending.Index.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new DdlSyntaxException($"Index '{pending.Index.Name}' on '{qualified}' is already declared.", pending.Position);
                }

                entry.Views[viewIndex] = view with { Indexes = [.. view.Indexes, pending.Index] };
            }
        }

        private Entry GetOrAdd(string name)
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

        private sealed class Entry(string name)
        {
            public string Name { get; } = name;
            public bool Declared { get; set; }
            public string? OldName { get; set; }
            public bool IsPartial { get; set; }
            public string? Comment { get; set; }
            public List<Table> Tables { get; } = [];
            public List<string> DroppedTables { get; } = [];
            public List<SchemaGrant> Grants { get; } = [];
            public List<View> Views { get; } = [];
            public List<string> DroppedViews { get; } = [];
            public List<EnumType> Enums { get; } = [];
            public List<string> DroppedEnums { get; } = [];
            public List<Sequence> Sequences { get; } = [];
            public List<string> DroppedSequences { get; } = [];
            public List<Routine> Routines { get; } = [];
            public List<string> DroppedRoutines { get; } = [];
            public List<Domain> Domains { get; } = [];
            public List<string> DroppedDomains { get; } = [];
            public List<CompositeType> CompositeTypes { get; } = [];
            public List<string> DroppedCompositeTypes { get; } = [];
        }

        private readonly record struct PendingGrant(string Schema, string Table, TableGrant Grant, SourcePosition Position);

        private readonly record struct PendingTrigger(string Schema, string Table, Trigger Trigger, SourcePosition Position);

        private readonly record struct PendingIndex(string Schema, string Relation, TableIndex Index, SourcePosition Position);
    }
}
