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
    private const string SchemaModels = "NSchema.Schema.Domain.Models";
    private const string SchemaDesired = "NSchema.Schema.Desired";
    private const string SchemaCurrent = "NSchema.Schema.Current";
    private const string SchemaCurrentBackends = "NSchema.Schema.Current.Backends";
    private const string SchemaDdl = "NSchema.Schema.Ddl";
    private const string SchemaPolicies = "NSchema.Schema.Policies";
    private const string Diff = "NSchema.Diff";
    private const string DiffModels = "NSchema.Diff.Domain.Models";
    private const string DiffPolicies = "NSchema.Diff.Policies";
    private const string Plan = "NSchema.Plan";
    private const string PlanBackends = "NSchema.Plan.Backends";
    private const string PlanModels = "NSchema.Plan.Domain.Models";
    private const string PlanFile = "NSchema.Plan.PlanFile";
    private const string Apply = "NSchema.Apply";
    private const string StateLocks = "NSchema.State.Locks";
    private const string StateLocksBackends = "NSchema.State.Locks.Backends";
    private const string StateStorage = "NSchema.State.Storage";
    private const string StateStorageBackends = "NSchema.State.Storage.Backends";
    private const string StateModels = "NSchema.State.Domain.Models";
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

        // ── Schema.Desired ──
        [typeof(NSchema.Schema.IDesiredSchemaProvider)] = SchemaDesired,
        [typeof(NSchema.Schema.DesiredProjectResult)] = SchemaDesired,
        [typeof(NSchema.Schema.Model.DesiredProject)] = SchemaDesired, // seam message, currently misfiled in Model

        // ── Schema.Current ──
        [typeof(NSchema.Schema.ICurrentSchemaProvider)] = SchemaCurrent,
        [typeof(NSchema.Schema.SchemaSourceMode)] = SchemaCurrent,
        [typeof(NSchema.Schema.ISchemaProvider)] = SchemaCurrentBackends, // renamed ISchemaIntrospector

        // ── Schema.Ddl: language machinery + the full syntax tree. ──
        [typeof(NSchema.Schema.Ddl.DdlReader)] = SchemaDdl,
        [typeof(NSchema.Schema.Ddl.DdlWriter)] = SchemaDdl,
        [typeof(NSchema.Schema.Ddl.DdlFormatter)] = SchemaDdl,
        [typeof(NSchema.Schema.Ddl.DdlSyntaxException)] = SchemaDdl, // likely absorbed by DdlDiagnostic in the Result<T,TDiagnostic> conversion
        [typeof(NSchema.Schema.Ddl.Model.DdlDocument)] = SchemaDdl, // becomes the parsed-project root of the full AST
        [typeof(NSchema.Schema.Ddl.Model.SourcePosition)] = SchemaDdl,
        // Template constructs are language features, not domain models; reshaped as AST nodes.
        [typeof(NSchema.Schema.Model.Templates.TemplateDefinition)] = SchemaDdl,
        [typeof(NSchema.Schema.Model.Templates.TemplateApplication)] = SchemaDdl,
        [typeof(NSchema.Schema.Model.Templates.TemplateInclude)] = SchemaDdl,
        [typeof(NSchema.Schema.Model.Templates.TemplateKind)] = SchemaDdl,
        [typeof(NSchema.Schema.Model.Templates.TemplateSet)] = SchemaDdl,

        // ── Schema.Policies ──
        [typeof(NSchema.Schema.ISchemaPolicy)] = SchemaPolicies,
        [typeof(NSchema.Schema.Policies.SchemaLintPolicy)] = SchemaPolicies,
        [typeof(NSchema.Schema.Policies.StructuralIntegritySchemaPolicy)] = SchemaPolicies,

        // ── Schema.Domain.Models: the shared pipeline vocabulary. ──
        [typeof(NSchema.Schema.Model.DatabaseSchema)] = SchemaModels,
        [typeof(NSchema.Schema.Model.INamedObject)] = SchemaModels,
        [typeof(NSchema.Schema.Model.IRenameableObject)] = SchemaModels,
        [typeof(NSchema.Schema.Model.Columns.Column)] = SchemaModels + ".Columns",
        [typeof(NSchema.Schema.Model.Columns.IdentityOptions)] = SchemaModels + ".Columns",
        [typeof(NSchema.Schema.Model.Columns.SqlType)] = SchemaModels + ".Columns",
        [typeof(NSchema.Schema.Model.CompositeTypes.CompositeField)] = SchemaModels + ".CompositeTypes",
        [typeof(NSchema.Schema.Model.CompositeTypes.CompositeType)] = SchemaModels + ".CompositeTypes",
        [typeof(NSchema.Schema.Model.Constraints.CheckConstraint)] = SchemaModels + ".Constraints",
        [typeof(NSchema.Schema.Model.Constraints.ExclusionConstraint)] = SchemaModels + ".Constraints",
        [typeof(NSchema.Schema.Model.Constraints.ExclusionElement)] = SchemaModels + ".Constraints",
        [typeof(NSchema.Schema.Model.Constraints.UniqueConstraint)] = SchemaModels + ".Constraints",
        [typeof(NSchema.Schema.Model.Domains.Domain)] = SchemaModels + ".Domains",
        [typeof(NSchema.Schema.Model.Enums.EnumType)] = SchemaModels + ".Enums",
        [typeof(NSchema.Schema.Model.Extensions.Extension)] = SchemaModels + ".Extensions",
        [typeof(NSchema.Schema.Model.Indexes.IndexColumn)] = SchemaModels + ".Indexes",
        [typeof(NSchema.Schema.Model.Indexes.IndexNulls)] = SchemaModels + ".Indexes",
        [typeof(NSchema.Schema.Model.Indexes.IndexSort)] = SchemaModels + ".Indexes",
        [typeof(NSchema.Schema.Model.Indexes.TableIndex)] = SchemaModels + ".Indexes",
        [typeof(NSchema.Schema.Model.Routines.Routine)] = SchemaModels + ".Routines",
        [typeof(NSchema.Schema.Model.Routines.RoutineKind)] = SchemaModels + ".Routines",
        [typeof(NSchema.Schema.Model.Schemas.SchemaDefinition)] = SchemaModels + ".Schemas",
        [typeof(NSchema.Schema.Model.Schemas.SchemaGrant)] = SchemaModels + ".Schemas",
        [typeof(NSchema.Schema.Model.Sequences.Sequence)] = SchemaModels + ".Sequences",
        [typeof(NSchema.Schema.Model.Sequences.SequenceOptions)] = SchemaModels + ".Sequences",
        [typeof(NSchema.Schema.Model.Tables.ForeignKey)] = SchemaModels + ".Tables",
        [typeof(NSchema.Schema.Model.Tables.PrimaryKey)] = SchemaModels + ".Tables",
        [typeof(NSchema.Schema.Model.Tables.ReferentialAction)] = SchemaModels + ".Tables",
        [typeof(NSchema.Schema.Model.Tables.Table)] = SchemaModels + ".Tables",
        [typeof(NSchema.Schema.Model.Tables.TableGrant)] = SchemaModels + ".Tables",
        [typeof(NSchema.Schema.Model.Tables.TablePrivilege)] = SchemaModels + ".Tables",
        [typeof(NSchema.Schema.Model.Triggers.Trigger)] = SchemaModels + ".Triggers",
        [typeof(NSchema.Schema.Model.Triggers.TriggerEvent)] = SchemaModels + ".Triggers",
        [typeof(NSchema.Schema.Model.Triggers.TriggerLevel)] = SchemaModels + ".Triggers",
        [typeof(NSchema.Schema.Model.Triggers.TriggerTiming)] = SchemaModels + ".Triggers",
        [typeof(NSchema.Schema.Model.Views.View)] = SchemaModels + ".Views",
        [typeof(NSchema.Schema.Model.Views.ViewDependency)] = SchemaModels + ".Views",
        // The unified script model (per-lane merge: Script absorbs DataMigration in this lane).
        [typeof(NSchema.Schema.Model.Scripts.Script)] = SchemaModels + ".Scripts",
        [typeof(NSchema.Schema.Model.Scripts.RunCondition)] = SchemaModels + ".Scripts",
        [typeof(NSchema.Schema.Model.Scripts.ScriptType)] = SchemaModels + ".Scripts", // may fold into the unified event vocabulary
        [typeof(NSchema.Schema.Model.Migrations.DataMigration)] = Removed, // merged into Script
        [typeof(NSchema.Schema.Model.Migrations.DataMigrationTrigger)] = SchemaModels + ".Scripts", // becomes the change-event vocabulary

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
        [typeof(NSchema.Plan.Model.MigrationPlan)] = Plan, // becomes the single plan artifact (absorbs PlannedMigration + SqlPlan)
        [typeof(NSchema.Sql.ISqlGenerator)] = PlanBackends, // renamed ISqlDialect; shrinks to actions → statements
        [typeof(NSchema.Sql.Model.SqlPlan)] = Removed, // absorbed into the single plan artifact
        [typeof(NSchema.Sql.Model.SqlStatement)] = PlanModels,
        [typeof(NSchema.Sql.Model.ScriptHash)] = PlanModels,
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
        [typeof(NSchema.Plan.Model.Migrations.ExecuteDataMigration)] = PlanModels + ".Scripts", // renamed for the unified script model
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

        // ── State.Locks ──
        [typeof(NSchema.State.IStateLockCoordinator)] = StateLocks, // renamed IStateLockManager
        [typeof(NSchema.State.StateLockCoordinatorExtensions)] = StateLocks, // renamed with its target
        [typeof(NSchema.State.IStateLockHandle)] = StateLocks,
        [typeof(NSchema.State.Model.StateLockInfo)] = StateLocks,
        [typeof(NSchema.State.Model.StateLockRequest)] = StateLocks,
        [typeof(NSchema.State.Model.StateLockedException)] = StateLocks,
        [typeof(NSchema.State.Model.StateLockMismatchException)] = StateLocks,
        [typeof(NSchema.State.IStateLock)] = StateLocksBackends,
        [typeof(NSchema.State.File.FileStateLockOptions)] = StateLocksBackends,

        // ── State.Storage ──
        [typeof(NSchema.State.Storage.ISchemaStateManager)] = StateStorage,
        [typeof(NSchema.State.Storage.StateReadArguments)] = StateStorage,
        [typeof(NSchema.State.Storage.StateReadResult)] = StateStorage,
        [typeof(NSchema.State.Storage.StateWriteArguments)] = StateStorage,
        [typeof(NSchema.State.Storage.StateWriteResult)] = StateStorage,
        [typeof(NSchema.State.Storage.StateRawReadArguments)] = StateStorage,
        [typeof(NSchema.State.Storage.StateRawReadResult)] = StateStorage,
        [typeof(NSchema.State.Storage.StateRawWriteArguments)] = StateStorage,
        [typeof(NSchema.State.Storage.StateRawWriteResult)] = StateStorage,
        [typeof(NSchema.State.StateDeserializationException)] = StateStorage,
        [typeof(NSchema.State.ISchemaStateStore)] = StateStorageBackends,
        [typeof(NSchema.State.File.FileSchemaStateStoreOptions)] = StateStorageBackends,

        // ── State.Domain.Models ──
        [typeof(NSchema.State.Model.SchemaState)] = StateModels,
        [typeof(NSchema.State.Model.ScriptRecord)] = StateModels,

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
