using NSchema.Project.Domain;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.CompositeTypes;
using NSchema.Project.Domain.Models.Constraints;
using NSchema.Project.Domain.Models.Domains;
using NSchema.Project.Domain.Models.Enums;
using NSchema.Project.Domain.Models.Extensions;
using NSchema.Project.Domain.Models.Indexes;
using NSchema.Project.Domain.Models.Routines;
using NSchema.Project.Domain.Models.Scripts;
using NSchema.Project.Domain.Models.Sequences;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Triggers;
using NSchema.Project.Domain.Models.Views;
using Syn = NSchema.Project.Nsql.Syntax;

namespace NSchema.Project.Nsql;

/// <summary>
/// Projects a parsed document into the domain.
/// </summary>
internal static class DocumentProjector
{
    /// <summary>
    /// Projects one declaration statement (create or grant) into the accumulator. Directives never reach here. Inside a template body,
    /// <paramref name="context"/> is the placeholder schema that unqualified names bind to.
    /// </summary>
    public static void ProjectStatement(Syn.NsqlStatement statement, DatabaseAccumulator schemas, SqlIdentifier? context)
    {
        switch (statement)
        {
            case Syn.Schemas.CreateSchemaStatement s:
                schemas.DeclareSchema(Name(s.Name), s.Doc, s.Name.Position);
                break;
            case Syn.Tables.CreateTableStatement s:
                ProjectTable(s, schemas, context);
                break;
            case Syn.Views.CreateViewStatement s:
                {
                    var (schema, name) = Bind(s.Name, context);
                    var dependsOn = ViewDependencyExtractor.Extract(s.Body.Value, schema);
                    schemas.AddView(schema, new View(name, s.Body, s.Doc, dependsOn, s.IsMaterialized), s.Name.Position);
                    break;
                }
            case Syn.Indexes.CreateIndexStatement s:
                {
                    var (schema, relation) = Bind(s.On, context);
                    schemas.AddIndex(schema, relation, ProjectIndex(s.Name, s.IsUnique, s.Columns, s.Method, s.Include, s.Predicate, s.Doc), s.Name.Position);
                    break;
                }
            case Syn.Routines.CreateRoutineStatement s:
                {
                    var (schema, name) = Bind(s.Name, context);
                    schemas.AddRoutine(schema, new Routine(name, Map(s.Kind), s.Arguments, s.Definition, s.Doc), s.Name.Position);
                    break;
                }
            case Syn.Enums.CreateEnumStatement s:
                {
                    var (schema, name) = Bind(s.Name, context);
                    schemas.AddEnum(schema, new EnumType(name, s.Values, s.Doc), s.Name.Position);
                    break;
                }
            case Syn.Domains.CreateDomainStatement s:
                {
                    var (schema, name) = Bind(s.Name, context);
                    var checks = s.Checks.Select(c => new CheckConstraint(Name(c.Name), c.Expression)).ToList();
                    schemas.AddDomain(schema, new DomainType(name, ParseType(s.Type), s.Default, s.NotNull, checks, s.Doc), s.Name.Position);
                    break;
                }
            case Syn.CompositeTypes.CreateCompositeTypeStatement s:
                {
                    var (schema, name) = Bind(s.Name, context);
                    var fields = s.Fields.Select(f => new CompositeField(Name(f.Name), ParseType(f.Type))).ToList();
                    schemas.AddCompositeType(schema, new CompositeType(name, fields, s.Doc), s.Name.Position);
                    break;
                }
            case Syn.Sequences.CreateSequenceStatement s:
                {
                    var (schema, name) = Bind(s.Name, context);
                    schemas.AddSequence(schema, new Sequence(name, ProjectSequenceOptions(s.Options), s.Doc), s.Name.Position);
                    break;
                }
            case Syn.Extensions.CreateExtensionStatement s:
                schemas.AddExtension(new Extension(Name(s.Name), s.Version, s.Doc), s.Name.Position);
                break;
            case Syn.Triggers.CreateTriggerStatement s:
                {
                    var (schema, table) = Bind(s.On, context);
                    schemas.AddTrigger(schema, table, ProjectTrigger(s), s.Name.Position);
                    break;
                }
            case Syn.Schemas.GrantSchemaUsageStatement s:
                schemas.AddSchemaGrant(Name(s.Schema), Name(s.Role));
                break;
            case Syn.Tables.GrantTableStatement s:
                {
                    var (schema, table) = Bind(s.On, context);
                    schemas.AddTableGrant(schema, table, new TableGrant(Name(s.Role), Map(s.Privileges)), s.On.Position);
                    break;
                }
            default:
                throw new InvalidOperationException($"Unprojectable statement '{statement.GetType().Name}'.");
        }
    }

    public static (Table Table, List<(SqlIdentifier TemplateName, int ColumnPosition)> Includes) ProjectTableMembers(
        SqlIdentifier name, string? doc, IReadOnlyList<Syn.Tables.TableMember> members, SqlIdentifier? context = null)
    {
        PrimaryKey? primaryKey = null;
        var columns = new List<Column>();
        var foreignKeys = new List<ForeignKey>();
        var uniqueConstraints = new List<UniqueConstraint>();
        var checkConstraints = new List<CheckConstraint>();
        var exclusionConstraints = new List<ExclusionConstraint>();
        var indexes = new List<TableIndex>();
        var includes = new List<(SqlIdentifier TemplateName, int ColumnPosition)>();

        foreach (var member in members)
        {
            switch (member)
            {
                case Syn.Tables.ColumnDefinition m:
                    columns.Add(new Column(Name(m.Name), ParseType(m.Type), m.IsNullable, m.IsIdentity, m.Default,
                        m.Doc, ProjectIdentityOptions(m.IdentityOptions), m.Generated));
                    break;
                case Syn.Constraints.PrimaryKeyDefinition m:
                    primaryKey = new PrimaryKey(Name(m.Name), Names(m.Columns), m.Doc);
                    break;
                case Syn.Constraints.ForeignKeyDefinition m:
                    {
                        // A foreign key's referenced table binds like any other name (the context inside a template
                        // body; the include placeholder when projecting a table template's members).
                        var refSchema = m.References.Schema is { } qualifier
                            ? new SqlIdentifier(qualifier.Value)
                            : context ?? SchemaToken.TargetSchemaPlaceholder;
                        foreignKeys.Add(new ForeignKey(Name(m.Name), Names(m.Columns), refSchema, Name(m.References.Name),
                            Names(m.ReferencedColumns), Map(m.OnDelete), Map(m.OnUpdate), m.Doc));
                        break;
                    }
                case Syn.Constraints.UniqueDefinition m:
                    uniqueConstraints.Add(new UniqueConstraint(Name(m.Name), Names(m.Columns), m.Doc));
                    break;
                case Syn.Constraints.CheckDefinition m:
                    checkConstraints.Add(new CheckConstraint(Name(m.Name), m.Expression, m.Doc));
                    break;
                case Syn.Constraints.ExclusionDefinition m:
                    exclusionConstraints.Add(new ExclusionConstraint(Name(m.Name),
                        m.Elements.Select(e => new ExclusionElement(e.Operator, OptionalName(e.Column), e.Expression)).ToList(),
                        m.Method?.Value, m.Predicate, m.Doc));
                    break;
                case Syn.Indexes.IndexDefinition m:
                    indexes.Add(ProjectIndex(m.Name, m.IsUnique, m.Columns, m.Method, m.Include, m.Predicate, m.Doc));
                    break;
                case Syn.Templates.IncludeMember m:
                    includes.Add((Name(m.TemplateName), columns.Count));
                    break;
                default:
                    throw new InvalidOperationException($"Unprojectable table member '{member.GetType().Name}'.");
            }
        }

        var table = new Table(name, primaryKey, doc,
            columns, foreignKeys, uniqueConstraints, checkConstraints, exclusionConstraints, indexes);
        return (table, includes);
    }

    /// <summary>
    /// Projects a SCRIPT directive into its kind bucket. Inside a template instance, <paramref name="context"/>
    /// is the applied schema: it scopes the script and substitutes the <c>{schema}</c> token in the body.
    /// </summary>
    public static void ProjectScript(Syn.Scripts.ScriptStatement statement, SqlIdentifier? context, ProjectedScripts scripts)
    {
        // Outside a template, context is null and the body is verbatim; inside one, the applied schema
        // substitutes the {schema} token and scopes the run.
        var sql = context is null ? statement.Body : SchemaToken.Instantiate(statement.Body, context);
        switch (statement.Event)
        {
            case Syn.Scripts.DeploymentEventClause deployment:
                // A hand-written deployment script is global (null scope); a templated one scopes to the
                // applied schema. A bare RUN (null condition) is the default, RUN ALWAYS.
                scripts.Add(new DeploymentScript(Name(statement.Name), sql, context, Map(deployment.Phase))
                {
                    RunOutsideTransaction = statement.RunOutsideTransaction,
                    RunCondition = Map(statement.RunCondition),
                });
                break;
            case Syn.Scripts.ChangeEventClause change:
                // A change-event script has no run condition; the node's constructor guarantees none was written.
                scripts.Add(new ChangeScript(Name(statement.Name), sql,
                    change.Path.Schema is { } schema ? new SqlIdentifier(schema.Value) : context,
                    Map(change.Trigger), Name(change.Path.Table), Name(change.Path.Member))
                {
                    RunOutsideTransaction = statement.RunOutsideTransaction,
                });
                break;
            default:
                throw new InvalidOperationException($"Unprojectable script event '{statement.Event.GetType().Name}'.");
        }
    }

    /// <summary>
    /// Validates a schema template's body by projecting it against the placeholder: internal duplicates
    /// surface through the accumulator, and a qualified declaration inside the body is rejected — it would
    /// create the same object once per application.
    /// </summary>
    public static IReadOnlyList<NsqlDiagnostic> ValidateTemplateBody(Syn.Templates.SchemaTemplateStatement statement)
    {
        var body = new DatabaseAccumulator();
        var directives = new DirectiveCollector();
        foreach (var inner in statement.Statements)
        {
            // Only declarations are validated for stray qualification; a directive (a script, a rename, a
            // drop) carries no schema declaration, so it is filtered out through the collector and skipped.
            if (!directives.TryAdd(inner, SchemaToken.TargetSchemaPlaceholder))
            {
                ProjectStatement(inner, body, SchemaToken.TargetSchemaPlaceholder);
            }
        }

        var fragment = body.Build();
        var diagnostics = body.Diagnostics.ToList();
        var stray = fragment.Schemas.FirstOrDefault(s => s.Name != SchemaToken.TargetSchemaPlaceholder);
        if (stray is not null)
        {
            diagnostics.Add(new NsqlDiagnostic("project",
                $"Template '{statement.Name.Value}' declares objects in schema '{stray.Name}'; objects inside a template must use " +
                $"unqualified names so they are created in each schema the template is applied to. (at {statement.Name.Position}).",
                DiagnosticSeverity.Error, statement.Name.Position));
        }
        return diagnostics;
    }

    private static void ProjectTable(Syn.Tables.CreateTableStatement statement, DatabaseAccumulator schemas, SqlIdentifier? context)
    {
        var (schema, name) = Bind(statement.Name, context);
        var (table, includes) = ProjectTableMembers(name, statement.Doc, statement.Members, context);
        schemas.AddTable(schema, table, statement.Name.Position);
        foreach (var (templateName, columnPosition) in includes)
        {
            schemas.AddInclude(new TemplateInclude(schema, name, templateName, columnPosition));
        }
    }

    private static TableIndex ProjectIndex(Syn.Identifier name, bool isUnique, IReadOnlyList<Syn.Indexes.IndexElement> columns,
        Syn.Identifier? method, IReadOnlyList<Syn.Identifier>? include, SqlText? predicate, string? doc)
    {
        var keys = columns.Select(c => new IndexColumn(OptionalName(c.Column), c.Expression, Map(c.Sort), Map(c.Nulls))).ToList();
        return new TableIndex(Name(name), keys, isUnique, doc, predicate, method?.Value, Names(include ?? []));
    }

    private static Trigger ProjectTrigger(Syn.Triggers.CreateTriggerStatement statement)
    {
        RoutineReference? function = null;
        SqlText? functionArguments = null;
        SqlText? body = null;
        switch (statement.Action)
        {
            case Syn.Triggers.ExecuteFunctionAction action:
                function = new RoutineReference(OptionalName(action.Function.Schema), Name(action.Function.Name));
                functionArguments = action.Arguments.Value.Length == 0 ? null : action.Arguments;
                break;
            case Syn.Triggers.InlineBodyAction action:
                body = action.Body;
                break;
        }

        return new Trigger(Name(statement.Name), Map(statement.Timing), Map(statement.Events), function,
            Map(statement.Level), statement.UpdateOfColumns is { } updateOf ? Names(updateOf) : null,
            statement.When, functionArguments, statement.Doc, body);
    }

    // --- name binding and small mappers ----------------------------------------------

    private static SqlIdentifier Name(Syn.Identifier identifier) => new(identifier.Value);

    private static SqlIdentifier? OptionalName(Syn.Identifier? identifier) =>
        identifier is null ? null : new SqlIdentifier(identifier.Value);

    private static List<SqlIdentifier> Names(IReadOnlyList<Syn.Identifier> identifiers) =>
        identifiers.Select(Name).ToList();

    /// <summary>
    /// Binds an optionally qualified name: written qualification wins; an unqualified name binds to the
    /// template placeholder (the parser rejects unqualified names outside template bodies).
    /// </summary>
    private static (SqlIdentifier Schema, SqlIdentifier Name) Bind(Syn.QualifiedName name, SqlIdentifier? context) =>
        (name.Schema is { } schema ? new SqlIdentifier(schema.Value) : context ?? SchemaToken.TargetSchemaPlaceholder,
         new SqlIdentifier(name.Name.Value));

    private static SqlType ParseType(Syn.TypeName type)
    {
        var text = type.Schema is { } schema ? $"{schema.Value}.{type.Name.Value}" : type.Name.Value;
        if (type.Arguments is { } arguments)
        {
            text += $"({arguments})";
        }
        return SqlType.Parse(text);
    }

    private static IdentityOptions? ProjectIdentityOptions(Syn.Tables.IdentityOptionsClause? options) =>
        options is null ? null : new IdentityOptions(options.Start, options.MinValue, options.Increment);

    private static SequenceOptions? ProjectSequenceOptions(Syn.Sequences.SequenceOptionsClause? options) =>
        options is null
            ? null
            : new SequenceOptions(options.As is { } dataType ? SqlType.Parse(dataType.Name.Value) : null,
                options.Start, options.Increment, options.MinValue, options.MaxValue, options.Cache, options.Cycle);

    private static RoutineKind Map(Syn.Routines.RoutineKind kind) =>
        kind == Syn.Routines.RoutineKind.Procedure ? RoutineKind.Procedure : RoutineKind.Function;

    private static ReferentialAction Map(Syn.Constraints.ReferentialAction action) => action switch
    {
        Syn.Constraints.ReferentialAction.Cascade => ReferentialAction.Cascade,
        Syn.Constraints.ReferentialAction.SetNull => ReferentialAction.SetNull,
        Syn.Constraints.ReferentialAction.SetDefault => ReferentialAction.SetDefault,
        _ => ReferentialAction.NoAction,
    };

    private static IndexSort Map(Syn.Indexes.IndexSort sort) => sort switch
    {
        Syn.Indexes.IndexSort.Ascending => IndexSort.Ascending,
        Syn.Indexes.IndexSort.Descending => IndexSort.Descending,
        _ => IndexSort.Default,
    };

    private static IndexNulls Map(Syn.Indexes.IndexNulls nulls) => nulls switch
    {
        Syn.Indexes.IndexNulls.First => IndexNulls.First,
        Syn.Indexes.IndexNulls.Last => IndexNulls.Last,
        _ => IndexNulls.Default,
    };

    private static TriggerTiming Map(Syn.Triggers.TriggerTiming timing) => timing switch
    {
        Syn.Triggers.TriggerTiming.Before => TriggerTiming.Before,
        Syn.Triggers.TriggerTiming.After => TriggerTiming.After,
        _ => TriggerTiming.InsteadOf,
    };

    private static TriggerEvent Map(Syn.Triggers.TriggerEvent events)
    {
        var mapped = TriggerEvent.None;
        if (events.HasFlag(Syn.Triggers.TriggerEvent.Insert)) { mapped |= TriggerEvent.Insert; }
        if (events.HasFlag(Syn.Triggers.TriggerEvent.Update)) { mapped |= TriggerEvent.Update; }
        if (events.HasFlag(Syn.Triggers.TriggerEvent.Delete)) { mapped |= TriggerEvent.Delete; }
        if (events.HasFlag(Syn.Triggers.TriggerEvent.Truncate)) { mapped |= TriggerEvent.Truncate; }
        return mapped;
    }

    private static TriggerLevel Map(Syn.Triggers.TriggerLevel level) =>
        level == Syn.Triggers.TriggerLevel.Row ? TriggerLevel.Row : TriggerLevel.Statement;

    private static TablePrivilege Map(Syn.Tables.TablePrivilege privileges)
    {
        var mapped = TablePrivilege.None;
        if (privileges.HasFlag(Syn.Tables.TablePrivilege.Select)) { mapped |= TablePrivilege.Select; }
        if (privileges.HasFlag(Syn.Tables.TablePrivilege.Insert)) { mapped |= TablePrivilege.Insert; }
        if (privileges.HasFlag(Syn.Tables.TablePrivilege.Update)) { mapped |= TablePrivilege.Update; }
        if (privileges.HasFlag(Syn.Tables.TablePrivilege.Delete)) { mapped |= TablePrivilege.Delete; }
        return mapped;
    }

    private static DeploymentPhase Map(Syn.Scripts.DeploymentPhase phase) =>
        phase == Syn.Scripts.DeploymentPhase.Pre ? DeploymentPhase.Pre : DeploymentPhase.Post;

    private static ChangeTrigger Map(Syn.Scripts.ChangeTrigger trigger) => trigger switch
    {
        Syn.Scripts.ChangeTrigger.AddColumn => ChangeTrigger.AddColumn,
        Syn.Scripts.ChangeTrigger.AlterColumnType => ChangeTrigger.AlterColumnType,
        _ => ChangeTrigger.AddConstraint,
    };

    private static RunCondition Map(Syn.Scripts.RunCondition? condition) =>
        condition == Syn.Scripts.RunCondition.Once ? RunCondition.Once : RunCondition.Always;
}
