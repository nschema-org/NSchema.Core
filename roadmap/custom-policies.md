# Custom policies

Naming-convention enforcement out of the box, plus a Roslyn-analyzer-style seam for user-written rules.

- **Gate:** the *decisions* are pre-release. Most of the code can land later without breaking.
- **Repo:** Core (rule identity, seam, severity map), CLI (config surface, `policy` noun).
- **Mental model (Tom, 2026-07-09): `.editorconfig`, not analyzer packages.** The primary feature is a declarative, checked-in, per-rule severity
  and parameter surface. ALC policy plugins are the escape hatch, not the headline.

## Why it needs decisions now

Nearly all of this is retrofittable. Only a few things re-cut a public shape:

- Retrofittable: `Diagnostic` is a positional record, so an optional rule id added later is purely additive. `IProjectPolicy` can grow a member as
  a default interface member (precedent: `Acquire`).
- **Not retrofittable:** the enforcement overlap. `DestructiveActionPolicy` self-enforces via `IOptions`, and `WithDestructiveActionPolicy` is
  public builder API. When a central severity map lands, that builder is either redundant (removing it breaks) or becomes sugar over the map.
  Deciding which costs nothing now.
- **Not retrofittable:** `Source`'s meaning. If rule ids arrive and `Source` re-cuts from policy-name to category, anything keyed on it breaks silently.

## The finding that motivates it

- `SchemaLintPolicy` emits **four distinct rules** — no-primary-key, nullable-PK-column, duplicate key columns, duplicate index columns.
- All four carry one `Source`: `"schema-lint"`.
- So **`Source` is policy identity, not rule identity**, and a per-rule severity map has nothing to key on.
- You cannot currently say "no-primary-key is a suggestion, but nullable-PK is an error".
- Same shape in `DataHazardPolicy` / `DestructiveActionPolicy` / `EnumValueRemovalPolicy`: a `PolicyName` const used as `Source`.

## Proposed spine (all open)

### 1. Rule identity — where does the id live? RULED 2026-07-15

- **`PolicyDiagnostic : Diagnostic`**, carrying the rule id (and likely the offending address/node). Not on base `Diagnostic`.
- Rationale:
  - The root grammar is priced by public visibility and is meant to hurt to grow. Not every diagnostic is a rule — "file not found" has no id.
  - `Result<T, TDiagnostic>` already exists for exactly this; `NsqlDiagnostic` (position-bearing) is the precedent.
  - `IEnumerable<PolicyDiagnostic>` is covariant to `IEnumerable<Diagnostic>`, so policy findings fold into `Result<T>` with no translation.
  - `Downgrade` already returns `this with { Severity }`, and `with` preserves the derived type — so a central map can rewrite severity safely.
- Cost: changing the seam return type is breaking for implementers — which is why it is a pre-release decision.

### 2. Rule id spelling

- Options: opaque code (`NS0001`) vs hierarchical slug (`schema-lint/no-primary-key`).
- **Lean: slug.** It composes with the existing kebab `Source` values, is self-documenting, and reads correctly in a checked-in config —
  which is the `.editorconfig` feel Tom asked for.
- Counter, worth weighing: analyzers use opaque codes for a reason — they survive renames, grep cleanly, and never need i18n. A slug bakes naming
  into the contract, so renaming a rule becomes a breaking change.

### 3. Self-enforcement → central map. RULED 2026-07-15

- Today: each policy decides its own severity (`DestructiveActionPolicy` reads `IOptions<DestructiveActionOptions>`).
- Proposed: policies emit at a *natural* severity; a central map keyed by rule id overrides after collection.
- `PolicyEnforcement { Error, Warn, Allow, Ignore }` is already the enforcement vocabulary, so the map is `rule id → PolicyEnforcement`.
  - Maps cleanly onto `.editorconfig` severities. `Allow` = report as Info; `Ignore` = silent.
- **This resolves the recorded tension.** `destructive_action` was deliberately kept *out* of config-in-SQL (flag/env only), which collides with a
  checked-in per-rule map. A layered map dissolves it: defaults < config-in-SQL < flag/env. Same shape as `.editorconfig` plus CLI overrides.
- `WithDestructiveActionPolicy(PolicyEnforcement)` survives as sugar that writes one map entry. Non-breaking.

### 4. Rule discovery

- Does `IProjectPolicy` / `IPlanPolicy` declare its supported rules (Roslyn's `SupportedDiagnostics`)?
- **Lean: yes.** Config validation needs it — without discovery, a typo'd rule id in config is silently ignored, which is the single most common
  `.editorconfig` complaint. Also powers a `policy list` command.
- Can arrive as a DIM defaulting to empty, so this one is *not* strictly pre-release — but a default-empty rule set is a fudge worth avoiding.

### 5. Parameters

- Naming conventions need parameters (patterns, case style, which object kinds).
- Config-in-SQL is a closed statement set per grammar, so a `POLICY` statement is additive to the config grammar.
- Sketch, not designed: `POLICY naming_convention (tables = '^[a-z_]+$', severity = error);`

### 6. Custom policies via plugins

- The escape hatch, not the headline. ALC rails are nearly kind-agnostic already.
- The two-kind hardcoding is a bounded list: config-reader switch, `PluginReference` maps, builder `Configure*` pairs, doctor array,
  `PluginInventory`, init restore list.

## Open questions for the next session

1. Slug or opaque code?
2. Rule declaration on the seam: required member, or DIM with an empty default?
3. Does a `POLICY` statement in the config grammar carry both severity and parameters, or only parameters?
4. Which object kinds does the first naming-convention rule set cover, and what does it take as parameters?

ErrorOr's wider lessons — factories, implicit conversions, `Match`/`Switch` — moved to [result ergonomics](result-ergonomics.md). Only the rule-id
half landed here.
