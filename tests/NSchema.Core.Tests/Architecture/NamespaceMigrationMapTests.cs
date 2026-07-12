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
    private const string ProjectModels = "NSchema.Project.Domain.Models";
    private const string ProjectDdl = "NSchema.Project.Ddl";
    private const string ProjectPolicies = "NSchema.Project.Policies";
    private const string CurrentRoot = "NSchema.Current";
    private const string CurrentBackends = "NSchema.Current.Backends";
    private const string Diff = "NSchema.Diff";
    private const string DiffModels = "NSchema.Diff.Domain.Models";
    private const string DiffPolicies = "NSchema.Diff.Policies";
    private const string Plan = "NSchema.Plan";
    private const string PlanBackends = "NSchema.Plan.Backends";
    private const string PlanModels = "NSchema.Plan.Domain.Models";
    private const string PlanFile = "NSchema.Plan.PlanFile";
    private const string Apply = "NSchema.Apply";
    private const string CurrentLocks = "NSchema.Current.Locks";
    private const string CurrentLocksBackends = "NSchema.Current.Locks.Backends";
    private const string CurrentStorage = "NSchema.Current.Storage";
    private const string CurrentStorageBackends = "NSchema.Current.Storage.Backends";
    private const string CurrentModels = "NSchema.Current.Domain.Models";
    private const string Operations = "NSchema.Operations";
    private const string Plugins = "NSchema.Plugins";

    private static readonly IReadOnlyDictionary<Type, string> _map = new Dictionary<Type, string>
    {
        // ── Root grammar: composition entry points + outcome vocabulary (the closed list). ──
        [typeof(NSchemaApplication)] = Root,
        [typeof(NSchemaApplicationBuilder)] = Root,
        [typeof(NSchemaApplicationOptions)] = Root,
        [typeof(NSchema.Diagnostics.Result)] = Root,
        [typeof(NSchema.Diagnostics.Result<>)] = Root,
        [typeof(NSchema.Diagnostics.Success)] = Root,
        [typeof(NSchema.Diagnostics.Diagnostic)] = Root,
        [typeof(NSchema.Diagnostics.DiagnosticSeverity)] = Root,
        [typeof(NSchema.Policies.PolicyEnforcement)] = Root, // absorbs DestructiveActionPolicy
        [typeof(NSchema.Policies.PolicyDiagnostics)] = Removed, // redundant once severity is first-class on Result

        // ── Project: the declared desired state — seam and messages at the cluster root. ──
        [typeof(NSchema.Schema.IProjectProvider)] = ProjectRoot,
        [typeof(NSchema.Schema.Model.Project)] = ProjectRoot, // seam message, currently misfiled in Model

        // ── Current: the source the project is diffed against — observed live, or recorded. ──
        [typeof(NSchema.Schema.ICurrentSchemaProvider)] = CurrentRoot,
        [typeof(NSchema.Schema.SchemaSourceMode)] = CurrentRoot,
        [typeof(NSchema.Schema.ISchemaProvider)] = CurrentBackends, // renamed ISchemaIntrospector

        // ── Project.Ddl: the project language — machinery + the full syntax tree. ──
        [typeof(NSchema.Schema.Ddl.DdlReader)] = ProjectDdl,
        [typeof(NSchema.Schema.Ddl.DdlWriter)] = ProjectDdl,
        [typeof(NSchema.Schema.Ddl.DdlFormatter)] = ProjectDdl,
        [typeof(NSchema.Schema.Ddl.DdlSyntaxException)] = ProjectDdl, // likely absorbed by DdlDiagnostic in the Result<T,TDiagnostic> conversion
        [typeof(NSchema.Schema.Ddl.Model.DdlDocument)] = ProjectDdl, // becomes the parsed-project root of the full AST
        [typeof(NSchema.Schema.Ddl.Model.SourcePosition)] = ProjectDdl,
        // Template constructs are language features, not domain models; reshaped as AST nodes.
        [typeof(NSchema.Schema.Model.Templates.TemplateDefinition)] = ProjectDdl,
        [typeof(NSchema.Schema.Model.Templates.TemplateApplication)] = ProjectDdl,
        [typeof(NSchema.Schema.Model.Templates.TemplateInclude)] = ProjectDdl,
        [typeof(NSchema.Schema.Model.Templates.TemplateKind)] = ProjectDdl,
        [typeof(NSchema.Schema.Model.Templates.TemplateSet)] = ProjectDdl,

        // ── Project.Policies ──
        [typeof(NSchema.Schema.ISchemaPolicy)] = ProjectPolicies,
        [typeof(NSchema.Schema.Policies.SchemaLintPolicy)] = ProjectPolicies,
        [typeof(NSchema.Schema.Policies.StructuralIntegritySchemaPolicy)] = ProjectPolicies,

        // ── Project.Domain.Models: the shared pipeline vocabulary (the schema tree + the script models). ──
        [typeof(NSchema.Schema.Model.DatabaseSchema)] = ProjectModels,
        [typeof(NSchema.Schema.Model.INamedObject)] = ProjectModels,
        [typeof(NSchema.Schema.Model.IRenameableObject)] = ProjectModels,
        [typeof(NSchema.Schema.Model.Columns.Column)] = ProjectModels + ".Columns",
        [typeof(NSchema.Schema.Model.Columns.IdentityOptions)] = ProjectModels + ".Columns",
        [typeof(NSchema.Schema.Model.Columns.SqlType)] = ProjectModels + ".Columns",
        [typeof(NSchema.Schema.Model.CompositeTypes.CompositeField)] = ProjectModels + ".CompositeTypes",
        [typeof(NSchema.Schema.Model.CompositeTypes.CompositeType)] = ProjectModels + ".CompositeTypes",
        [typeof(NSchema.Schema.Model.Constraints.CheckConstraint)] = ProjectModels + ".Constraints",
        [typeof(NSchema.Schema.Model.Constraints.ExclusionConstraint)] = ProjectModels + ".Constraints",
        [typeof(NSchema.Schema.Model.Constraints.ExclusionElement)] = ProjectModels + ".Constraints",
        [typeof(NSchema.Schema.Model.Constraints.UniqueConstraint)] = ProjectModels + ".Constraints",
        [typeof(NSchema.Schema.Model.Domains.Domain)] = ProjectModels + ".Domains",
        [typeof(NSchema.Schema.Model.Enums.EnumType)] = ProjectModels + ".Enums",
        [typeof(NSchema.Schema.Model.Extensions.Extension)] = ProjectModels + ".Extensions",
        [typeof(NSchema.Schema.Model.Indexes.IndexColumn)] = ProjectModels + ".Indexes",
        [typeof(NSchema.Schema.Model.Indexes.IndexNulls)] = ProjectModels + ".Indexes",
        [typeof(NSchema.Schema.Model.Indexes.IndexSort)] = ProjectModels + ".Indexes",
        [typeof(NSchema.Schema.Model.Indexes.TableIndex)] = ProjectModels + ".Indexes",
        [typeof(NSchema.Schema.Model.Routines.Routine)] = ProjectModels + ".Routines",
        [typeof(NSchema.Schema.Model.Routines.RoutineKind)] = ProjectModels + ".Routines",
        [typeof(NSchema.Schema.Model.Schemas.SchemaDefinition)] = ProjectModels + ".Schemas",
        [typeof(NSchema.Schema.Model.Schemas.SchemaGrant)] = ProjectModels + ".Schemas",
        [typeof(NSchema.Schema.Model.Sequences.Sequence)] = ProjectModels + ".Sequences",
        [typeof(NSchema.Schema.Model.Sequences.SequenceOptions)] = ProjectModels + ".Sequences",
        [typeof(NSchema.Schema.Model.Tables.ForeignKey)] = ProjectModels + ".Tables",
        [typeof(NSchema.Schema.Model.Tables.PrimaryKey)] = ProjectModels + ".Tables",
        [typeof(NSchema.Schema.Model.Tables.ReferentialAction)] = ProjectModels + ".Tables",
        [typeof(NSchema.Schema.Model.Tables.Table)] = ProjectModels + ".Tables",
        [typeof(NSchema.Schema.Model.Tables.TableGrant)] = ProjectModels + ".Tables",
        [typeof(NSchema.Schema.Model.Tables.TablePrivilege)] = ProjectModels + ".Tables",
        [typeof(NSchema.Schema.Model.Triggers.Trigger)] = ProjectModels + ".Triggers",
        [typeof(NSchema.Schema.Model.Triggers.TriggerEvent)] = ProjectModels + ".Triggers",
        [typeof(NSchema.Schema.Model.Triggers.TriggerLevel)] = ProjectModels + ".Triggers",
        [typeof(NSchema.Schema.Model.Triggers.TriggerTiming)] = ProjectModels + ".Triggers",
        [typeof(NSchema.Schema.Model.Views.View)] = ProjectModels + ".Views",
        [typeof(NSchema.Schema.Model.Views.ViewDependency)] = ProjectModels + ".Views",
        // The unified script model: one Script, discriminated by the event it runs on.
        [typeof(NSchema.Schema.Model.Scripts.Script)] = ProjectModels + ".Scripts",
        [typeof(NSchema.Schema.Model.Scripts.RunCondition)] = ProjectModels + ".Scripts",
        [typeof(NSchema.Schema.Model.Scripts.ScriptEvent)] = ProjectModels + ".Scripts",
        [typeof(NSchema.Schema.Model.Scripts.DeploymentEvent)] = ProjectModels + ".Scripts",
        [typeof(NSchema.Schema.Model.Scripts.DeploymentPhase)] = ProjectModels + ".Scripts",
        [typeof(NSchema.Schema.Model.Scripts.ChangeEvent)] = ProjectModels + ".Scripts",
        [typeof(NSchema.Schema.Model.Scripts.ChangeTrigger)] = ProjectModels + ".Scripts",

        // ── Diff: root = reader + presentation read model; tree in Domain.Models. ──
        [typeof(NSchema.Diff.DiffReader)] = Diff,
        [typeof(NSchema.Diff.Model.DiffDocument)] = Diff,
        [typeof(NSchema.Diff.Model.DiffLine)] = Diff,
        [typeof(NSchema.Diff.Model.DiffSummary)] = Diff,
        [typeof(NSchema.Diff.IDiffPolicy)] = DiffPolicies,
        [typeof(NSchema.Diff.Policies.DataHazardOptions)] = DiffPolicies,
        [typeof(NSchema.Diff.Policies.DestructiveActionOptions)] = DiffPolicies,
        [typeof(NSchema.Diff.Policies.DestructiveActionPolicy)] = Removed, // folds into PolicyEnforcement
        [typeof(NSchema.Diff.Model.ChangeKind)] = DiffModels,
        [typeof(NSchema.Diff.Model.DatabaseDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.INamedObjectDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.ISchemaObjectDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.SchemaDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.TableDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.ColumnDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.CheckConstraintDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.UniqueConstraintDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.ExclusionConstraintDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.ForeignKeyDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.PrimaryKeyDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.IndexDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.ViewDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.RoutineDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.SequenceDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.EnumDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.EnumValueAddition)] = DiffModels,
        [typeof(NSchema.Diff.Model.DomainDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.CompositeTypeDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.CompositeFieldDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.ExtensionDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.TriggerDiff)] = DiffModels,
        [typeof(NSchema.Diff.Model.GrantChange)] = DiffModels,
        [typeof(NSchema.Diff.Model.ValueChange<>)] = DiffModels,

        // ── Plan: the single artifact at the root; dialect SPI in Backends; actions in Domain.Models. ──
        [typeof(NSchema.Plan.Model.MigrationPlan)] = Plan, // the single plan artifact: diff + scripts + statements
        [typeof(NSchema.Sql.ISqlDialect)] = PlanBackends,
        [typeof(NSchema.Sql.Model.SqlStatement)] = PlanModels,
        [typeof(NSchema.Plan.Model.MigrationAction)] = PlanModels,
        [typeof(NSchema.Plan.Model.Columns.AddColumn)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Model.Columns.AlterColumnNullability)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Model.Columns.AlterColumnType)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Model.Columns.AlterIdentitySequence)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Model.Columns.DropColumn)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Model.Columns.RenameColumn)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Model.Columns.SetColumnComment)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Model.Columns.SetColumnDefault)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Model.Columns.SetColumnGenerated)] = PlanModels + ".Columns",
        [typeof(NSchema.Plan.Model.CompositeTypes.AddCompositeField)] = PlanModels + ".CompositeTypes",
        [typeof(NSchema.Plan.Model.CompositeTypes.AlterCompositeFieldType)] = PlanModels + ".CompositeTypes",
        [typeof(NSchema.Plan.Model.CompositeTypes.CreateCompositeType)] = PlanModels + ".CompositeTypes",
        [typeof(NSchema.Plan.Model.CompositeTypes.DropCompositeField)] = PlanModels + ".CompositeTypes",
        [typeof(NSchema.Plan.Model.CompositeTypes.DropCompositeType)] = PlanModels + ".CompositeTypes",
        [typeof(NSchema.Plan.Model.CompositeTypes.RenameCompositeType)] = PlanModels + ".CompositeTypes",
        [typeof(NSchema.Plan.Model.CompositeTypes.SetCompositeTypeComment)] = PlanModels + ".CompositeTypes",
        [typeof(NSchema.Plan.Model.Constraints.AddCheckConstraint)] = PlanModels + ".Constraints",
        [typeof(NSchema.Plan.Model.Constraints.AddExclusionConstraint)] = PlanModels + ".Constraints",
        [typeof(NSchema.Plan.Model.Constraints.AddUniqueConstraint)] = PlanModels + ".Constraints",
        [typeof(NSchema.Plan.Model.Constraints.DropCheckConstraint)] = PlanModels + ".Constraints",
        [typeof(NSchema.Plan.Model.Constraints.DropExclusionConstraint)] = PlanModels + ".Constraints",
        [typeof(NSchema.Plan.Model.Constraints.DropUniqueConstraint)] = PlanModels + ".Constraints",
        [typeof(NSchema.Plan.Model.Constraints.SetConstraintComment)] = PlanModels + ".Constraints",
        [typeof(NSchema.Plan.Model.Domains.AddDomainCheck)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Model.Domains.AlterDomainDefault)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Model.Domains.AlterDomainNotNull)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Model.Domains.CreateDomain)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Model.Domains.DropDomain)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Model.Domains.DropDomainCheck)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Model.Domains.RecreateDomain)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Model.Domains.RenameDomain)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Model.Domains.SetDomainComment)] = PlanModels + ".Domains",
        [typeof(NSchema.Plan.Model.Enums.AddEnumValue)] = PlanModels + ".Enums",
        [typeof(NSchema.Plan.Model.Enums.CreateEnum)] = PlanModels + ".Enums",
        [typeof(NSchema.Plan.Model.Enums.DropEnum)] = PlanModels + ".Enums",
        [typeof(NSchema.Plan.Model.Enums.RenameEnum)] = PlanModels + ".Enums",
        [typeof(NSchema.Plan.Model.Enums.SetEnumComment)] = PlanModels + ".Enums",
        [typeof(NSchema.Plan.Model.Extensions.AlterExtension)] = PlanModels + ".Extensions",
        [typeof(NSchema.Plan.Model.Extensions.CreateExtension)] = PlanModels + ".Extensions",
        [typeof(NSchema.Plan.Model.Extensions.DropExtension)] = PlanModels + ".Extensions",
        [typeof(NSchema.Plan.Model.Extensions.SetExtensionComment)] = PlanModels + ".Extensions",
        [typeof(NSchema.Plan.Model.Indexes.CreateIndex)] = PlanModels + ".Indexes",
        [typeof(NSchema.Plan.Model.Indexes.DropIndex)] = PlanModels + ".Indexes",
        [typeof(NSchema.Plan.Model.Indexes.SetIndexComment)] = PlanModels + ".Indexes",
        [typeof(NSchema.Plan.Model.Routines.CreateRoutine)] = PlanModels + ".Routines",
        [typeof(NSchema.Plan.Model.Routines.DropRoutine)] = PlanModels + ".Routines",
        [typeof(NSchema.Plan.Model.Routines.RecreateRoutine)] = PlanModels + ".Routines",
        [typeof(NSchema.Plan.Model.Routines.RenameRoutine)] = PlanModels + ".Routines",
        [typeof(NSchema.Plan.Model.Routines.SetRoutineComment)] = PlanModels + ".Routines",
        [typeof(NSchema.Plan.Model.Schemas.CreateSchema)] = PlanModels + ".Schemas",
        [typeof(NSchema.Plan.Model.Schemas.DropSchema)] = PlanModels + ".Schemas",
        [typeof(NSchema.Plan.Model.Schemas.GrantSchemaUsage)] = PlanModels + ".Schemas",
        [typeof(NSchema.Plan.Model.Schemas.RenameSchema)] = PlanModels + ".Schemas",
        [typeof(NSchema.Plan.Model.Schemas.RevokeSchemaUsage)] = PlanModels + ".Schemas",
        [typeof(NSchema.Plan.Model.Schemas.SetSchemaComment)] = PlanModels + ".Schemas",
        // Sequences: fixes the lone-singular folder while it moves.
        [typeof(NSchema.Plan.Model.Sequence.AlterSequence)] = PlanModels + ".Sequences",
        [typeof(NSchema.Plan.Model.Sequence.CreateSequence)] = PlanModels + ".Sequences",
        [typeof(NSchema.Plan.Model.Sequence.DropSequence)] = PlanModels + ".Sequences",
        [typeof(NSchema.Plan.Model.Sequence.RenameSequence)] = PlanModels + ".Sequences",
        [typeof(NSchema.Plan.Model.Sequence.SetSequenceComment)] = PlanModels + ".Sequences",
        [typeof(NSchema.Plan.Model.Tables.AddForeignKey)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Model.Tables.AddPrimaryKey)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Model.Tables.CreateTable)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Model.Tables.DropForeignKey)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Model.Tables.DropPrimaryKey)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Model.Tables.DropTable)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Model.Tables.GrantTablePrivileges)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Model.Tables.RenameTable)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Model.Tables.RevokeTablePrivileges)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Model.Tables.SetTableComment)] = PlanModels + ".Tables",
        [typeof(NSchema.Plan.Model.Triggers.CreateTrigger)] = PlanModels + ".Triggers",
        [typeof(NSchema.Plan.Model.Triggers.DropTrigger)] = PlanModels + ".Triggers",
        [typeof(NSchema.Plan.Model.Triggers.SetTriggerComment)] = PlanModels + ".Triggers",
        [typeof(NSchema.Plan.Model.Scripts.ExecuteScript)] = PlanModels + ".Scripts",
        [typeof(NSchema.Plan.Model.Views.CreateView)] = PlanModels + ".Views",
        [typeof(NSchema.Plan.Model.Views.DropView)] = PlanModels + ".Views",
        [typeof(NSchema.Plan.Model.Views.RenameView)] = PlanModels + ".Views",
        [typeof(NSchema.Plan.Model.Views.SetViewComment)] = PlanModels + ".Views",
        [typeof(NSchema.Plan.PlanFile.IPlanFileWriter)] = PlanFile,
        [typeof(NSchema.Plan.PlanFile.PlanFileEnvelope)] = PlanFile, // likely thins once the single artifact lands
        [typeof(NSchema.Plan.PlanFile.PlanFileDeserializationException)] = PlanFile,

        // ── Apply: plan execution. ──
        [typeof(NSchema.Sql.SqlOptions)] = Apply,
        [typeof(NSchema.Sql.TransactionMode)] = Apply,

        // ── Current.Locks: guards the shared record against concurrent runs. ──
        [typeof(NSchema.State.IStateLockCoordinator)] = CurrentLocks, // renamed IStateLockManager
        [typeof(NSchema.State.StateLockCoordinatorExtensions)] = CurrentLocks, // renamed with its target
        [typeof(NSchema.State.IStateLockHandle)] = CurrentLocks,
        [typeof(NSchema.State.Model.StateLockInfo)] = CurrentLocks,
        [typeof(NSchema.State.Model.StateLockRequest)] = CurrentLocks,
        [typeof(NSchema.State.Model.StateLockedException)] = CurrentLocks,
        [typeof(NSchema.State.Model.StateLockMismatchException)] = CurrentLocks,
        [typeof(NSchema.State.IStateLock)] = CurrentLocksBackends,
        [typeof(NSchema.State.File.FileStateLockOptions)] = CurrentLocksBackends,

        // ── Current.Storage: the source's persistence — the recorded snapshot + ledger. ──
        [typeof(NSchema.State.Storage.ISchemaStateManager)] = CurrentStorage,
        [typeof(NSchema.State.Storage.StateReadArguments)] = CurrentStorage,
        [typeof(NSchema.State.Storage.StateReadResult)] = CurrentStorage,
        [typeof(NSchema.State.Storage.StateWriteArguments)] = CurrentStorage,
        [typeof(NSchema.State.Storage.StateWriteResult)] = CurrentStorage,
        [typeof(NSchema.State.Storage.StateRawReadArguments)] = CurrentStorage,
        [typeof(NSchema.State.Storage.StateRawReadResult)] = CurrentStorage,
        [typeof(NSchema.State.Storage.StateRawWriteArguments)] = CurrentStorage,
        [typeof(NSchema.State.Storage.StateRawWriteResult)] = CurrentStorage,
        [typeof(NSchema.State.StateDeserializationException)] = CurrentStorage,
        [typeof(NSchema.State.ISchemaStateStore)] = CurrentStorageBackends,
        [typeof(NSchema.State.File.FileSchemaStateStoreOptions)] = CurrentStorageBackends,

        // ── Current.Domain.Models ──
        [typeof(NSchema.State.Model.SchemaState)] = CurrentModels,
        [typeof(NSchema.Schema.Model.Scripts.ScriptExecution)] = CurrentModels, // the ledger entry — SchemaState is its aggregate root; the differ reads it as source vocabulary

        // ── Operations: one seam, one vocabulary — publics flatten to the root. ──
        [typeof(INSchemaOperations)] = Operations,
        [typeof(NSchema.Operations.Plan.PlanArguments)] = Operations,
        [typeof(NSchema.Operations.Plan.PlanResult)] = Operations,
        [typeof(NSchema.Operations.Plan.PlanTarget)] = Operations,
        [typeof(NSchema.Operations.Apply.ApplyArguments)] = Operations,
        [typeof(NSchema.Operations.Apply.ApplyResult)] = Operations,
        [typeof(NSchema.Operations.Refresh.RefreshArguments)] = Operations,
        [typeof(NSchema.Operations.Refresh.RefreshResult)] = Operations,
        [typeof(NSchema.Operations.Validate.ValidateArguments)] = Operations,
        [typeof(NSchema.Operations.Validate.ValidateResult)] = Operations,
        [typeof(NSchema.Operations.Drift.DriftArguments)] = Operations,
        [typeof(NSchema.Operations.Drift.DriftResult)] = Operations,
        [typeof(NSchema.Operations.Import.ImportArguments)] = Operations,
        [typeof(NSchema.Operations.Import.ImportResult)] = Operations,
        [typeof(NSchema.Operations.Doctor.DoctorArguments)] = Operations,
        [typeof(NSchema.Operations.Doctor.DoctorResult)] = Operations,
        [typeof(NSchema.Operations.Progress.OperationProgress)] = Operations,
        [typeof(NSchema.Operations.Progress.ProgressLevel)] = Operations,

        // ── Plugins: the host ↔ plugin-author contract. ──
        [typeof(NSchema.Plugins.INSchemaPlugin)] = Plugins,
        [typeof(NSchema.Plugins.INSchemaProviderPlugin)] = Plugins,
        [typeof(NSchema.Plugins.INSchemaBackendPlugin)] = Plugins,
        [typeof(NSchema.Plugins.ScaffoldContext)] = Plugins,
        [typeof(NSchema.Plugins.PluginConfigureResult)] = Removed, // unified onto Result
        // Config settings records: the plugin seam's message (the syntax-node side stays in Schema.Ddl).
        [typeof(NSchema.Configuration.ConfigBlock)] = Plugins,
        [typeof(NSchema.Configuration.ConfigValue)] = Plugins,
        [typeof(NSchema.Configuration.ConfigValueKind)] = Plugins,
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

    [Fact(Skip = "Activates in the v5 move phase: every surviving type must live at its target namespace.")]
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
