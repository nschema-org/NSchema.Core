using NSchema.Model.Routines;
using NSchema.Operations;
using NSchema.Operations.Progress;
using NSchema.Project.Domain.Models;
using Syntax = NSchema.Project.Nsql.Syntax;

namespace NSchema.Tests.Architecture;

/// <summary>
/// The executable v5 migration map: every public type in Core is assigned its 5.0 namespace (or its
/// removal). The completeness test runs green today and fails when a new public type ships without a
/// decided home; the placement test activates in the move phase and fails until every type has
/// arrived. The position rules the targets encode are described in ARCHITECTURE.md.
/// </summary>
public sealed class NamespaceMigrationMapTests
{
    /// <summary>
    /// The type does not survive to 5.0 (deleted, merged, or superseded). The comment on the entry
    /// says which.
    /// </summary>
    private const string Removed = "(removed in 5.0)";

    private const string Root = "NSchema";
    private const string ProjectRoot = "NSchema.Project";
    private const string Model = "NSchema.Model";
    private const string ModelServices = "NSchema.Model.Services";
    private const string ProjectModels = "NSchema.Project.Domain.Models";
    private const string ProjectNsql = "NSchema.Project.Nsql";
    private const string ProjectNsqlSyntax = "NSchema.Project.Nsql.Syntax";
    private const string OperationsProgress = "NSchema.Operations.Progress";
    private const string ProjectPolicies = "NSchema.Project.Policies";
    private const string DeploymentRoot = "NSchema.Deployment";
    private const string DeploymentBackends = "NSchema.Deployment.Backends";
    private const string Diff = "NSchema.Diff";
    private const string DiffReaderNs = "NSchema.Diff.Reader";
    private const string DiffModels = "NSchema.Diff.Domain.Models";
    private const string PlanPolicies = "NSchema.Plan.Policies";
    private const string Plan = "NSchema.Plan";
    private const string PlanBackends = "NSchema.Plan.Backends";
    private const string PlanModels = "NSchema.Plan.Domain.Models";
    private const string PlanFile = "NSchema.Plan.PlanFile";
    private const string Apply = "NSchema.Apply";
    private const string StateLocks = "NSchema.State.Locks";
    private const string StateLocksBackends = "NSchema.State.Locks.Backends";
    private const string StateRoot = "NSchema.State";
    private const string StateBackends = "NSchema.State.Backends";
    private const string StateModels = "NSchema.State.Domain.Models";
    private const string Operations = "NSchema.Operations";
    private const string Plugins = "NSchema.Plugins";

    private static readonly IReadOnlyDictionary<Type, string> _map = new Dictionary<Type, string>
    {
        // ── Root grammar: composition entry points + outcome vocabulary (the closed list). ──
        [typeof(NSchemaApplication)] = Root,
        [typeof(NSchemaApplicationBuilder)] = Root,
        [typeof(NSchemaApplicationOptions)] = Root,
        [typeof(NSchema.Result)] = Root,
        [typeof(NSchema.Result<>)] = Root,
        [typeof(NSchema.Result<,>)] = Root, // the diagnostic-typed result — folds upward as Result<T> without translation
        [typeof(NSchema.Diagnostic)] = Root,
        [typeof(NSchema.DiagnosticSeverity)] = Root,
        [typeof(PolicyEnforcement)] = Root, // absorbed DestructiveActionPolicy

        // ── ProjectDefinition: the declared desired state — seam and messages at the cluster root. ──
        [typeof(NSchema.Project.IProjectProvider)] = ProjectRoot,
        [typeof(NSchema.Project.Domain.Models.ProjectDefinition)] = ProjectModels, // the project aggregate — raw domain vocabulary the provider returns (the DatabaseState parallel), not a seam-shaped message
        [typeof(NSchema.Model.DatabaseScope)] = Model,
        [typeof(NSchema.Model.ValueObject<>)] = Model, // the single-value primitive base — value equality, renders as its value
        [typeof(NSchema.Model.SqlIdentifier)] = Model, // the identifier vocabulary primitive — case-insensitive equality baked in, shared by every lane
        [typeof(NSchema.Model.SqlText)] = Model, // the opaque-SQL vocabulary primitive — verbatim foreign SQL, ordinal equality (data)
        [typeof(NSchema.Model.Address)] = Model, // the address base — shaped by containment, carrying neither kind nor name-space (both engine-specific)
        [typeof(NSchema.Model.ObjectReference)] = Model, // the address of a schema-level object — always fully qualified
        [typeof(RoutineReference)] = Model + ".Routines", // a routine reference as written — unqualified resolves via the engine's search path; homed with the routines it names, now the dependency graph resolves them too

        // ── Current: the source the project is diffed against — observed live, or recorded. ──
        [typeof(NSchema.Deployment.IDatabaseProvider)] = DeploymentRoot,
        [typeof(NSchema.Deployment.Backends.IDatabaseIntrospector)] = DeploymentBackends,

        // ── ProjectDefinition.Nsql: the project language — machinery, documents, and the full syntax tree. ──
        [typeof(NSchema.Project.Nsql.NsqlWriter)] = ProjectNsql, // domain → syntax → text; SyntaxBuilder is its first half
        [typeof(NSchema.Project.Nsql.NsqlFormatter)] = ProjectNsql, // token-stream reformatter, shares the lexer
        [typeof(NSchema.Project.Nsql.SourcePosition)] = ProjectNsql,
        // Template constructs are language features, not domain models; reshaped as AST nodes.

        // ── ProjectDefinition.Nsql: the NSchema language — documents at the lane root, the syntax tree
        // under .Syntax — the language lane, born in its 5.0 home. ──
        [typeof(NSchema.Project.Nsql.NsqlDocument)] = ProjectNsql,
        [typeof(NSchema.Project.Nsql.NsqlConfigDocument)] = ProjectNsql,
        [typeof(NSchema.Project.Nsql.NsqlReader)] = ProjectNsql, // the file-aware read seam — stamps provenance onto documents and diagnostics
        [typeof(NSchema.Project.Nsql.NsqlDiagnostic)] = ProjectNsql, // the lane's positioned diagnostic — file + SourcePosition structurally
        [typeof(Syntax.NsqlNode)] = ProjectNsqlSyntax,
        [typeof(Syntax.NsqlStatement)] = ProjectNsqlSyntax,
        [typeof(Syntax.Identifier)] = ProjectNsqlSyntax,
        [typeof(Syntax.QualifiedName)] = ProjectNsqlSyntax,
        [typeof(Syntax.MemberPath)] = ProjectNsqlSyntax,
        [typeof(Syntax.TypeName)] = ProjectNsqlSyntax,
        [typeof(Syntax.Schemas.CreateSchemaStatement)] = ProjectNsqlSyntax + ".Schemas",
        [typeof(Syntax.Schemas.RenameSchemaStatement)] = ProjectNsqlSyntax + ".Schemas",
        [typeof(Syntax.Schemas.PartialSchemaStatement)] = ProjectNsqlSyntax + ".Schemas",
        [typeof(Syntax.Schemas.GrantSchemaUsageStatement)] = ProjectNsqlSyntax + ".Schemas",
        [typeof(Syntax.Tables.CreateTableStatement)] = ProjectNsqlSyntax + ".Tables",
        [typeof(Syntax.Tables.RenameTableStatement)] = ProjectNsqlSyntax + ".Tables",
        [typeof(Syntax.Tables.RenameColumnStatement)] = ProjectNsqlSyntax + ".Tables",
        [typeof(Syntax.Tables.TableMember)] = ProjectNsqlSyntax + ".Tables",
        [typeof(Syntax.Tables.ColumnDefinition)] = ProjectNsqlSyntax + ".Tables",
        [typeof(Syntax.Tables.IdentityOptionsClause)] = ProjectNsqlSyntax + ".Tables",
        [typeof(Syntax.Templates.IncludeMember)] = ProjectNsqlSyntax + ".Templates",
        [typeof(Syntax.Constraints.PrimaryKeyDefinition)] = ProjectNsqlSyntax + ".Constraints",
        [typeof(Syntax.Constraints.ForeignKeyDefinition)] = ProjectNsqlSyntax + ".Constraints",
        [typeof(Syntax.Constraints.UniqueDefinition)] = ProjectNsqlSyntax + ".Constraints",
        [typeof(Syntax.Constraints.CheckDefinition)] = ProjectNsqlSyntax + ".Constraints",
        [typeof(Syntax.Constraints.ExclusionDefinition)] = ProjectNsqlSyntax + ".Constraints",
        [typeof(Syntax.Constraints.ExclusionElement)] = ProjectNsqlSyntax + ".Constraints",
        [typeof(Syntax.Constraints.ReferentialAction)] = ProjectNsqlSyntax + ".Constraints",
        [typeof(Syntax.Indexes.IndexDefinition)] = ProjectNsqlSyntax + ".Indexes",
        [typeof(Syntax.Indexes.CreateIndexStatement)] = ProjectNsqlSyntax + ".Indexes",
        [typeof(Syntax.Indexes.IndexElement)] = ProjectNsqlSyntax + ".Indexes",
        [typeof(Syntax.Indexes.IndexSort)] = ProjectNsqlSyntax + ".Indexes",
        [typeof(Syntax.Indexes.IndexNulls)] = ProjectNsqlSyntax + ".Indexes",
        [typeof(Syntax.Views.CreateViewStatement)] = ProjectNsqlSyntax + ".Views",
        [typeof(Syntax.Routines.CreateRoutineStatement)] = ProjectNsqlSyntax + ".Routines",
        [typeof(Syntax.Routines.RoutineKind)] = ProjectNsqlSyntax + ".Routines",
        [typeof(Syntax.Enums.CreateEnumStatement)] = ProjectNsqlSyntax + ".Enums",
        [typeof(Syntax.Domains.CreateDomainStatement)] = ProjectNsqlSyntax + ".Domains",
        [typeof(Syntax.CompositeTypes.CreateCompositeTypeStatement)] = ProjectNsqlSyntax + ".CompositeTypes",
        [typeof(Syntax.CompositeTypes.CompositeFieldDefinition)] = ProjectNsqlSyntax + ".CompositeTypes",
        [typeof(Syntax.Sequences.CreateSequenceStatement)] = ProjectNsqlSyntax + ".Sequences",
        [typeof(Syntax.Sequences.SequenceOptionsClause)] = ProjectNsqlSyntax + ".Sequences",
        [typeof(Syntax.Extensions.CreateExtensionStatement)] = ProjectNsqlSyntax + ".Extensions",
        [typeof(Syntax.Triggers.CreateTriggerStatement)] = ProjectNsqlSyntax + ".Triggers",
        [typeof(Syntax.Triggers.TriggerAction)] = ProjectNsqlSyntax + ".Triggers",
        [typeof(Syntax.Triggers.ExecuteFunctionAction)] = ProjectNsqlSyntax + ".Triggers",
        [typeof(Syntax.Triggers.InlineBodyAction)] = ProjectNsqlSyntax + ".Triggers",
        [typeof(Syntax.Triggers.TriggerTiming)] = ProjectNsqlSyntax + ".Triggers",
        [typeof(Syntax.Triggers.TriggerEvent)] = ProjectNsqlSyntax + ".Triggers",
        [typeof(Syntax.Triggers.TriggerLevel)] = ProjectNsqlSyntax + ".Triggers",
        [typeof(Syntax.Tables.GrantTableStatement)] = ProjectNsqlSyntax + ".Tables",
        [typeof(Syntax.Tables.TablePrivilege)] = ProjectNsqlSyntax + ".Tables",
        [typeof(Syntax.Scripts.ScriptStatement)] = ProjectNsqlSyntax + ".Scripts",
        [typeof(Syntax.Scripts.ScriptEventClause)] = ProjectNsqlSyntax + ".Scripts",
        [typeof(Syntax.Scripts.DeploymentEventClause)] = ProjectNsqlSyntax + ".Scripts",
        [typeof(Syntax.Scripts.ChangeEventClause)] = ProjectNsqlSyntax + ".Scripts",
        [typeof(Syntax.Scripts.RunCondition)] = ProjectNsqlSyntax + ".Scripts",
        [typeof(Syntax.Scripts.DeploymentPhase)] = ProjectNsqlSyntax + ".Scripts",
        [typeof(Syntax.Scripts.ChangeTrigger)] = ProjectNsqlSyntax + ".Scripts",
        [typeof(Syntax.Templates.TemplateStatement)] = ProjectNsqlSyntax + ".Templates",
        [typeof(Syntax.Templates.SchemaTemplateStatement)] = ProjectNsqlSyntax + ".Templates",
        [typeof(Syntax.Templates.TableTemplateStatement)] = ProjectNsqlSyntax + ".Templates",
        [typeof(Syntax.Templates.ApplyTemplateStatement)] = ProjectNsqlSyntax + ".Templates",
        [typeof(Syntax.Schemas.DropSchemaStatement)] = ProjectNsqlSyntax + ".Schemas",
        [typeof(Syntax.Tables.DropTableStatement)] = ProjectNsqlSyntax + ".Tables",
        [typeof(Syntax.Views.DropViewStatement)] = ProjectNsqlSyntax + ".Views",
        [typeof(Syntax.Views.RenameViewStatement)] = ProjectNsqlSyntax + ".Views",
        [typeof(Syntax.Enums.DropEnumStatement)] = ProjectNsqlSyntax + ".Enums",
        [typeof(Syntax.Enums.RenameEnumStatement)] = ProjectNsqlSyntax + ".Enums",
        [typeof(Syntax.Domains.DropDomainStatement)] = ProjectNsqlSyntax + ".Domains",
        [typeof(Syntax.Domains.RenameDomainStatement)] = ProjectNsqlSyntax + ".Domains",
        [typeof(Syntax.CompositeTypes.DropCompositeTypeStatement)] = ProjectNsqlSyntax + ".CompositeTypes",
        [typeof(Syntax.CompositeTypes.RenameCompositeTypeStatement)] = ProjectNsqlSyntax + ".CompositeTypes",
        [typeof(Syntax.Sequences.DropSequenceStatement)] = ProjectNsqlSyntax + ".Sequences",
        [typeof(Syntax.Sequences.RenameSequenceStatement)] = ProjectNsqlSyntax + ".Sequences",
        [typeof(Syntax.Routines.DropRoutineStatement)] = ProjectNsqlSyntax + ".Routines",
        [typeof(Syntax.Routines.RenameRoutineStatement)] = ProjectNsqlSyntax + ".Routines",
        [typeof(Syntax.Extensions.DropExtensionStatement)] = ProjectNsqlSyntax + ".Extensions",
        [typeof(Syntax.Config.ConfigStatement)] = ProjectNsqlSyntax + ".Config",
        [typeof(Syntax.Config.StateStatement)] = ProjectNsqlSyntax + ".Config",
        [typeof(Syntax.Config.DatabaseStatement)] = ProjectNsqlSyntax + ".Config",
        [typeof(Syntax.Config.ConfigAttribute)] = ProjectNsqlSyntax + ".Config",
        [typeof(Syntax.Config.ConfigValueNode)] = ProjectNsqlSyntax + ".Config",
        [typeof(Syntax.Config.StringValue)] = ProjectNsqlSyntax + ".Config",
        [typeof(Syntax.Config.IntegerValue)] = ProjectNsqlSyntax + ".Config",
        [typeof(Syntax.Config.BooleanValue)] = ProjectNsqlSyntax + ".Config",
        [typeof(Syntax.Config.IdentifierValue)] = ProjectNsqlSyntax + ".Config",

        // ── ProjectDefinition.Policies ──
        [typeof(NSchema.Project.Policies.IProjectPolicy)] = ProjectPolicies,

        // ── ProjectDefinition.Domain.Models: the shared pipeline vocabulary (the schema tree + the script models). ──
        [typeof(NSchema.Model.Database)] = Model,
        [typeof(NSchema.Model.INamedObject)] = Model,
        // Addresses and the directive vocabulary (cross-kind shapes at the root, slices per subject).
        [typeof(NSchema.Model.MemberReference)] = Model,
        [typeof(NSchema.Project.Domain.Models.ProjectDirectives)] = ProjectModels,
        [typeof(SchemaRenameDirective)] = ProjectModels, // the directives are one vocabulary, kept together rather than mirrored per-kind against the kernel
        [typeof(NSchema.Project.Domain.Models.ObjectRenameDirective)] = ProjectModels,
        [typeof(NSchema.Project.Domain.Models.MemberRenameDirective)] = ProjectModels,
        [typeof(SchemaDirectives)] = ProjectModels,
        [typeof(TableDirectives)] = ProjectModels,
        [typeof(ViewDirectives)] = ProjectModels,
        [typeof(EnumDirectives)] = ProjectModels,
        [typeof(SequenceDirectives)] = ProjectModels,
        [typeof(RoutineDirectives)] = ProjectModels,
        [typeof(DomainDirectives)] = ProjectModels,
        [typeof(CompositeTypeDirectives)] = ProjectModels,
        [typeof(ExtensionDirectives)] = ProjectModels,
        [typeof(NSchema.Model.Columns.Column)] = Model + ".Columns",
        [typeof(NSchema.Model.Columns.IdentityOptions)] = Model + ".Columns",
        [typeof(NSchema.Model.Columns.SqlType)] = Model + ".Columns",
        [typeof(NSchema.Model.CompositeTypes.CompositeField)] = Model + ".CompositeTypes",
        [typeof(NSchema.Model.CompositeTypes.CompositeType)] = Model + ".CompositeTypes",
        [typeof(NSchema.Model.Constraints.CheckConstraint)] = Model + ".Constraints",
        [typeof(NSchema.Model.Constraints.ExclusionConstraint)] = Model + ".Constraints",
        [typeof(NSchema.Model.Constraints.ExclusionElement)] = Model + ".Constraints",
        [typeof(NSchema.Model.Constraints.UniqueConstraint)] = Model + ".Constraints",
        [typeof(NSchema.Model.Domains.DomainType)] = Model + ".Domains",
        [typeof(NSchema.Model.Enums.EnumType)] = Model + ".Enums",
        [typeof(NSchema.Model.Extensions.Extension)] = Model + ".Extensions",
        [typeof(NSchema.Model.Indexes.IndexColumn)] = Model + ".Indexes",
        [typeof(NSchema.Model.Indexes.IndexNulls)] = Model + ".Indexes",
        [typeof(NSchema.Model.Indexes.IndexSort)] = Model + ".Indexes",
        [typeof(NSchema.Model.Indexes.TableIndex)] = Model + ".Indexes",
        [typeof(NSchema.Model.Routines.Routine)] = Model + ".Routines",
        [typeof(NSchema.Model.Routines.RoutineKind)] = Model + ".Routines",
        [typeof(NSchema.Model.Schemas.Schema)] = Model + ".Schemas",
        [typeof(NSchema.Model.Schemas.SchemaGrant)] = Model + ".Schemas",
        [typeof(NSchema.Model.Sequences.Sequence)] = Model + ".Sequences",
        [typeof(NSchema.Model.Sequences.SequenceOptions)] = Model + ".Sequences",
        [typeof(NSchema.Model.Tables.ForeignKey)] = Model + ".Tables",
        [typeof(NSchema.Model.Tables.PrimaryKey)] = Model + ".Tables",
        [typeof(NSchema.Model.Tables.ReferentialAction)] = Model + ".Tables",
        [typeof(NSchema.Model.Tables.Table)] = Model + ".Tables",
        [typeof(NSchema.Model.Tables.TableGrant)] = Model + ".Tables",
        [typeof(NSchema.Model.Tables.TablePrivilege)] = Model + ".Tables",
        [typeof(NSchema.Model.Triggers.Trigger)] = Model + ".Triggers",
        [typeof(NSchema.Model.Triggers.TriggerEvent)] = Model + ".Triggers",
        [typeof(NSchema.Model.Triggers.TriggerLevel)] = Model + ".Triggers",
        [typeof(NSchema.Model.Triggers.TriggerTiming)] = Model + ".Triggers",
        [typeof(NSchema.Model.Views.View)] = Model + ".Views",
        [typeof(NSchema.Model.Views.ViewDependency)] = Model + ".Views",
        // The script model: an abstract Script with two concrete kinds — ChangeScript and DeploymentScript.
        [typeof(NSchema.Model.Scripts.Script)] = Model + ".Scripts",
        [typeof(NSchema.Model.Scripts.ChangeScript)] = Model + ".Scripts",
        [typeof(NSchema.Model.Scripts.DeploymentScript)] = Model + ".Scripts",
        [typeof(NSchema.Model.Scripts.ScriptReference)] = Model + ".Scripts",
        [typeof(NSchema.Model.Scripts.RunCondition)] = Model + ".Scripts",
        [typeof(NSchema.Model.Scripts.DeploymentPhase)] = Model + ".Scripts",
        [typeof(NSchema.Model.Scripts.ChangeTrigger)] = Model + ".Scripts",

        // ── Diff: root = reader + presentation read model; tree in Domain.Models. ──
        [typeof(NSchema.Diff.Reader.DiffReader)] = DiffReaderNs,
        [typeof(NSchema.Diff.Reader.DiffDocument)] = DiffReaderNs,
        [typeof(NSchema.Diff.Reader.DiffLine)] = DiffReaderNs,
        [typeof(NSchema.Diff.Domain.Models.DiffSummary)] = DiffModels, // produced by DatabaseDiff.GetSummary — model vocabulary, not a seam message
        [typeof(NSchema.Plan.Policies.IPlanPolicy)] = PlanPolicies,
        [typeof(NSchema.Plan.Policies.DataHazardOptions)] = PlanPolicies,
        [typeof(NSchema.Plan.Policies.DestructiveActionOptions)] = PlanPolicies,
        [typeof(NSchema.Diff.Domain.Models.ChangeKind)] = DiffModels,
        [typeof(NSchema.Diff.Domain.Models.DatabaseDiff)] = DiffModels,
        [typeof(NSchema.Diff.Domain.Models.INamedObjectDiff)] = DiffModels,
        [typeof(NSchema.Diff.Domain.Models.ISchemaObjectDiff)] = DiffModels,
        [typeof(NSchema.Diff.Domain.Models.Schemas.SchemaDiff)] = DiffModels + ".Schemas",
        [typeof(NSchema.Diff.Domain.Models.Tables.TableDiff)] = DiffModels + ".Tables",
        [typeof(NSchema.Diff.Domain.Models.Columns.ColumnDiff)] = DiffModels + ".Columns",
        [typeof(NSchema.Diff.Domain.Models.Constraints.CheckConstraintDiff)] = DiffModels + ".Constraints",
        [typeof(NSchema.Diff.Domain.Models.Constraints.UniqueConstraintDiff)] = DiffModels + ".Constraints",
        [typeof(NSchema.Diff.Domain.Models.Constraints.ExclusionConstraintDiff)] = DiffModels + ".Constraints",
        [typeof(NSchema.Diff.Domain.Models.Constraints.ForeignKeyDiff)] = DiffModels + ".Constraints",
        [typeof(NSchema.Diff.Domain.Models.Constraints.PrimaryKeyDiff)] = DiffModels + ".Constraints",
        [typeof(NSchema.Diff.Domain.Models.Indexes.IndexDiff)] = DiffModels + ".Indexes",
        [typeof(NSchema.Diff.Domain.Models.Views.ViewDiff)] = DiffModels + ".Views",
        [typeof(NSchema.Diff.Domain.Models.Routines.RoutineDiff)] = DiffModels + ".Routines",
        [typeof(NSchema.Diff.Domain.Models.Sequences.SequenceDiff)] = DiffModels + ".Sequences",
        [typeof(NSchema.Diff.Domain.Models.Enums.EnumDiff)] = DiffModels + ".Enums",
        [typeof(NSchema.Diff.Domain.Models.Enums.EnumValueAddition)] = DiffModels + ".Enums",
        [typeof(NSchema.Diff.Domain.Models.Domains.DomainDiff)] = DiffModels + ".Domains",
        [typeof(NSchema.Diff.Domain.Models.CompositeTypes.CompositeTypeDiff)] = DiffModels + ".CompositeTypes",
        [typeof(NSchema.Diff.Domain.Models.CompositeTypes.CompositeFieldDiff)] = DiffModels + ".CompositeTypes",
        [typeof(NSchema.Diff.Domain.Models.Extensions.ExtensionDiff)] = DiffModels + ".Extensions",
        [typeof(NSchema.Diff.Domain.Models.Triggers.TriggerDiff)] = DiffModels + ".Triggers",
        [typeof(NSchema.Diff.Domain.Models.Tables.GrantChange)] = DiffModels + ".Tables",
        [typeof(NSchema.Diff.Domain.Models.ValueChange<>)] = DiffModels,

        // ── Plan: the single artifact at the root; dialect SPI in Backends; actions in Domain.Models. ──
        [typeof(NSchema.Plan.Domain.Models.MigrationPlan)] = PlanModels, // the boundary artifact is vocabulary — roots hold seams, and may be empty // the single plan artifact: diff + scripts + statements
        [typeof(NSchema.Plan.Backends.ISqlDialect)] = PlanBackends,
        [typeof(NSchema.Plan.Domain.Models.SqlStatement)] = PlanModels,
        [typeof(NSchema.Plan.Domain.Models.MigrationAction)] = PlanModels,
        [typeof(NSchema.Plan.Domain.Models.Columns.AddColumn)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Domain.Models.Columns.AlterColumnNullability)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Domain.Models.Columns.AlterColumnType)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Domain.Models.Columns.AlterIdentitySequence)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Domain.Models.Columns.DropColumn)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Domain.Models.Columns.RenameColumn)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Domain.Models.Columns.SetColumnComment)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Domain.Models.Columns.SetColumnDefault)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Domain.Models.Columns.SetColumnGenerated)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Domain.Models.CompositeTypes.AddCompositeField)] = PlanModels + ".CompositeTypes",
        [typeof(NSchema.Plan.Domain.Models.CompositeTypes.AlterCompositeFieldType)] = PlanModels + ".CompositeTypes",
        [typeof(NSchema.Plan.Domain.Models.CompositeTypes.CreateCompositeType)] = PlanModels + ".CompositeTypes",
        [typeof(NSchema.Plan.Domain.Models.CompositeTypes.DropCompositeField)] = PlanModels + ".CompositeTypes",
        [typeof(NSchema.Plan.Domain.Models.CompositeTypes.DropCompositeType)] = PlanModels + ".CompositeTypes",
        [typeof(NSchema.Plan.Domain.Models.CompositeTypes.RenameCompositeType)] = PlanModels + ".CompositeTypes",
        [typeof(NSchema.Plan.Domain.Models.CompositeTypes.SetCompositeTypeComment)] = PlanModels + ".CompositeTypes",
        [typeof(NSchema.Plan.Domain.Models.Constraints.AddCheckConstraint)] = PlanModels + ".Constraints",
        [typeof(NSchema.Plan.Domain.Models.Constraints.AddExclusionConstraint)] = PlanModels + ".Constraints",
        [typeof(NSchema.Plan.Domain.Models.Constraints.AddUniqueConstraint)] = PlanModels + ".Constraints",
        [typeof(NSchema.Plan.Domain.Models.Constraints.DropCheckConstraint)] = PlanModels + ".Constraints",
        [typeof(NSchema.Plan.Domain.Models.Constraints.DropExclusionConstraint)] = PlanModels + ".Constraints",
        [typeof(NSchema.Plan.Domain.Models.Constraints.DropUniqueConstraint)] = PlanModels + ".Constraints",
        [typeof(NSchema.Plan.Domain.Models.Constraints.SetConstraintComment)] = PlanModels + ".Constraints",
        [typeof(NSchema.Plan.Domain.Models.Domains.AddDomainCheck)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Domain.Models.Domains.AlterDomainDefault)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Domain.Models.Domains.AlterDomainNotNull)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Domain.Models.Domains.CreateDomain)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Domain.Models.Domains.DropDomain)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Domain.Models.Domains.DropDomainCheck)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Domain.Models.Domains.RecreateDomain)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Domain.Models.Domains.RenameDomain)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Domain.Models.Domains.SetDomainComment)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Domain.Models.Enums.AddEnumValue)] = PlanModels + ".Enums",
        [typeof(NSchema.Plan.Domain.Models.Enums.CreateEnum)] = PlanModels + ".Enums",
        [typeof(NSchema.Plan.Domain.Models.Enums.DropEnum)] = PlanModels + ".Enums",
        [typeof(NSchema.Plan.Domain.Models.Enums.RenameEnum)] = PlanModels + ".Enums",
        [typeof(NSchema.Plan.Domain.Models.Enums.SetEnumComment)] = PlanModels + ".Enums",
        [typeof(NSchema.Plan.Domain.Models.Extensions.AlterExtension)] = PlanModels + ".Extensions",
        [typeof(NSchema.Plan.Domain.Models.Extensions.CreateExtension)] = PlanModels + ".Extensions",
        [typeof(NSchema.Plan.Domain.Models.Extensions.DropExtension)] = PlanModels + ".Extensions",
        [typeof(NSchema.Plan.Domain.Models.Extensions.SetExtensionComment)] = PlanModels + ".Extensions",
        [typeof(NSchema.Plan.Domain.Models.Indexes.CreateIndex)] = PlanModels + ".Indexes",
        [typeof(NSchema.Plan.Domain.Models.Indexes.DropIndex)] = PlanModels + ".Indexes",
        [typeof(NSchema.Plan.Domain.Models.Indexes.SetIndexComment)] = PlanModels + ".Indexes",
        [typeof(NSchema.Plan.Domain.Models.Routines.CreateRoutine)] = PlanModels + ".Routines",
        [typeof(NSchema.Plan.Domain.Models.Routines.DropRoutine)] = PlanModels + ".Routines",
        [typeof(NSchema.Plan.Domain.Models.Routines.RecreateRoutine)] = PlanModels + ".Routines",
        [typeof(NSchema.Plan.Domain.Models.Routines.RenameRoutine)] = PlanModels + ".Routines",
        [typeof(NSchema.Plan.Domain.Models.Routines.SetRoutineComment)] = PlanModels + ".Routines",
        [typeof(NSchema.Plan.Domain.Models.Schemas.CreateSchema)] = PlanModels + ".Schemas",
        [typeof(NSchema.Plan.Domain.Models.Schemas.DropSchema)] = PlanModels + ".Schemas",
        [typeof(NSchema.Plan.Domain.Models.Schemas.GrantSchemaUsage)] = PlanModels + ".Schemas",
        [typeof(NSchema.Plan.Domain.Models.Schemas.RenameSchema)] = PlanModels + ".Schemas",
        [typeof(NSchema.Plan.Domain.Models.Schemas.RevokeSchemaUsage)] = PlanModels + ".Schemas",
        [typeof(NSchema.Plan.Domain.Models.Schemas.SetSchemaComment)] = PlanModels + ".Schemas",
        // Sequences: fixes the lone-singular folder while it moves.
        [typeof(NSchema.Plan.Domain.Models.Sequences.AlterSequence)] = PlanModels + ".Sequences",
        [typeof(NSchema.Plan.Domain.Models.Sequences.CreateSequence)] = PlanModels + ".Sequences",
        [typeof(NSchema.Plan.Domain.Models.Sequences.DropSequence)] = PlanModels + ".Sequences",
        [typeof(NSchema.Plan.Domain.Models.Sequences.RenameSequence)] = PlanModels + ".Sequences",
        [typeof(NSchema.Plan.Domain.Models.Sequences.SetSequenceComment)] = PlanModels + ".Sequences",
        [typeof(NSchema.Plan.Domain.Models.Tables.AddForeignKey)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Domain.Models.Tables.AddPrimaryKey)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Domain.Models.Tables.CreateTable)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Domain.Models.Tables.DropForeignKey)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Domain.Models.Tables.DropPrimaryKey)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Domain.Models.Tables.DropTable)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Domain.Models.Tables.GrantTablePrivileges)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Domain.Models.Tables.RenameTable)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Domain.Models.Tables.RevokeTablePrivileges)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Domain.Models.Tables.SetTableComment)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Domain.Models.Triggers.CreateTrigger)] = PlanModels + ".Triggers",
        [typeof(NSchema.Plan.Domain.Models.Triggers.DropTrigger)] = PlanModels + ".Triggers",
        [typeof(NSchema.Plan.Domain.Models.Triggers.SetTriggerComment)] = PlanModels + ".Triggers",
        [typeof(NSchema.Plan.Domain.Models.Scripts.ExecuteScript)] = PlanModels + ".Scripts",
        [typeof(NSchema.Plan.Domain.Models.Views.CreateView)] = PlanModels + ".Views",
        [typeof(NSchema.Plan.Domain.Models.Views.DropView)] = PlanModels + ".Views",
        [typeof(NSchema.Plan.Domain.Models.Views.RenameView)] = PlanModels + ".Views",
        [typeof(NSchema.Plan.Domain.Models.Views.SetViewComment)] = PlanModels + ".Views",
        [typeof(NSchema.Plan.PlanFile.IPlanFileManager)] = PlanFile,
        [typeof(NSchema.Plan.PlanFile.PlanFileEnvelope)] = PlanFile, // likely thins once the single artifact lands

        // ── Apply: plan execution. ──
        [typeof(NSchema.Apply.TransactionMode)] = Apply,

        // ── Current.Locks: guards the shared record against concurrent runs. ──
        [typeof(NSchema.State.Locks.IStateLockManager)] = StateLocks,
        [typeof(NSchema.State.Locks.IStateLockHandle)] = StateLocks,
        [typeof(NSchema.State.Locks.StateLockInfo)] = StateLocks,
        [typeof(NSchema.State.Locks.StateLockRequest)] = StateLocks,
        [typeof(NSchema.State.Locks.AcquireLockArguments)] = StateLocks,
        [typeof(NSchema.State.Locks.StateLockedException)] = StateLocks,
        [typeof(NSchema.State.Locks.StateLockMismatchException)] = StateLocks,
        [typeof(NSchema.State.Locks.Backends.IStateLock)] = StateLocksBackends,

        // ── Current.Storage: the source's persistence — the recorded snapshot + ledger. ──
        [typeof(NSchema.State.IDatabaseStateManager)] = StateRoot,
        [typeof(NSchema.State.StateReadArguments)] = StateRoot,
        [typeof(NSchema.State.StateReadResult)] = StateRoot,
        [typeof(NSchema.State.StateWriteArguments)] = StateRoot,
        [typeof(NSchema.State.StateWriteResult)] = StateRoot,
        [typeof(NSchema.State.StateRawReadArguments)] = StateRoot,
        [typeof(NSchema.State.StateRawReadResult)] = StateRoot,
        [typeof(NSchema.State.StateRawWriteArguments)] = StateRoot,
        [typeof(NSchema.State.StateRawWriteResult)] = StateRoot,
        [typeof(NSchema.State.Backends.IDatabaseStateStore)] = StateBackends,

        // ── Current.Domain.Models ──
        [typeof(NSchema.State.Domain.Models.DatabaseState)] = StateModels,
        [typeof(NSchema.State.Domain.Models.ScriptExecution)] = StateModels, // the ledger entry — DatabaseState is its aggregate root; the differ reads it as source vocabulary

        // ── Operations: one seam, one vocabulary — publics flatten to the root. ──
        [typeof(INSchemaOperations)] = Operations,
        [typeof(NSchema.Operations.PlanArguments)] = Operations,
        [typeof(NSchema.Operations.PlanResult)] = Operations,
        [typeof(NSchema.Operations.PlanTarget)] = Operations,
        [typeof(NSchema.Operations.ApplyArguments)] = Operations,
        [typeof(NSchema.Operations.ApplyResult)] = Operations,
        [typeof(NSchema.Operations.RefreshArguments)] = Operations,
        [typeof(NSchema.Operations.RefreshResult)] = Operations,
        [typeof(NSchema.Operations.ValidateArguments)] = Operations,
        [typeof(NSchema.Operations.ValidateResult)] = Operations,
        [typeof(NSchema.Operations.DriftArguments)] = Operations,
        [typeof(NSchema.Operations.DriftResult)] = Operations,
        [typeof(NSchema.Operations.ImportArguments)] = Operations,
        [typeof(NSchema.Operations.ImportResult)] = Operations,
        [typeof(NSchema.Operations.DoctorArguments)] = Operations,
        [typeof(NSchema.Operations.DoctorResult)] = Operations,
        [typeof(OperationProgress)] = OperationsProgress,
        [typeof(ProgressLevel)] = OperationsProgress,

        // ── Plugins: the host ↔ plugin-author contract. ──
        [typeof(NSchema.Plugins.INSchemaPlugin)] = Plugins,
        [typeof(NSchema.Plugins.INSchemaProviderPlugin)] = Plugins,
        [typeof(NSchema.Plugins.INSchemaBackendPlugin)] = Plugins,
        [typeof(NSchema.Plugins.ScaffoldContext)] = Plugins,
        // Config settings records: the plugin seam's message (the syntax-node side stays in Project.Nsql).
        [typeof(NSchema.Plugins.ConfigValue)] = Plugins, // the settings scalar — moved with the plugin payload at the config lane split
        [typeof(NSchema.Plugins.ConfigValueKind)] = Plugins,
        [typeof(NSchema.Plugins.PluginSettings)] = Plugins, // the statement's translated payload — replaced ConfigBlock on the plugin seam
    };

    [Fact]
    public void EveryPublicType_HasAMigrationTarget()
    {
        // Arrange — unspeakable names are compiler-generated carriers (e.g. extension blocks), not API surface.
        var publicTypes = ArchitectureTestSupport.CoreAssembly.GetExportedTypes()
            .Where(t => !t.Name.Contains('<'))
            .ToList();

        // Act
        var unmapped = publicTypes.Except(_map.Keys).Select(t => t.FullName).ToList();

        // Assert
        publicTypes.ShouldNotBeEmpty();
        unmapped.ShouldBeEmpty($"Public types without a decided v5 home (add them to the migration map): {string.Join(", ", unmapped)}");
    }

    [Fact]
    public void Map_ContainsOnlyCurrentPublicTypes()
    {
        // Act — an entry goes stale when the type stops being public in Core (e.g. removed in the shrink phase).
        var stale = _map.Keys
            .Where(t => !t.IsVisible || t.Assembly != ArchitectureTestSupport.CoreAssembly)
            .Select(t => t.FullName)
            .ToList();

        // Assert
        stale.ShouldBeEmpty($"Stale migration-map entries (the type is no longer public in Core): {string.Join(", ", stale)}");
    }

    [Fact]
    public void EveryMappedType_LivesInItsTargetNamespace()
    {
        // Act
        var misplaced = _map
            .Where(e => e.Value != Removed && e.Key.Namespace != e.Value)
            .Select(e => $"{e.Key.FullName} → {e.Value}")
            .ToList();

        // Assert
        misplaced.ShouldBeEmpty($"Types not yet at their v5 home: {string.Join(", ", misplaced)}");
    }
}
