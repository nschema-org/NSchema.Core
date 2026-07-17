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
    private readonly List<SchemaDropDirective> _schemaDrops = [];
    private readonly List<SchemaPartialDirective> _partials = [];
    private readonly List<ObjectRenameDirective> _renames = [];
    private readonly List<ObjectDropDirective> _drops = [];
    private readonly List<MemberRenameDirective> _columnRenames = [];
    private readonly List<ExtensionDropDirective> _extensionDrops = [];

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
                AddUnique(_schemaDrops, new SchemaDropDirective(Name(s.Name)));
                return true;
            case Syn.Schemas.PartialSchemaStatement s:
                AddUnique(_partials, new SchemaPartialDirective(Name(s.Schema)));
                return true;
            case RenameObjectStatement s:
                _renames.Add(new ObjectRenameDirective(s.Kind, Reference(s.From, context), Name(s.To)));
                return true;
            case DropObjectStatement s:
                AddUnique(_drops, new ObjectDropDirective(s.Kind, Reference(s.Name, context)));
                return true;
            case Syn.Tables.RenameColumnStatement s:
                _columnRenames.Add(new MemberRenameDirective(
                    new MemberAddress(Bind(s.From.Schema, context), Name(s.From.Table), Name(s.From.Member)),
                    Name(s.To)));
                return true;
            case Syn.Extensions.DropExtensionStatement s:
                AddUnique(_extensionDrops, new ExtensionDropDirective(Name(s.Name)));
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
        _scripts.Change.AddRange(other.ChangeScripts);
        _scripts.Deployment.AddRange(other.DeploymentScripts);
        _schemaRenames.AddRange(other.Schemas.Renames);
        _schemaDrops.AddRange(other.Schemas.Drops);
        _partials.AddRange(other.Schemas.Partials);
        _renames.AddRange(other.Renames);
        _drops.AddRange(other.Drops);
        _columnRenames.AddRange(other.ColumnRenames);
        _extensionDrops.AddRange(other.ExtensionDrops);
    }

    public ProjectDirectives Build() => new(
        new SchemaDirectives(_schemaRenames, _schemaDrops, _partials),
        _renames,
        _drops,
        _columnRenames,
        _extensionDrops,
        _scripts.Change,
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
    private static ObjectAddress Reference(QualifiedName name, SqlIdentifier? context) =>
        new(Bind(name.Schema, context), Name(name.Name));

    /// <summary>
    /// Resolves a directive's schema: the written one, or the applied schema inside a template body.
    /// </summary>
    private static SqlIdentifier Bind(Identifier? schema, SqlIdentifier? context) =>
        schema is { } s ? Name(s)
        : context ?? throw new InvalidOperationException("Directive reference has no schema part outside a template.");
}
