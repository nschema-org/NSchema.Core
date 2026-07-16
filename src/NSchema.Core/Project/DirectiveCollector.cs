using NSchema.Model;
using NSchema.Project.Model.Directives;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax;
using Syn = NSchema.Project.Nsql.Syntax;

namespace NSchema.Project;

/// <summary>
/// Collects directive statements across a project's documents and builds the <see cref="ProjectDirectives"/>.
/// </summary>
internal sealed class DirectiveCollector
{
    private readonly ProjectedScripts _scripts = new();
    private readonly List<SchemaRenameDirective> _schemaRenames = [];
    private readonly List<SqlIdentifier> _schemaDrops = [];
    private readonly List<SqlIdentifier> _partials = [];
    private readonly List<ObjectRenameDirective> _tableRenames = [];
    private readonly List<ObjectReference> _tableDrops = [];
    private readonly List<MemberRenameDirective> _columnRenames = [];
    private readonly List<ObjectRenameDirective> _viewRenames = [];
    private readonly List<ObjectReference> _viewDrops = [];
    private readonly List<ObjectRenameDirective> _enumRenames = [];
    private readonly List<ObjectReference> _enumDrops = [];
    private readonly List<ObjectRenameDirective> _sequenceRenames = [];
    private readonly List<ObjectReference> _sequenceDrops = [];
    private readonly List<ObjectRenameDirective> _routineRenames = [];
    private readonly List<ObjectReference> _routineDrops = [];
    private readonly List<ObjectRenameDirective> _domainRenames = [];
    private readonly List<ObjectReference> _domainDrops = [];
    private readonly List<ObjectRenameDirective> _compositeTypeRenames = [];
    private readonly List<ObjectReference> _compositeTypeDrops = [];
    private readonly List<SqlIdentifier> _extensionDrops = [];

    /// <summary>
    /// Consumes a directive statement, returning <see langword="false"/> when the statement is not one.
    /// </summary>
    /// <param name="statement">The statement to consume.</param>
    /// <param name="context">The schema the directive binds to, when inside a template.</param>
    public bool TryAdd(NsqlStatement statement, SqlIdentifier? context = null)
    {
        switch (statement)
        {
            case Syn.Scripts.ScriptStatement s:
                DocumentProjector.ProjectScript(s, context, _scripts);
                return true;
            case Syn.Schemas.RenameSchemaStatement s:
                _schemaRenames.Add(new SchemaRenameDirective(Name(s.From), Name(s.To)));
                return true;
            case Syn.Schemas.DropSchemaStatement s:
                AddUnique(_schemaDrops, Name(s.Name));
                return true;
            case Syn.Schemas.PartialSchemaStatement s:
                AddUnique(_partials, Name(s.Schema));
                return true;
            case Syn.Tables.RenameTableStatement s:
                _tableRenames.Add(new ObjectRenameDirective(Reference(s.From, context), Name(s.To)));
                return true;
            case Syn.Tables.RenameColumnStatement s:
                _columnRenames.Add(new MemberRenameDirective(
                    new MemberReference(Bind(s.From.Schema, context), Name(s.From.Table), Name(s.From.Member)),
                    Name(s.To)));
                return true;
            case Syn.Tables.DropTableStatement s:
                AddUnique(_tableDrops, Reference(s.Name, context));
                return true;
            case Syn.Views.RenameViewStatement s:
                _viewRenames.Add(new ObjectRenameDirective(Reference(s.From, context), Name(s.To)));
                return true;
            case Syn.Views.DropViewStatement s:
                AddUnique(_viewDrops, Reference(s.Name, context));
                return true;
            case Syn.Enums.RenameEnumStatement s:
                _enumRenames.Add(new ObjectRenameDirective(Reference(s.From, context), Name(s.To)));
                return true;
            case Syn.Enums.DropEnumStatement s:
                AddUnique(_enumDrops, Reference(s.Name, context));
                return true;
            case Syn.Sequences.RenameSequenceStatement s:
                _sequenceRenames.Add(new ObjectRenameDirective(Reference(s.From, context), Name(s.To)));
                return true;
            case Syn.Sequences.DropSequenceStatement s:
                AddUnique(_sequenceDrops, Reference(s.Name, context));
                return true;
            case Syn.Routines.RenameRoutineStatement s:
                _routineRenames.Add(new ObjectRenameDirective(Reference(s.From, context), Name(s.To)));
                return true;
            case Syn.Routines.DropRoutineStatement s:
                AddUnique(_routineDrops, Reference(s.Name, context));
                return true;
            case Syn.Domains.RenameDomainStatement s:
                _domainRenames.Add(new ObjectRenameDirective(Reference(s.From, context), Name(s.To)));
                return true;
            case Syn.Domains.DropDomainStatement s:
                AddUnique(_domainDrops, Reference(s.Name, context));
                return true;
            case Syn.CompositeTypes.RenameCompositeTypeStatement s:
                _compositeTypeRenames.Add(new ObjectRenameDirective(Reference(s.From, context), Name(s.To)));
                return true;
            case Syn.CompositeTypes.DropCompositeTypeStatement s:
                AddUnique(_compositeTypeDrops, Reference(s.Name, context));
                return true;
            case Syn.Extensions.DropExtensionStatement s:
                AddUnique(_extensionDrops, Name(s.Name));
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Merges another set of directives into this collector.
    /// </summary>
    public void Absorb(ProjectDirectives other)
    {
        _scripts.Change.AddRange(other.Tables.ChangeScripts);
        _scripts.Deployment.AddRange(other.DeploymentScripts);
        _schemaRenames.AddRange(other.Schemas.Renames);
        _schemaDrops.AddRange(other.Schemas.Drops);
        _partials.AddRange(other.Schemas.Partials);
        _tableRenames.AddRange(other.Tables.Renames);
        _tableDrops.AddRange(other.Tables.Drops);
        _columnRenames.AddRange(other.Tables.ColumnRenames);
        _viewRenames.AddRange(other.Views.Renames);
        _viewDrops.AddRange(other.Views.Drops);
        _enumRenames.AddRange(other.Enums.Renames);
        _enumDrops.AddRange(other.Enums.Drops);
        _sequenceRenames.AddRange(other.Sequences.Renames);
        _sequenceDrops.AddRange(other.Sequences.Drops);
        _routineRenames.AddRange(other.Routines.Renames);
        _routineDrops.AddRange(other.Routines.Drops);
        _domainRenames.AddRange(other.Domains.Renames);
        _domainDrops.AddRange(other.Domains.Drops);
        _compositeTypeRenames.AddRange(other.CompositeTypes.Renames);
        _compositeTypeDrops.AddRange(other.CompositeTypes.Drops);
        _extensionDrops.AddRange(other.Extensions.Drops);
    }

    public ProjectDirectives Build() => new(
        new SchemaDirectives(_schemaRenames, _schemaDrops, _partials),
        new TableDirectives(_tableRenames, _tableDrops, _columnRenames, _scripts.Change),
        new ViewDirectives(_viewRenames, _viewDrops),
        new EnumDirectives(_enumRenames, _enumDrops),
        new SequenceDirectives(_sequenceRenames, _sequenceDrops),
        new RoutineDirectives(_routineRenames, _routineDrops),
        new DomainDirectives(_domainRenames, _domainDrops),
        new CompositeTypeDirectives(_compositeTypeRenames, _compositeTypeDrops),
        new ExtensionDirectives(_extensionDrops),
        _scripts.Deployment
    );

    // A repeated drop is idempotent, as it always has been across files; repeated renames are conflicts and
    // stay in the lists for validation to catch.
    private static void AddUnique<T>(List<T> list, T item)
    {
        if (!list.Contains(item))
        {
            list.Add(item);
        }
    }

    private static SqlIdentifier Name(Identifier identifier) => new(identifier.Value);

    /// <summary>
    /// Translates a directive's qualified name into an address, binding an unqualified name (only legal inside
    /// a template body) to the applied schema.
    /// </summary>
    private static ObjectReference Reference(QualifiedName name, SqlIdentifier? context) =>
        new(Bind(name.Schema, context), Name(name.Name));

    /// <summary>
    /// Resolves a directive's schema: the written one, or the applied schema inside a template body.
    /// </summary>
    private static SqlIdentifier Bind(Identifier? schema, SqlIdentifier? context) =>
        schema is { } s ? Name(s)
        : context ?? throw new InvalidOperationException("Directive reference has no schema part outside a template.");
}
