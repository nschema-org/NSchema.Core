using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Extensions;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;
using NSchema.Model.Views;
using NSchema.Project.Model.Directives;
using NSchema.Project.Nsql.Syntax;
using Syn = NSchema.Project.Nsql.Syntax;

namespace NSchema.Project.Nsql;

/// <summary>
/// Builds syntax documents from the domain model.
/// </summary>
internal static class SyntaxBuilder
{
    private static readonly SourcePosition _none = SourcePosition.None;

    public static NsqlDocument Build(Database database, bool declareSchemas = true) =>
        Build(database, ProjectDirectives.Empty, declareSchemas);

    public static NsqlDocument Build(Database database, ProjectDirectives directives, bool declareSchemas = true)
    {
        var statements = new List<NsqlStatement>();

        // Extensions are database-global and are created first, so they precede the schemas.
        foreach (var extension in database.Extensions)
        {
            statements.Add(Build(extension));
        }

        foreach (var definition in database.Schemas)
        {
            AddSchema(statements, definition, declareSchemas);
        }

        // Scripts are directives; each kind renders straight from its home on the directives.
        foreach (var script in directives.ChangeScripts)
        {
            statements.Add(Build(script));
        }
        foreach (var script in directives.DeploymentScripts)
        {
            statements.Add(Build(script));
        }

        AddDirectives(statements, directives);

        return new NsqlDocument(statements);
    }

    /// <summary>
    /// Emits the directive statements after the declarations: the renames, container level first.
    /// </summary>
    private static void AddDirectives(List<NsqlStatement> statements, ProjectDirectives directives)
    {
        foreach (var rename in directives.SchemaRenames)
        {
            statements.Add(new Syn.Schemas.RenameSchemaStatement(Name(rename.From), Name(rename.To)) { Position = _none });
        }
        foreach (var rename in directives.ObjectRenames)
        {
            statements.Add(new RenameObjectStatement(rename.From.Kind, Qualified(rename.From.Schema, rename.From.Name), Name(rename.To)) { Position = _none });
        }
        foreach (var rename in directives.MemberRenames)
        {
            statements.Add(new Syn.Tables.RenameColumnStatement(
                new MemberPath(Name(rename.From.Schema), Name(rename.From.Object), Name(rename.From.Member)) { Position = _none },
                Name(rename.To))
            { Position = _none });
        }


    }

    private static void AddSchema(List<NsqlStatement> statements, Schema schema, bool declare)
    {
        if (declare)
        {
            statements.Add(new Syn.Schemas.CreateSchemaStatement(Name(schema.Name))
            {
                Position = _none,
                Doc = schema.Comment,
            });

            foreach (var grant in schema.Grants)
            {
                statements.Add(new Syn.Schemas.GrantSchemaUsageStatement(Name(schema.Name), Name(grant.Role)) { Position = _none });
            }
        }

        foreach (var enumType in schema.Enums)
        {
            statements.Add(new Syn.Enums.CreateEnumStatement(Qualified(schema.Name, enumType.Name), enumType.Values.Select(v => v.Value).ToList())
            {
                Position = _none,
                Doc = enumType.Comment,
            });
        }

        foreach (var domain in schema.Domains)
        {
            statements.Add(new Syn.Domains.CreateDomainStatement(Qualified(schema.Name, domain.Name), Type(domain.DataType), domain.NotNull,
                domain.Checks.Select(c => new Syn.Constraints.CheckDefinition(Name(c.Name), c.Expression) { Position = _none }).ToList(),
                domain.Default)
            {
                Position = _none,
                Doc = domain.Comment,
            });
        }

        foreach (var compositeType in schema.CompositeTypes)
        {
            statements.Add(new Syn.CompositeTypes.CreateCompositeTypeStatement(Qualified(schema.Name, compositeType.Name),
                compositeType.Fields.Select(f => new Syn.CompositeTypes.CompositeFieldDefinition(Name(f.Name), Type(f.DataType)) { Position = _none }).ToList())
            {
                Position = _none,
                Doc = compositeType.Comment,
            });
        }

        foreach (var sequence in schema.Sequences)
        {
            statements.Add(new Syn.Sequences.CreateSequenceStatement(Qualified(schema.Name, sequence.Name),
                Options(sequence.Options))
            {
                Position = _none,
                Doc = sequence.Comment,
            });
        }

        foreach (var routine in schema.Routines)
        {
            statements.Add(new Syn.Routines.CreateRoutineStatement(Qualified(schema.Name, routine.Name),
                routine.RoutineKind == RoutineKind.Procedure ? Syn.Routines.RoutineKind.Procedure : Syn.Routines.RoutineKind.Function,
                routine.Arguments, routine.Definition)
            {
                Position = _none,
                Doc = routine.Comment,
            });
        }

        foreach (var table in schema.Tables)
        {
            AddTable(statements, schema.Name, table);
        }

        foreach (var view in schema.Views)
        {
            AddView(statements, schema.Name, view);
        }


    }

    private static void AddTable(List<NsqlStatement> statements, SqlIdentifier schemaName, Table table)
    {
        var members = new List<Syn.Tables.TableMember>();

        foreach (var column in table.Columns)
        {
            members.Add(new Syn.Tables.ColumnDefinition(Name(column.Name), Type(column.Type), column.IsNullable, column.IsIdentity,
                Options(column.IdentityOptions), column.DefaultExpression, column.GeneratedExpression)
            {
                Position = _none,
                Doc = column.Comment,
            });
        }
        if (table.PrimaryKey is { } pk)
        {
            members.Add(new Syn.Constraints.PrimaryKeyDefinition(Name(pk.Name), Names(pk.ColumnNames)) { Position = _none, Doc = pk.Comment });
        }
        foreach (var fk in table.ForeignKeys)
        {
            members.Add(new Syn.Constraints.ForeignKeyDefinition(Name(fk.Name), Names(fk.ColumnNames),
                Qualified(fk.References.Schema, fk.References.Name), Names(fk.ReferencedColumnNames),
                Action(fk.OnDelete), Action(fk.OnUpdate))
            {
                Position = _none,
                Doc = fk.Comment,
            });
        }
        foreach (var unique in table.UniqueConstraints)
        {
            members.Add(new Syn.Constraints.UniqueDefinition(Name(unique.Name), Names(unique.ColumnNames)) { Position = _none, Doc = unique.Comment });
        }
        foreach (var check in table.CheckConstraints)
        {
            members.Add(new Syn.Constraints.CheckDefinition(Name(check.Name), check.Expression) { Position = _none, Doc = check.Comment });
        }
        foreach (var exclusion in table.ExclusionConstraints)
        {
            members.Add(new Syn.Constraints.ExclusionDefinition(Name(exclusion.Name),
                exclusion.Elements.Select(e => new Syn.Constraints.ExclusionElement(e.Operator, OptionalName(e.Column), e.Expression) { Position = _none }).ToList(),
                OptionalName(exclusion.Method), exclusion.Predicate)
            {
                Position = _none,
                Doc = exclusion.Comment,
            });
        }
        foreach (var index in table.Indexes)
        {
            members.Add(new Syn.Indexes.IndexDefinition(Name(index.Name), index.IsUnique, Keys(index.Columns),
                OptionalName(index.Method),
                Names(index.Include), index.Predicate)
            {
                Position = _none,
                Doc = index.Comment,
            });
        }

        statements.Add(new Syn.Tables.CreateTableStatement(Qualified(schemaName, table.Name), members)
        {
            Position = _none,
            Doc = table.Comment,
        });

        foreach (var grant in table.Grants)
        {
            statements.Add(new Syn.Tables.GrantTableStatement(Privileges(grant.Privileges), Qualified(schemaName, table.Name), Name(grant.Role))
            {
                Position = _none,
            });
        }

        // Triggers are standalone statements (like grants), emitted after their table so the table exists when
        // they are read back.
        foreach (var trigger in table.Triggers)
        {
            statements.Add(Build(schemaName, table.Name, trigger));
        }
    }

    private static void AddView(List<NsqlStatement> statements, SqlIdentifier schemaName, View view)
    {
        statements.Add(new Syn.Views.CreateViewStatement(Qualified(schemaName, view.Name), view.Body, view.IsMaterialized)
        {
            Position = _none,
            Doc = view.Comment,
        });

        // A materialized view's indexes are standalone statements emitted after it (a plain view has none).
        foreach (var index in view.Indexes)
        {
            statements.Add(new Syn.Indexes.CreateIndexStatement(Name(index.Name), index.IsUnique, Qualified(schemaName, view.Name),
                Keys(index.Columns), OptionalName(index.Method),
                Names(index.Include), index.Predicate)
            {
                Position = _none,
                Doc = index.Comment,
            });
        }
    }

    private static Syn.Triggers.CreateTriggerStatement Build(SqlIdentifier schemaName, SqlIdentifier tableName, Trigger trigger)
    {
        Syn.Triggers.TriggerAction action = trigger.Body is { } body
            ? new Syn.Triggers.InlineBodyAction(body) { Position = _none }
            : new Syn.Triggers.ExecuteFunctionAction(
                new QualifiedName(OptionalName(trigger.Function!.Schema), Name(trigger.Function.Name)) { Position = _none },
                trigger.FunctionArguments ?? "")
            {
                Position = _none,
            };

        return new Syn.Triggers.CreateTriggerStatement(Name(trigger.Name), Timing(trigger.Timing), Events(trigger.Events),
            Qualified(schemaName, tableName), action,
            trigger.UpdateOfColumns is { Count: > 0 } updateOf ? Names(updateOf) : null,
            trigger.Level == TriggerLevel.Row ? Syn.Triggers.TriggerLevel.Row : Syn.Triggers.TriggerLevel.Statement,
            trigger.When)
        {
            Position = _none,
            Doc = trigger.Comment,
        };
    }

    private static Syn.Scripts.ScriptStatement Build(Script script)
    {
        Syn.Scripts.ScriptEventClause clause = script switch
        {
            DeploymentScript deployment => new Syn.Scripts.DeploymentEventClause(deployment.Phase == DeploymentPhase.Pre ? Syn.Scripts.DeploymentPhase.Pre : Syn.Scripts.DeploymentPhase.Post)
            {
                Position = _none,
            },
            ChangeScript change => new Syn.Scripts.ChangeEventClause(Trigger(change.Target.Trigger), new MemberPath(OptionalName(change.Target.Schema), Name(change.Target.Table), Name(change.Target.Member)) { Position = _none })
            {
                Position = _none,
            },
            _ => throw new InvalidOperationException($"Unbuildable script '{script.GetType().Name}'."),
        };

        // Only a deployment script carries a run condition; a change-event script renders a bare RUN (null).
        Syn.Scripts.RunCondition? condition = script is DeploymentScript d
            ? d.RunCondition == RunCondition.Once ? Syn.Scripts.RunCondition.Once : Syn.Scripts.RunCondition.Always
            : null;

        return new Syn.Scripts.ScriptStatement(Name(script.Name), condition, clause, script.Sql, script.RunOutsideTransaction)
        {
            Position = _none,
        };
    }

    private static Syn.Extensions.CreateExtensionStatement Build(Extension extension) =>
        new(Name(extension.Name), extension.Version)
        {
            Position = _none,
            Doc = extension.Comment,
        };

    // --- leaf conversions -------------------------------------------------------------

    private static Identifier Name(SqlIdentifier name) => new(name.Value) { Position = _none };

    private static Identifier? OptionalName(SqlIdentifier? name) => name is null ? null : Name(name);

    private static List<Identifier> Names(IReadOnlyList<SqlIdentifier> names) => names.Select(Name).ToList();

    private static QualifiedName Qualified(SqlIdentifier schema, SqlIdentifier name) =>
        new(Name(schema), Name(name)) { Position = _none };

    /// <summary>
    /// Decomposes a type's canonical text (<c>varchar(100)</c>, <c>app.status</c>) into the written form;
    /// the renderer reassembles the same text, so the round-trip is exact.
    /// </summary>
    private static TypeName Type(SqlType type)
    {
        // The type carries its qualifier and arguments as components, so read them straight across — no
        // rendering to a string and splitting it back apart.
        var schema = type.Schema is { } qualifier ? new Identifier(qualifier.Value) { Position = _none } : null;
        var arguments = Arguments(type);
        return new TypeName(schema, new Identifier(type.Name.Value) { Position = _none }, arguments) { Position = _none };
    }

    private static string? Arguments(SqlType type)
    {
        if (type.Precision is { } precision)
        {
            return $"{precision},{type.Scale}";
        }

        return type.Length is { } length ? length.ToString() : null;
    }

    private static Syn.Tables.IdentityOptionsClause? Options(IdentityOptions? options) =>
        options is null ? null : new Syn.Tables.IdentityOptionsClause(options.StartWith, options.IncrementBy, options.MinValue) { Position = _none };

    private static Syn.Sequences.SequenceOptionsClause? Options(SequenceOptions options) =>
        options.DataType is null && options.StartWith is null && options.IncrementBy is null
            && options.MinValue is null && options.MaxValue is null && options.Cache is null && !options.Cycle
            ? null
            : new Syn.Sequences.SequenceOptionsClause(options.DataType is { } type ? Type(type) : null,
                options.StartWith, options.IncrementBy, options.MinValue, options.MaxValue, options.Cache, options.Cycle)
            {
                Position = _none,
            };

    private static List<Syn.Indexes.IndexElement> Keys(IReadOnlyList<IndexColumn> columns) =>
        columns.Select(c => new Syn.Indexes.IndexElement(OptionalName(c.Column), c.Expression, Sort(c.Sort), Nulls(c.Nulls)) { Position = _none }).ToList();

    private static Syn.Constraints.ReferentialAction Action(ReferentialAction action) => action switch
    {
        ReferentialAction.Cascade => Syn.Constraints.ReferentialAction.Cascade,
        ReferentialAction.SetNull => Syn.Constraints.ReferentialAction.SetNull,
        ReferentialAction.SetDefault => Syn.Constraints.ReferentialAction.SetDefault,
        _ => Syn.Constraints.ReferentialAction.NoAction,
    };

    private static Syn.Indexes.IndexSort Sort(IndexSort sort) => sort switch
    {
        IndexSort.Ascending => Syn.Indexes.IndexSort.Ascending,
        IndexSort.Descending => Syn.Indexes.IndexSort.Descending,
        _ => Syn.Indexes.IndexSort.Default,
    };

    private static Syn.Indexes.IndexNulls Nulls(IndexNulls nulls) => nulls switch
    {
        IndexNulls.First => Syn.Indexes.IndexNulls.First,
        IndexNulls.Last => Syn.Indexes.IndexNulls.Last,
        _ => Syn.Indexes.IndexNulls.Default,
    };

    private static Syn.Triggers.TriggerTiming Timing(TriggerTiming timing) => timing switch
    {
        TriggerTiming.Before => Syn.Triggers.TriggerTiming.Before,
        TriggerTiming.After => Syn.Triggers.TriggerTiming.After,
        _ => Syn.Triggers.TriggerTiming.InsteadOf,
    };

    private static Syn.Triggers.TriggerEvent Events(TriggerEvent events)
    {
        var mapped = Syn.Triggers.TriggerEvent.None;
        if (events.HasFlag(TriggerEvent.Insert)) { mapped |= Syn.Triggers.TriggerEvent.Insert; }
        if (events.HasFlag(TriggerEvent.Update)) { mapped |= Syn.Triggers.TriggerEvent.Update; }
        if (events.HasFlag(TriggerEvent.Delete)) { mapped |= Syn.Triggers.TriggerEvent.Delete; }
        if (events.HasFlag(TriggerEvent.Truncate)) { mapped |= Syn.Triggers.TriggerEvent.Truncate; }
        return mapped;
    }

    private static Syn.Tables.TablePrivilege Privileges(TablePrivilege privileges)
    {
        var mapped = Syn.Tables.TablePrivilege.None;
        if (privileges.HasFlag(TablePrivilege.Select)) { mapped |= Syn.Tables.TablePrivilege.Select; }
        if (privileges.HasFlag(TablePrivilege.Insert)) { mapped |= Syn.Tables.TablePrivilege.Insert; }
        if (privileges.HasFlag(TablePrivilege.Update)) { mapped |= Syn.Tables.TablePrivilege.Update; }
        if (privileges.HasFlag(TablePrivilege.Delete)) { mapped |= Syn.Tables.TablePrivilege.Delete; }
        return mapped;
    }

    private static Syn.Scripts.ChangeTrigger Trigger(ChangeTrigger trigger) => trigger switch
    {
        ChangeTrigger.AddColumn => Syn.Scripts.ChangeTrigger.AddColumn,
        ChangeTrigger.AlterColumnType => Syn.Scripts.ChangeTrigger.AlterColumnType,
        _ => Syn.Scripts.ChangeTrigger.AddConstraint,
    };
}
