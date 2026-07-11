# NSchema.Core architecture

This document outlines the target architecture for NSchema 5.0, agreed 2026-07-11. Until the 5.0 reorg completes, the code migrates toward this spec
in phases. The type-level migration map lives in `tests/NSchema.Core.Tests/Architecture/NamespaceMigrationMapTests.cs` as an executable registry, and
the position rules below are enforced by the architecture tests.

## The taxonomy

Every top-level namespace is one of exactly four things. Nothing names a technology, a layer, or a grab-bag (`Sql`, `Configuration`, `Policies`,
`Helpers`, and `Diagnostics` all dissolve in 5.0).

1. **Pipeline stages** — `Schema` → `Diff` → `Plan` → `Apply`. The four steps of a migration: resolve what you have and what you want, diff them, plan
   the difference, execute the plan. Dependencies point strictly backward along this chain (enforced by the dependency-direction tests).
2. **Supporting capabilities** — `State` (recorded schema, run-once ledger, locking) and `Plugins` (the contract between plugin authors and hosts).
3. **Orchestration** — `Operations`. Anything that composes *across* capabilities is by definition orchestration and lives here.
4. **The root grammar** — the bare `NSchema` namespace: the composition entry points (`NSchemaApplication`, `NSchemaApplicationBuilder`, options) and
   the outcome vocabulary every consumer file speaks (`Result`, `Result<T>`, `Result<T, TDiagnostic>`, `Diagnostic`, `DiagnosticSeverity`, `PolicyEnforcement`).
   Root membership is a **closed list** — the one hand-maintained registry, and it should hurt to grow.

## Position rules: location is the classification

A feature is a set of capabilities. A public type's namespace position is its classification. Depth encodes audience, applied fractally:

| Position         | Audience                | Contents and invariants                                                          |
|------------------|-------------------------|----------------------------------------------------------------------------------|
| cluster root     | consumers               | The consumer seam(s), their implementations, and every record that crosses the seam ("seam messages live with their seam"). Types shared between the root and its SPI also live here (consumer-optimal tie-break; the backend references upward). |
| `.Backends`      | plugin/backend authors  | The downstream SPI and built-in implementations. Never referenced by consumer code. |
| `.Policies`      | policy authors          | The policy seam (a domain extension point) plus built-in policies and their options. |
| `.Domain`        | nobody (internal)       | Domain services. Internal types; placement is hygiene, not contract.               |
| `.Domain.Models` | everyone (vocabulary)   | Pure model types: records, enums, model-shape interfaces, exceptions. No service references (enforced by the model-purity tests). |

Conventions that ride along:

- **Consumer facades are `I{X}Manager`** when they front mutation (`ISchemaStateManager`, `IStateLockManager`); read-only sources keep `Provider`.
  Backends are named for what they concretely are (`ISchemaStateStore`, `IStateLock`, `ISqlDialect`, `ISchemaIntrospector`).
- **No layer words** in namespaces (`Services`, `Helpers`, `Utils`, `Common`) and no grab-bag folders. A namespace names a capability or a position,
  never a layer.
- **Audience roles are consumer, implementer, and host.** `Plugins` is the one cluster whose root is *implemented* by plugin authors and *consumed* by
  hosts (the CLI); Core defines it because Core is the deepest assembly all parties share, but Core itself never calls it. The engine-major compatibility
  check lives with the contract so every host enforces it identically.

## The lane rule: same concept ≠ same model

A concept that appears in several contexts gets a model per context, with translation at the boundary — a script is a syntax node in a parsed document,
a declaration in a desired project, a hash in a plan manifest, and an execution record in the state ledger, and those are four types. Three tiers,
enforceable as namespace-reference rules:

1. **Shared pipeline vocabulary** — `Schema.Domain.Models` only. The one tree every pipeline stage may reference (the diff annotates it, plan actions
   carry it, the dialect renders it). Closed: nothing else is promoted into this tier.
2. **Stage artifacts** — each stage's output record is its contract with the next stage and flows only forward along the DAG: parsed documents → desired
   project → `DatabaseDiff` → the plan artifact.
3. **Lane-private models** — the DDL syntax tree, the state ledger models, the plan-file envelope. These never cross their cluster boundary; anything
   wanting the information gets a translation at the seam (`DiffReader`, the document→settings config reader, the workflow's record→hash conversion are
   all this pattern).

## The clusters

```
NSchema                     app, builder, options · Result / Result<T> / Result<T,TDiag>,
                            Diagnostic, DiagnosticSeverity, PolicyEnforcement   [closed list]
├─ Schema                   (pure node)
│  ├─ .Domain.Models        the DatabaseSchema tree (per-kind sub-namespaces) · Script model
│  ├─ .Desired              IDesiredSchemaProvider, DesiredProject(Result) · projection + template
│  │                        expansion in .Domain · no .Backends: DDL files are the only input
│  ├─ .Current              ICurrentSchemaProvider, SchemaSourceMode
│  │  └─ .Backends          ISchemaIntrospector (the provider SPI)
│  ├─ .Ddl                  DdlReader/DdlWriter/DdlFormatter · the full syntax tree (AST) incl.
│  │                        templates and config blocks · SourcePosition, DdlDiagnostic
│  └─ .Policies             ISchemaPolicy + built-ins
├─ Diff                     DiffReader + DiffDocument/DiffLine/DiffSummary (presentation read model)
│  ├─ .Policies             IDiffPolicy + built-ins + options (provider policies plug in here)
│  ├─ .Domain.Models        the DatabaseDiff tree
│  └─ .Domain               comparer (per-kind handlers), matcher, normalizer
├─ Plan                     IMigrationPlanner → the single plan artifact (diff + actions + scripts
│  │                        + SQL + manifest); a dialect is required — there is no SQL-less plan
│  ├─ .Backends             ISqlDialect (actions in, statements out)
│  ├─ .Domain.Models        MigrationAction hierarchy (per-kind), SqlStatement, ScriptHash
│  └─ .PlanFile             IPlanFileWriter + envelope
├─ Apply                    plan execution: ISqlExecutor (internal), TransactionMode, SqlOptions,
│                           script composition around statements
├─ State                    (pure node)
│  ├─ .Locks                IStateLockManager, request/info/handle, lock exceptions
│  │  └─ .Backends          IStateLock, FileStateLock
│  ├─ .Storage              ISchemaStateManager + argument/result records
│  │  └─ .Backends          ISchemaStateStore, serializer, file store
│  └─ .Domain.Models        SchemaState, ScriptRecord
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
- **Adding a public type**: it must occupy a recognized position (or join the root closed list). The classification tests fail otherwise — decide what
  it is before shipping it.
