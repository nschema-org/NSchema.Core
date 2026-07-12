# NSchema.Core architecture

This document outlines the target architecture for NSchema 5.0, agreed 2026-07-11. Until the 5.0 reorg completes, the code migrates toward this spec
in phases. The type-level migration map lives in `tests/NSchema.Core.Tests/Architecture/NamespaceMigrationMapTests.cs` as an executable registry, and
the position rules below are enforced by the architecture tests.

## The taxonomy

Every top-level namespace is one of exactly five things. Nothing names a technology, a layer, or a grab-bag (`Schema`, `Sql`, `Configuration`,
`Policies`, `Helpers`, and `Diagnostics` all dissolve in 5.0).

1. **Sources** — `Project` (the declared desired state; "desired" needs no qualifier — the project *is* what you want) and `Current` (what actually
   exists: observed live, or recorded). The two inputs the pipeline diffs; neither references the other. **Each source owns its persistence**:
   the Project's persistence format is DDL files (read via `.Ddl`), and Current's is the state store (`.Storage`, guarded by `.Locks`). Recorded
   state is two things with different natures: the schema capture is a rebuildable cache of current reality, but the run-once ledger is
   identity-bearing history — losing it re-runs scripts against data they already mutated. That is why planning and applying require a state
   store (a disposable database opts out explicitly with ephemeral state) and why replacing an unreadable payload demands force.
2. **Pipeline stages** — `Diff` → `Plan` → `Apply`: diff the sources, plan the difference, execute the plan. Dependencies point strictly backward
   along this chain, and stages may reference the sources' vocabulary but never the reverse (enforced by the dependency-direction tests).
3. **Supporting capabilities** — `Plugins` (the contract between plugin authors and hosts).
4. **Orchestration** — `Operations`. Anything that composes *across* capabilities is by definition orchestration and lives here.
5. **The root grammar** — the bare `NSchema` namespace: the composition entry points (`NSchemaApplication`, `NSchemaApplicationBuilder`, options) and
   the outcome vocabulary every consumer file speaks (`Result`, `Result<T>`, `Result<T, TDiagnostic>`, `Diagnostic`, `DiagnosticSeverity`, `PolicyEnforcement`).
   Root membership is a **closed list** — the one hand-maintained registry, and it should hurt to grow.

## Position rules: location is the classification

A feature is a set of capabilities. A public type's namespace position is its classification. Depth encodes audience, applied fractally:

| Position         | Audience                | Contents and invariants                                                          |
|------------------|-------------------------|----------------------------------------------------------------------------------|
| cluster root     | consumers               | The consumer seam(s), their implementations, and every record *shaped for* the seam ("seam messages live with their seam"). Boundary artifacts are vocabulary, not seam messages — they live in `.Domain.Models`, and a root with no seam is legitimately empty. Types shared between the root and its SPI also live here (consumer-optimal tie-break; the backend references upward). |
| `.Backends`      | plugin/backend authors  | The downstream SPI and built-in implementations. Never referenced by consumer code. |
| `.Policies`      | policy authors          | The policy seam (a domain extension point) plus built-in policies and their options. |
| `.Domain`        | nobody (internal)       | Domain services. Internal types; placement is hygiene, not contract.               |
| `.Domain.Models` | everyone (vocabulary)   | Pure model types: records, enums, model-shape interfaces, exceptions. No service references (enforced by the model-purity tests). |

Conventions that ride along:

- **A model lives with what it describes, not who produces it.** `DatabaseSchema` is the output of parsing on one path and introspection on the
  other; `ScriptRecord` is stamped by an apply — all live with their meaning, so locations stay meaning-stable.
- **Consumer facades are `I{X}Manager`** when they front mutation (`ISchemaStateManager`, `IStateLockManager`); read-only sources keep `Provider`.
  Backends are named for what they concretely are (`ISchemaStateStore`, `IStateLock`, `ISqlDialect`, `ISchemaIntrospector`).
- **No layer words** in namespaces (`Services`, `Helpers`, `Utils`, `Common`) and no grab-bag folders. A namespace names a capability or a position,
  never a layer.
- **Data shapes separate from the functional surface, fractally.** Within any capability namespace, model/data-shaped types sit in a `Models`
  child (`Project.Ddl.Models`, `Operations.Progress`), keeping the capability root for the machinery and seams. Same instinct as `.Domain.Models`,
  applied at whatever depth the capability lives.
- **Shape interfaces are earned by machinery, not by pattern-spotting.** `INamedObject`/`IRenameableObject` exist because the comparer's matching
  dispatches over them; `ScopeSchema` arrived with its consumers. A shared shape with no generic consumer is speculative surface — add it when the
  machinery that reads it arrives, shaped by what that machinery actually asks. (Corollary: in the model tree, belonging is *containment*, never a
  denormalized parent-name property.)
- **Two outcome shapes, and never `!`.** Total and silent → bare `T` (`CompareTeardown`, the structural comparer, the linearizer). Anything with
  findings or failure → `Result<T>` (an all-quiet `Result` is noise; a bare return documents totality). A consumer never uses the null-forgiving
  operator on `Value`: either check honestly (`is not { } x` → propagate) or, where a *producer-side invariant* says the value is always carried
  (the project comparer's `Compare`), call `Require()` — a violated invariant then fails loudly at the consumption site, naming the diagnostics,
  instead of as a null reference downstream.
- **Audience roles are consumer, implementer, and host.** `Plugins` is the one cluster whose root is *implemented* by plugin authors and *consumed* by
  hosts (the CLI); Core defines it because Core is the deepest assembly all parties share, but Core itself never calls it. The engine-major compatibility
  check lives with the contract so every host enforces it identically.

## The lane rule: same concept ≠ same model

A concept that appears in several contexts gets a model per context, with translation at the boundary — a script is a syntax node in a parsed
document, a declaration in the project, and an execution record in the ledger, and those are distinct types. Three tiers, enforceable as
namespace-reference rules:

1. **Source vocabulary** — each source contributes a vocabulary the stages may read, and the diff stage consumes both by definition (it diffs the
   sources): `Project.Domain.Models` is the subject language (the schema tree; the script declarations and events), `Current.Domain.Models` is the
   observation language (`SchemaState` and its ledger entries, `ScriptRecord`). Closed: nothing else is promoted into this tier, and the sources
   never reference the stages or each other.
2. **Stage artifacts** — each stage's output record is its contract with the next stage and flows only forward along the DAG: parsed documents →
   project → `DatabaseDiff` (the complete difference, implied script runs included) → the plan artifact.
3. **Lane-private models** — the DDL syntax tree, the state envelope, the plan-file envelope. These never cross their cluster boundary; anything
   wanting the information gets a translation at the seam (`DiffReader` and the document→settings config reader are this pattern).

## The clusters

```
NSchema                     app, builder, options · Result / Result<T> / Result<T,TDiag>,
                            Diagnostic, DiagnosticSeverity, PolicyEnforcement   [closed list]
├─ Project                  IProjectProvider (app.Project), Project, ProjectResult — the declared
│  │                        desired state; seam + messages at the root · no .Backends: DDL files
│  │                        are the only input
│  ├─ .Domain.Models        the project vocabulary: the DatabaseSchema tree (per-kind
│  │                        sub-namespaces) · the Script declarations and events
│  ├─ .Domain               projection: aggregation + template expansion
│  ├─ .Ddl                  DdlReader/DdlWriter/DdlFormatter · the full syntax tree (AST) incl.
│  │                        templates and config blocks · SourcePosition, DdlDiagnostic
│  └─ .Policies             ISchemaPolicy + built-ins (they validate the schema half)
├─ Current                  ICurrentSchemaProvider (app.CurrentSchema), SchemaSourceMode — what
│  │                        actually exists; reads the live database or falls back to the
│  │                        recorded snapshot in its own .Storage
│  ├─ .Backends             ISchemaIntrospector (the provider SPI)
│  ├─ .Storage              ISchemaStateManager (app.State) + argument/result records — the
│  │  │                     recorded snapshot + run-once ledger
│  │  └─ .Backends          ISchemaStateStore, serializer, file store
│  ├─ .Locks                IStateLockManager (app.Locks), request/info/handle, lock exceptions —
│  │  │                     guards the shared record against concurrent runs
│  │  └─ .Backends          IStateLock, FileStateLock
│  └─ .Domain.Models        SchemaState + ScriptRecord (its ledger entries; the envelope stays
│                           internal)
├─ Diff
│  ├─ .Reader               DiffReader + DiffDocument/DiffLine — the presentation read model (Plan.PlanFile analogue)
│  ├─ .Policies             IDiffPolicy + built-ins + options (provider policies plug in here)
│  ├─ .Domain.Models        the DatabaseDiff tree — the complete difference: schema changes plus
│  │                        the implied script runs (root Scripts list; nodes reference by name)
│  └─ .Domain               project comparer (run-once resolution + matching), structural comparer
│                           (per-kind handlers), matcher, normalizer, CurrentState
├─ Plan                     IMigrationPlanner → the single plan artifact (the complete diff + the
│  │                        statements that execute it); a dialect is required — no SQL-less plan
│  ├─ .Backends             ISqlDialect (one action in, statements out — scripts included)
│  ├─ .Domain.Models        MigrationAction hierarchy (per-kind, incl. internal ExecuteScript),
│  │                        SqlStatement
│  └─ .PlanFile             IPlanFileWriter + envelope
├─ Apply                    plan execution: ISqlExecutor (internal), TransactionMode, SqlOptions
├─ Operations               INSchemaOperations + all Arguments/Results/progress records (flattened —
│                           one seam, one vocabulary, one using) · slices stay as folders
└─ Plugins                  INSchemaPlugin + provider/backend plugin interfaces, config settings
                            records, ScaffoldContext, engine-major handshake
```

## Growth rules

- **Adding an operation**: a new slice folder (internal handler + public Arguments/Result records at the `Operations` root) + registration + a method
  on `INSchemaOperations`. Enforced by the operation-slice tests.
- **Adding a schema object kind**: model + diff node + comparer handler + linearizer rule + writer section + parser production + AST node. The per-kind
  completeness test makes forgetting one a build break, not a fidelity bug.
- **Adding a statement keyword**: the top-level dispatch is a closed set with no catch-all, so a new keyword is an additive, non-breaking change
  (files using it were previously syntax errors) — and a retired keyword truly leaves the grammar, failing as any unknown statement does.
- **Adding a script event kind**: a `ScriptEvent` record (which must answer `ScopeSchema` and `Description` — the scope rule and the ON-clause source
  text are declared properties of the event, never an ad-hoc split or a writer-side type switch) + its template-instantiation arm in the applicator + parser production + matcher/weaving semantics +
  (where annotatable) a `MigrationName` reference on the diff nodes it attaches to.
- **Adding a public type**: it must occupy a recognized position (or join the root closed list). The classification tests fail otherwise — decide what
  it is before shipping it.
