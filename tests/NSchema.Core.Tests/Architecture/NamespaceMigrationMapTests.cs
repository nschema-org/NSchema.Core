using NSchema.Diff.Model;
using NSchema.Diff.Model.Columns;
using NSchema.Diff.Model.CompositeTypes;
using NSchema.Diff.Model.Constraints;
using NSchema.Diff.Model.Domains;
using NSchema.Diff.Model.Enums;
using NSchema.Diff.Model.Extensions;
using NSchema.Diff.Model.Indexes;
using NSchema.Diff.Model.Routines;
using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Sequences;
using NSchema.Diff.Model.Tables;
using NSchema.Diff.Model.Triggers;
using NSchema.Diff.Model.Views;
using NSchema.Model.Routines;
using NSchema.Operations;
using NSchema.Operations.Progress;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Columns;
using NSchema.Plan.Model.CompositeTypes;
using NSchema.Plan.Model.Constraints;
using NSchema.Plan.Model.Domains;
using NSchema.Plan.Model.Enums;
using NSchema.Plan.Model.Extensions;
using NSchema.Plan.Model.Indexes;
using NSchema.Plan.Model.Routines;
using NSchema.Plan.Model.Schemas;
using NSchema.Plan.Model.Scripts;
using NSchema.Plan.Model.Sequences;
using NSchema.Plan.Model.Tables;
using NSchema.Plan.Model.Triggers;
using NSchema.Plan.Model.Views;
using NSchema.Project.Model.Directives;
using NSchema.State.Model;
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
        [typeof(Result)] = Root,
        [typeof(Result<>)] = Root,
        [typeof(Result<,>)] = Root, // the diagnostic-typed result — folds upward as Result<T> without translation
        [typeof(Diagnostic)] = Root,
        [typeof(DiagnosticSeverity)] = Root,
        [typeof(PolicyEnforcement)] = Root, // absorbed DestructiveActionPolicy

        // ── ProjectDefinition: the declared desired state — seam and messages at the cluster root. ──
        [typeof(NSchema.Project.IProjectProvider)] = ProjectRoot,
        [typeof(ProjectDefinition)] = ProjectModels, // the project aggregate — raw domain vocabulary the provider returns (the DatabaseState parallel), not a seam-shaped message
        [typeof(Model.PlanningScope)] = Model,
        [typeof(Model.ValueObject<>)] = Model, // the single-value primitive base — value equality, renders as its value
        [typeof(Model.SqlIdentifier)] = Model, // the identifier vocabulary primitive — case-insensitive equality baked in, shared by every lane
        [typeof(Model.SqlText)] = Model, // the opaque-SQL vocabulary primitive — verbatim foreign SQL, ordinal equality (data)
        [typeof(Model.Address)] = Model, // the address base — shaped by containment, carrying neither kind nor name-space (both engine-specific)
        [typeof(Model.ObjectReference)] = Model, // the address of a schema-level object — always fully qualified
        [typeof(RoutineReference)] = Model + ".Routines", // a routine reference as written — unqualified resolves via the engine's search path; homed with the routines it names, now the dependency graph resolves them too

        // ── Current: the source the project is diffed against — observed live, or recorded. ──
        [typeof(Deployment.IDatabaseProvider)] = DeploymentRoot,
        [typeof(Deployment.Backends.IDatabaseIntrospector)] = DeploymentBackends,

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
        [typeof(Model.Database)] = Model,
        [typeof(Model.INamedObject)] = Model,
        // Addresses and the directive vocabulary (cross-kind shapes at the root, slices per subject).
        [typeof(Model.MemberReference)] = Model,
        [typeof(ProjectDirectives)] = ProjectModels,
        [typeof(SchemaRenameDirective)] = ProjectModels, // the directives are one vocabulary, kept together rather than mirrored per-kind against the kernel
        [typeof(ObjectRenameDirective)] = ProjectModels,
        [typeof(MemberRenameDirective)] = ProjectModels,
        [typeof(SchemaDirectives)] = ProjectModels,
        [typeof(TableDirectives)] = ProjectModels,
        [typeof(ViewDirectives)] = ProjectModels,
        [typeof(EnumDirectives)] = ProjectModels,
        [typeof(SequenceDirectives)] = ProjectModels,
        [typeof(RoutineDirectives)] = ProjectModels,
        [typeof(DomainDirectives)] = ProjectModels,
        [typeof(CompositeTypeDirectives)] = ProjectModels,
        [typeof(ExtensionDirectives)] = ProjectModels,
        [typeof(Model.Columns.Column)] = Model + ".Columns",
        [typeof(Model.Columns.IdentityOptions)] = Model + ".Columns",
        [typeof(Model.Columns.SqlType)] = Model + ".Columns",
        [typeof(Model.CompositeTypes.CompositeField)] = Model + ".CompositeTypes",
        [typeof(Model.CompositeTypes.CompositeType)] = Model + ".CompositeTypes",
        [typeof(Model.Constraints.CheckConstraint)] = Model + ".Constraints",
        [typeof(Model.Constraints.ExclusionConstraint)] = Model + ".Constraints",
        [typeof(Model.Constraints.ExclusionElement)] = Model + ".Constraints",
        [typeof(Model.Constraints.UniqueConstraint)] = Model + ".Constraints",
        [typeof(Model.Domains.DomainType)] = Model + ".Domains",
        [typeof(Model.Enums.EnumType)] = Model + ".Enums",
        [typeof(Model.Extensions.Extension)] = Model + ".Extensions",
        [typeof(Model.Indexes.IndexColumn)] = Model + ".Indexes",
        [typeof(Model.Indexes.IndexNulls)] = Model + ".Indexes",
        [typeof(Model.Indexes.IndexSort)] = Model + ".Indexes",
        [typeof(Model.Indexes.TableIndex)] = Model + ".Indexes",
        [typeof(Routine)] = Model + ".Routines",
        [typeof(RoutineKind)] = Model + ".Routines",
        [typeof(Model.Schemas.Schema)] = Model + ".Schemas",
        [typeof(Model.Schemas.SchemaGrant)] = Model + ".Schemas",
        [typeof(Model.Sequences.Sequence)] = Model + ".Sequences",
        [typeof(Model.Sequences.SequenceOptions)] = Model + ".Sequences",
        [typeof(Model.Tables.ForeignKey)] = Model + ".Tables",
        [typeof(Model.Tables.PrimaryKey)] = Model + ".Tables",
        [typeof(Model.Tables.ReferentialAction)] = Model + ".Tables",
        [typeof(Model.Tables.Table)] = Model + ".Tables",
        [typeof(Model.Tables.TableGrant)] = Model + ".Tables",
        [typeof(Model.Tables.TablePrivilege)] = Model + ".Tables",
        [typeof(Model.Triggers.Trigger)] = Model + ".Triggers",
        [typeof(Model.Triggers.TriggerEvent)] = Model + ".Triggers",
        [typeof(Model.Triggers.TriggerLevel)] = Model + ".Triggers",
        [typeof(Model.Triggers.TriggerTiming)] = Model + ".Triggers",
        [typeof(Model.Views.View)] = Model + ".Views",
        [typeof(Model.Views.ViewDependency)] = Model + ".Views",
        // The script model: an abstract Script with two concrete kinds — ChangeScript and DeploymentScript.
        [typeof(Model.Scripts.Script)] = Model + ".Scripts",
        [typeof(Model.Scripts.ChangeScript)] = Model + ".Scripts",
        [typeof(Model.Scripts.DeploymentScript)] = Model + ".Scripts",
        [typeof(Model.Scripts.ScriptReference)] = Model + ".Scripts",
        [typeof(Model.Scripts.RunCondition)] = Model + ".Scripts",
        [typeof(Model.Scripts.DeploymentPhase)] = Model + ".Scripts",
        [typeof(Model.Scripts.ChangeTrigger)] = Model + ".Scripts",

        // ── Diff: root = reader + presentation read model; tree in Domain.Models. ──
        [typeof(NSchema.Diff.Reader.DiffReader)] = DiffReaderNs,
        [typeof(NSchema.Diff.Reader.DiffDocument)] = DiffReaderNs,
        [typeof(NSchema.Diff.Reader.DiffLine)] = DiffReaderNs,
        [typeof(DiffSummary)] = DiffModels, // produced by DatabaseDiff.GetSummary — model vocabulary, not a seam message
        [typeof(NSchema.Plan.Policies.IPlanPolicy)] = PlanPolicies,
        [typeof(NSchema.Plan.Policies.DataHazardOptions)] = PlanPolicies,
        [typeof(NSchema.Plan.Policies.DestructiveActionOptions)] = PlanPolicies,
        [typeof(ChangeKind)] = DiffModels,
        [typeof(DatabaseDiff)] = DiffModels,
        [typeof(INamedObjectDiff)] = DiffModels,
        [typeof(ISchemaObjectDiff)] = DiffModels,
        [typeof(SchemaDiff)] = DiffModels + ".Schemas",
        [typeof(TableDiff)] = DiffModels + ".Tables",
        [typeof(ColumnDiff)] = DiffModels + ".Columns",
        [typeof(CheckConstraintDiff)] = DiffModels + ".Constraints",
        [typeof(UniqueConstraintDiff)] = DiffModels + ".Constraints",
        [typeof(ExclusionConstraintDiff)] = DiffModels + ".Constraints",
        [typeof(ForeignKeyDiff)] = DiffModels + ".Constraints",
        [typeof(PrimaryKeyDiff)] = DiffModels + ".Constraints",
        [typeof(IndexDiff)] = DiffModels + ".Indexes",
        [typeof(ViewDiff)] = DiffModels + ".Views",
        [typeof(RoutineDiff)] = DiffModels + ".Routines",
        [typeof(SequenceDiff)] = DiffModels + ".Sequences",
        [typeof(EnumDiff)] = DiffModels + ".Enums",
        [typeof(EnumValueAddition)] = DiffModels + ".Enums",
        [typeof(DomainDiff)] = DiffModels + ".Domains",
        [typeof(CompositeTypeDiff)] = DiffModels + ".CompositeTypes",
        [typeof(CompositeFieldDiff)] = DiffModels + ".CompositeTypes",
        [typeof(ExtensionDiff)] = DiffModels + ".Extensions",
        [typeof(TriggerDiff)] = DiffModels + ".Triggers",
        [typeof(GrantChange)] = DiffModels + ".Tables",
        [typeof(ValueChange<>)] = DiffModels,

        // ── Plan: the single artifact at the root; dialect SPI in Backends; actions in Domain.Models. ──
        [typeof(MigrationPlan)] = PlanModels, // the boundary artifact is vocabulary — roots hold seams, and may be empty // the single plan artifact: diff + scripts + statements
        [typeof(NSchema.Plan.Backends.ISqlDialect)] = PlanBackends,
        [typeof(SqlStatement)] = PlanModels,
        [typeof(MigrationAction)] = PlanModels,
        [typeof(AddColumn)] = PlanModels + ".Columns",
        [typeof(AlterColumnNullability)] = PlanModels + ".Columns",
        [typeof(AlterColumnType)] = PlanModels + ".Columns",
        [typeof(AlterIdentitySequence)] = PlanModels + ".Columns",
        [typeof(DropColumn)] = PlanModels + ".Columns",
        [typeof(RenameColumn)] = PlanModels + ".Columns",
        [typeof(SetColumnComment)] = PlanModels + ".Columns",
        [typeof(SetColumnDefault)] = PlanModels + ".Columns",
        [typeof(SetColumnGenerated)] = PlanModels + ".Columns",
        [typeof(AddCompositeField)] = PlanModels + ".CompositeTypes",
        [typeof(AlterCompositeFieldType)] = PlanModels + ".CompositeTypes",
        [typeof(CreateCompositeType)] = PlanModels + ".CompositeTypes",
        [typeof(DropCompositeField)] = PlanModels + ".CompositeTypes",
        [typeof(DropCompositeType)] = PlanModels + ".CompositeTypes",
        [typeof(RenameCompositeType)] = PlanModels + ".CompositeTypes",
        [typeof(SetCompositeTypeComment)] = PlanModels + ".CompositeTypes",
        [typeof(AddCheckConstraint)] = PlanModels + ".Constraints",
        [typeof(AddExclusionConstraint)] = PlanModels + ".Constraints",
        [typeof(AddUniqueConstraint)] = PlanModels + ".Constraints",
        [typeof(DropCheckConstraint)] = PlanModels + ".Constraints",
        [typeof(DropExclusionConstraint)] = PlanModels + ".Constraints",
        [typeof(DropUniqueConstraint)] = PlanModels + ".Constraints",
        [typeof(SetConstraintComment)] = PlanModels + ".Constraints",
        [typeof(AddDomainCheck)] = PlanModels + ".Domains",
        [typeof(AlterDomainDefault)] = PlanModels + ".Domains",
        [typeof(AlterDomainNotNull)] = PlanModels + ".Domains",
        [typeof(CreateDomain)] = PlanModels + ".Domains",
        [typeof(DropDomain)] = PlanModels + ".Domains",
        [typeof(DropDomainCheck)] = PlanModels + ".Domains",
        [typeof(RecreateDomain)] = PlanModels + ".Domains",
        [typeof(RenameDomain)] = PlanModels + ".Domains",
        [typeof(SetDomainComment)] = PlanModels + ".Domains",
        [typeof(AddEnumValue)] = PlanModels + ".Enums",
        [typeof(CreateEnum)] = PlanModels + ".Enums",
        [typeof(DropEnum)] = PlanModels + ".Enums",
        [typeof(RenameEnum)] = PlanModels + ".Enums",
        [typeof(SetEnumComment)] = PlanModels + ".Enums",
        [typeof(AlterExtension)] = PlanModels + ".Extensions",
        [typeof(CreateExtension)] = PlanModels + ".Extensions",
        [typeof(DropExtension)] = PlanModels + ".Extensions",
        [typeof(SetExtensionComment)] = PlanModels + ".Extensions",
        [typeof(CreateIndex)] = PlanModels + ".Indexes",
        [typeof(DropIndex)] = PlanModels + ".Indexes",
        [typeof(SetIndexComment)] = PlanModels + ".Indexes",
        [typeof(CreateRoutine)] = PlanModels + ".Routines",
        [typeof(DropRoutine)] = PlanModels + ".Routines",
        [typeof(RecreateRoutine)] = PlanModels + ".Routines",
        [typeof(RenameRoutine)] = PlanModels + ".Routines",
        [typeof(SetRoutineComment)] = PlanModels + ".Routines",
        [typeof(CreateSchema)] = PlanModels + ".Schemas",
        [typeof(DropSchema)] = PlanModels + ".Schemas",
        [typeof(GrantSchemaUsage)] = PlanModels + ".Schemas",
        [typeof(RenameSchema)] = PlanModels + ".Schemas",
        [typeof(RevokeSchemaUsage)] = PlanModels + ".Schemas",
        [typeof(SetSchemaComment)] = PlanModels + ".Schemas",
        // Sequences: fixes the lone-singular folder while it moves.
        [typeof(AlterSequence)] = PlanModels + ".Sequences",
        [typeof(CreateSequence)] = PlanModels + ".Sequences",
        [typeof(DropSequence)] = PlanModels + ".Sequences",
        [typeof(RenameSequence)] = PlanModels + ".Sequences",
        [typeof(SetSequenceComment)] = PlanModels + ".Sequences",
        [typeof(AddForeignKey)] = PlanModels + ".Tables",
        [typeof(AddPrimaryKey)] = PlanModels + ".Tables",
        [typeof(CreateTable)] = PlanModels + ".Tables",
        [typeof(DropForeignKey)] = PlanModels + ".Tables",
        [typeof(DropPrimaryKey)] = PlanModels + ".Tables",
        [typeof(DropTable)] = PlanModels + ".Tables",
        [typeof(GrantTablePrivileges)] = PlanModels + ".Tables",
        [typeof(RenameTable)] = PlanModels + ".Tables",
        [typeof(RevokeTablePrivileges)] = PlanModels + ".Tables",
        [typeof(SetTableComment)] = PlanModels + ".Tables",
        [typeof(CreateTrigger)] = PlanModels + ".Triggers",
        [typeof(DropTrigger)] = PlanModels + ".Triggers",
        [typeof(SetTriggerComment)] = PlanModels + ".Triggers",
        [typeof(ExecuteScript)] = PlanModels + ".Scripts",
        [typeof(CreateView)] = PlanModels + ".Views",
        [typeof(DropView)] = PlanModels + ".Views",
        [typeof(RenameView)] = PlanModels + ".Views",
        [typeof(SetViewComment)] = PlanModels + ".Views",
        [typeof(NSchema.Plan.PlanFile.IPlanFileManager)] = PlanFile,
        [typeof(NSchema.Plan.PlanFile.PlanFileEnvelope)] = PlanFile, // likely thins once the single artifact lands

        // ── Apply: plan execution. ──
        [typeof(Apply.TransactionMode)] = Apply,

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
        [typeof(DatabaseState)] = StateModels,
        [typeof(ScriptExecution)] = StateModels, // the ledger entry — DatabaseState is its aggregate root; the differ reads it as source vocabulary

        // ── Operations: one seam, one vocabulary — publics flatten to the root. ──
        [typeof(INSchemaOperations)] = Operations,
        [typeof(PlanArguments)] = Operations,
        [typeof(PlanResult)] = Operations,
        [typeof(PlanTarget)] = Operations,
        [typeof(ApplyArguments)] = Operations,
        [typeof(ApplyResult)] = Operations,
        [typeof(RefreshArguments)] = Operations,
        [typeof(RefreshResult)] = Operations,
        [typeof(ValidateArguments)] = Operations,
        [typeof(ValidateResult)] = Operations,
        [typeof(DriftArguments)] = Operations,
        [typeof(DriftResult)] = Operations,
        [typeof(ImportArguments)] = Operations,
        [typeof(ImportResult)] = Operations,
        [typeof(DoctorArguments)] = Operations,
        [typeof(DoctorResult)] = Operations,
        [typeof(OperationProgress)] = OperationsProgress,
        [typeof(ProgressLevel)] = OperationsProgress,

        // ── Plugins: the host ↔ plugin-author contract. ──
        [typeof(Plugins.INSchemaPlugin)] = Plugins,
        [typeof(Plugins.INSchemaProviderPlugin)] = Plugins,
        [typeof(Plugins.INSchemaBackendPlugin)] = Plugins,
        [typeof(Plugins.ScaffoldContext)] = Plugins,
        // Config settings records: the plugin seam's message (the syntax-node side stays in Project.Nsql).
        [typeof(Plugins.ConfigValue)] = Plugins, // the settings scalar — moved with the plugin payload at the config lane split
        [typeof(Plugins.ConfigValueKind)] = Plugins,
        [typeof(Plugins.PluginSettings)] = Plugins, // the statement's translated payload — replaced ConfigBlock on the plugin seam
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
