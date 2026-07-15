# NSchema.Core architecture

This document outlines the target architecture for NSchema 5.0, agreed 2026-07-11. Until the 5.0 reorg completes, the code migrates toward this spec
in phases. The type-level migration map lives in `tests/NSchema.Core.Tests/Architecture/NamespaceMigrationMapTests.cs` as an executable registry, and
the position rules below are enforced by the architecture tests.

## The taxonomy

Every top-level namespace is one of exactly five things. Nothing names a technology, a layer, or a grab-bag (`Schema`, `Sql`, `Configuration`,
`Policies`, `Helpers`, and `Diagnostics` all dissolve in 5.0).

1. **Sources** — the inputs the pipeline diffs. `Project` (the declared desired state; "desired" needs no qualifier — the project *is* what you
   want, read from DDL files via `.Nsql`); `Deployment` (the live database, read through an introspector — the running instance you are managing);
   and `State` (what was recorded about the deployment: the captured schema, the run-once ledger, and the locks that guard them). Each names a
   noun, not a temporal stance — the old `Current` contrasted against a "desired" that is now just `Project`, so it was retired. `Deployment` and
   `State` are the current side split in two: reading the live database and persisting our record of it are different jobs that were only bundled
   while one provider fronted both. The sources never reference each other or the stages. Recorded state is two things with different natures: the
   schema capture is a rebuildable cache of current reality, but the run-once ledger is identity-bearing history — losing it re-runs scripts against
   data they already mutated. That is why planning and applying require a state store (a disposable database opts out explicitly with ephemeral
   state) and why replacing an unreadable payload demands force.
2. **Pipeline stages** — `Diff` → `Plan` → `Apply`: diff the sources, plan the difference, execute the plan. Dependencies point strictly backward
   along this chain, and stages may reference the sources' vocabulary but never the reverse (enforced by the dependency-direction tests). `Apply` is
   the one *effectful* stage — it writes to the `Deployment` — and stays a stage rather than folding into `Deployment` because it consumes the plan
   (downstream of `Plan`) while the sources feed `Diff` (upstream): merging them would cycle the DAG through the ledger vocabulary `Diff` reads.
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

- **A model lives with what it describes, not who produces it.** `Database` is the output of parsing on one path and introspection on the
  other; `ScriptRecord` is stamped by an apply — all live with their meaning, so locations stay meaning-stable.
- **Consumer facades are `I{X}Manager`** when they front mutation (`IDatabaseStateManager`, `IStateLockManager`); read-only sources keep `Provider`.
  Backends are named for what they concretely are (`IDatabaseStateStore`, `IStateLock`, `ISqlDialect`, `IDatabaseIntrospector`).
- **No layer words** in namespaces (`Services`, `Helpers`, `Utils`, `Common`) and no grab-bag folders. A namespace names a capability or a position,
  never a layer.
- **Data shapes separate from the functional surface, fractally.** Within any capability namespace, model/data-shaped types sit in a `Models`
  child (`Project.Ddl.Models`, `Operations.Progress`), keeping the capability root for the machinery and seams. Same instinct as `.Domain.Models`,
  applied at whatever depth the capability lives.
- **Shape interfaces are earned by machinery, not by pattern-spotting.** `INamedObject` exists because the comparer's matching
  dispatches over it; `ScopeSchema` arrived with its consumers. A shared shape with no generic consumer is speculative surface — add it when the
  machinery that reads it arrives, shaped by what that machinery actually asks. (Corollary: in the model tree, belonging is *containment*, never a
  denormalized parent-name property.)
- **Stateless is static; stateful or configured is constructed.** A pure function over data (`NsqlReader`, the assembler, the
  expander, the hashing and token services) is a static class — parameterization, if it comes, is arguments, not instance state.
  A type is constructed only when instances genuinely differ (a parser's cursor, a configured backend). No `Instance`/`Default`
  singletons: they leave two spellings of every call and commit to nothing.
- **Two outcome shapes, and never `!`.** Total and silent → bare `T` (the structural comparer, the linearizer). Anything with
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
   sources): `Project.Domain.Models` is the subject language (the schema tree; the scripts — an abstract `Script` with `ChangeScript`/`DeploymentScript` kinds, never aggregated; the management directives),
   `State.Domain.Models` is the observation language (`DatabaseState` and its ledger entries, `ScriptExecution`). Closed: nothing else is promoted
   into this tier, and the sources never reference the stages or each other.
   **Statements declare; directives steer.** The schema tree is pure observation vocabulary — introspection can produce every field on it, so it
   carries no `OldName`, no `IsPartial`, no `Dropped*`. Management intent lives on `ProjectDirectives`: per-kind slice records in their subject
   namespaces (kind is encoded *structurally* — which property you are in — so no consumer type-switches, and a kind that cannot take a verb simply
   lacks the field), cross-kind directives at the root (crossing kinds is the orchestrating walker's job). **Scripts are directives too** — a
   change-event script steers the member it prepares, so it rides that kind's slice (`TableDirectives.ChangeScripts`); a deployment script bookends
   the run without a subject, so it is the archetypal root directive (`ProjectDirectives.DeploymentScripts`). Directive application is deliberately
   *not* on the directives (behavior lives with the composition it needs — the comparer's matching); what a directive owns is its address. Directive
   addresses name **current reality** — the names things have now — with one exception: a partial marks the project's own declaration, so it carries
   the declared name. The rules that need no current state (target declared, source not, no chains/collisions, no rename-of-dropped, no
   drop-of-declared) validate at the read; the rule that needs current state (an applied rename is spent) is the differ's, reported as a
   self-expiry info.
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
│  ├─ .Domain.Models        the project vocabulary: the Database tree (per-kind
│  │                        sub-namespaces) · the Script declarations and events
│  ├─ .Domain               projection: aggregation + template expansion + scope filtering
│  ├─ .Ddl                  DdlReader/DdlWriter/DdlFormatter · the full syntax tree (AST) incl.
│  │                        templates and config blocks · SourcePosition, DdlDiagnostic
│  └─ .Policies             IProjectPolicy + built-ins (validate the declared project)
├─ Deployment               IDatabaseProvider (app.Database) — the live database, read through an
│  │                        introspector; GetLive only, the recorded read is a plain State read
│  └─ .Backends             IDatabaseIntrospector (the provider SPI)
├─ State                    IDatabaseStateManager (app.State) + argument/result records — the
│  │                        recorded snapshot + run-once ledger
│  ├─ .Backends             IDatabaseStateStore, serializer, file store
│  ├─ .Locks                IStateLockManager (app.Locks), request/info/handle, lock exceptions —
│  │  │                     guards the shared record against concurrent runs
│  │  └─ .Backends          IStateLock, FileStateLock
│  └─ .Domain.Models        DatabaseState + ScriptExecution (its ledger entries; the envelope stays
│                           internal)
├─ Diff
│  ├─ .Reader               DiffReader + DiffDocument/DiffLine — the presentation read model (Plan.PlanFile analogue)
│  ├─ .Domain.Models        the DatabaseDiff tree — the complete difference: schema changes with
│  │                        each change script inlined on its node, deployment scripts at the root
│  └─ .Domain               project comparer (run-once resolution + matching), structural comparer
│                           (per-kind handlers), matcher, normalizer, CurrentState
├─ Plan                     IMigrationPlanner → the single plan artifact (the complete diff + the
│  │                        statements that execute it); a dialect is required — no SQL-less plan
│  ├─ .Backends             ISqlDialect (one action in, statements out — scripts included)
│  ├─ .Policies             IPlanPolicy + built-ins + options (provider policies plug in here;
│  │                        post-render, they see exactly what an apply would execute)
│  ├─ .Domain.Models        MigrationAction hierarchy (per-kind, incl. internal ExecuteScript),
│  │                        SqlStatement
│  └─ .PlanFile             IPlanFileManager + envelope
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
- **Comparing strings**: decide what the string *is* first. An **object name** is an `SqlIdentifier` — case-insensitive equality is baked into the type,
  so name-bearing properties, keyed collections, and record equality follow the rule with no comparer at the call site (and no call site may re-decide
  it). **Opaque SQL** — a body, definition, or expression NSchema carries but does not interpret — is a `SqlText`: ordinal equality (it's data), typed so
  the type system marks where foreign SQL travels. **Other data** (enum labels, hashes, comments, prose) compares exactly and stays `string`.
  **Language keywords and config keys** are the parser/formatter's own case rules and stay explicit at their match sites. A `string`-typed property
  that actually holds an identifier or opaque SQL is an enforcement hole — type it. Both types derive from the abstract `ValueObject` record — one
  `Value` property, equality by value, renders as its value — and each derived type owns its comparison semantics (the record default is ordinal;
  `SqlIdentifier` overrides to case-insensitive). The base is machinery, not a framework: no validation or semantics live on it, and serialization is
  one converter per wire concern, not per type — `ValueObjectJsonConverter` is a `JsonConverterFactory` that covers every `ValueObject<T>`
  automatically through the type's single-value constructor (the Verify converter and test bridge generalize the same way).
  The types have **no implicit string conversions** and no mixed-operand equality: construction (`new SqlIdentifier(text)`)
  and reads (`.Value`) are explicit, so a raw string never slips past the checkpoint in either direction. Comparing a value object to a raw primitive
  in domain or application code is usually a sign the primitive side should be elevated to the same type (the template placeholder is an
  `SqlIdentifier` constant for exactly this reason) — asserting on the underlying value is a test-side concern. **`.Value` is exceptional**: it marks
  an intentional exit from identifier semantics into text work (rendering, file paths, keyword matching, token substitution), and it belongs inside
  the component doing that text work — an unwrap whose result feeds another identifier-shaped parameter, a keyed collection, or a constructor is
  laundering, not an exit (ToString covers interpolation, so even display rarely needs it). Diagnostic factories and prose helpers take the value
  object and render it themselves; callers pass what they hold.
- **Names label; addresses identify.** A `Name` in the schema tree is a *scoped label* — meaningful only within its container (a column's name
  identifies it within its table, as `pg_attribute` keys on `(attrelid, attname)`); no node in the aggregate is self-identifying, by design.
  `SqlIdentifier` models the engine's name-resolution semantics (fold on compare), which is why it is domain vocabulary and not a lexing concern —
  the lexer tokenizes, it never compares; the written form (position, quoting) belongs to the language lane's own identifier node when the AST lands.
  Pointing at a node *from outside the tree* takes an **address**: `ObjectReference` (schema + name, both required — an address that isn't fully
  qualified identifies nothing; component-wise identifier equality, never a dotted string smuggled through `SqlIdentifier`), `ScriptReference`
  (scope schema + name — the one address whose container is *genuinely optional by domain*: a null schema means the script is global, living at the
  project root, not that resolution is deferred), and `MemberReference` (schema + object + member — a column rename's subject). An address is distinct from a **reference as written** (`RoutineReference`, optionally qualified —
  an unqualified routine reference is resolved by the engine's search path, so it stays as declared; resolving one sets its schema part, never
  concatenates text). Renames, change-event script matching, and targeting
  all consume addresses. Address *resolution* (address → node) is a domain service over the pure tree (`AddressLookup`, when its first consumer
  arrives), and *walking* is the per-kind handler orchestrator — the tree stays the stored truth because it is the language's truth; flatness lives
  at the seams that want it (index keys, the linearizer's output).
