# Plan surface

The `PlanArguments` record: what a plan is computed *from*, what it converges *to*, and what it *acts on*.

- **Gate:** the shape is pre-release. Object-granularity targeting is post-release.
- **Repo:** Core (arguments, workflow, filter), CLI (flags).
- Supersedes `roadmap/targeted-operations.md` — scope, targeting, teardown, and the current-side axis turned out to be one entangled design.

## RULED 2026-07-15: no planning against the live database

- **Plan always diffs recorded state → project.** The current-side axis is deleted, not renamed.
- Evidence that it was never a real axis:
  - `ComputePlan` reads state **unconditionally** — the run-once ledger only exists there.
  - The axis decided exactly one field: whether `CurrentState.Database` came from `GetDatabase(live)` or `state.Database`.
  - Even in Live mode the current side was a hybrid: `new CurrentState(currentSchema, state.Scripts)` — live schema stitched to a recorded ledger.
  - A three-value public enum for a single-field toggle on an otherwise state-sourced composite.
- Enabled by state stores being compulsory (disposable databases use the ephemeral backend), which was not true when the axis was designed.

### Consequences

- `SourceMode` **dies entirely** — `ComputePlan`'s parameter was its last mode-as-data site.
- `PlanTarget.Live` / `.Recorded` die; `PlanOperation`'s branch collapses.
- **Apply = refresh → plan → execute → refresh.** The first refresh is the live read plan used to do, written down honestly.
  - Bonus: a failed apply now leaves state reflecting pre-apply reality instead of stale.
- `Deployment` feeds only refresh, drift, and import. The DAG reads cleanly: **Deployment → (refresh) → State → (plan) → Diff.**
- The `Empty` + `Live` teardown hazard never exists — there is no live cell to tear down against.
- No `Source` / `Target` / `Baseline` word is needed for the current side at all, which settles the naming problem.

### Accepted cost

- **The read-only live plan is gone.** Reading reality without touching state is no longer possible; you refresh, which writes state and takes the lock.
- Hurts one case: a PR preview with DB read access but no state write. Mitigated — the canonical CI recipe is plan-against-recorded plus a drift check.
- **We now always trust the cache**, where Terraform refreshes by default and won't. Judged acceptable for databases specifically: cloud resources
  drift constantly, but nobody's autoscaler adds a column, and drift is first-class here.
- **Fallback if it bites:** not the old enum — Terraform's actual spelling, a freshness flag (`Refresh`: in-memory, don't write). Keeps the
  capability, still kills the three-value enum, but resurrects the teardown-against-live hazard.

## RULED: teardown is an empty desired side

- Teardown is not a third kind of plan. It is `Project`-vs-nothing: diffing recorded → empty drops everything.
- Kills the conditional-meaning smell where `Scope` was documented as "ignored for Teardown" — a field whose meaning depended on another field.
- `ComputeTeardown()` collapses into the normal plan path; teardown stops being an operation shape.
- **Scoping is no longer special-cased.** `Target=Empty, Scope=app` tears down schema `app`. Partial teardown falls out for free.
  - **Supersedes the 2026-07-12 ruling** that narrowing a teardown is targeting-not-scoping and `ComputeTeardown()` stays parameter-free. That
    rested on teardown's recorded-only/no-project/no-policies being *definitional*; they are coordinates, not definitions.

## Naming — lean, needs a nod

- **`PlanTarget { Project, Empty }`** — the type keeps its name, every value changes.
- The word reclaims its natural meaning: *the state we are heading toward*. That was the reading flagged as ambiguous while `Target` also meant
  "the thing we compare against".
- Nothing is called `Source`, so it never collides with `AddProjectSource`, the taxonomy's three sources, or `SourceMode`.
- Note the drift from the earlier "for sure, let's rename `PlanTarget`" — the *type name* survives; its meaning and values do not.

## Scope absorbs targeting — agreed in direction, design open

- Raised by Tom: scope and target are mutually exclusive, so they will need bundling anyway.
- They are **one axis at two granularities**. Both answer "what part of the database is this run about?"
  - `--target app.users` already implies scope `app`; `--scope billing --target app.users` is simply contradictory.
  - Terraform precedent: no separate schema scope. `-target` takes addresses at whatever granularity, module or resource.
- **Direction:** grow `SchemaScope` (today `All | Of(names)`, exposing `SchemaNames` / `Includes`) into an address set.
  - `Of("app")` = whole schema. `Of("app.users")` = one object.
  - Read seams keep taking schema names, via a projection derived from the address set.
  - Remaining narrowing applies to the diff, post-compare, with dependency closure.
  - One user-facing concept, two internal applications. The mechanism split (push schema granularity into the reads, filter the rest after) is an
    optimization, not a second idea.
- **Supersedes the 2026-07-12 "never merged" ruling**, which rested on targeting putting no pressure on the source seams. It derives a read scope,
  so it does — without complicating the seam.
- **Worth checking: closure is probably already a latent hole.** Scope does no dependency closure today. Scope to `app` while `billing.orders` has
  an FK to `app.users`, then drop `app.users`, and the plan breaks an object it never read. Unifying makes closure one problem, solved once.

## Columns, or objects only?

- **Lean: objects only.** Terraform targets a resource, not an attribute. The diff tree's unit of change is the object, and closure at column
  granularity is nasty — a column drop can be entailed by a table rebuild.
- Not urgent. The address vocabulary exists (`ObjectReference`, `MemberReference`, `RoutineReference`, `ScriptReference`) but shares **no base**;
  `ValueObject<T>` is for single-value primitives, not addresses. Introducing an `Address` base later is additive.

## Policies on an empty desired state — RULED: they always run

- No special case needed; the question dissolves.
  - **Project policies** have no input — `Target=Empty` reads no project. Vacuous, not disabled.
  - **Plan policies** run normally. The artifact exists, and a teardown genuinely *is* destructive: that finding is correct, not noise.
  - Apply already re-runs plan policies against the carried diff, so a plan-side bypass only buys "plan says fine, apply says blocked".
- **Tom's framing:** a teardown must always be *possible* — it is an escape hatch — but it does not have to be *easy*. Destroying your database
  should require acknowledging the warnings we put in the way.
- **Blocked-by-default is the honest outcome.** Blocked plans still carry the full artifact and still write to `OutFile`, so review loses nothing.
- **`destroy` sets `--destructive-actions allow`, not `Force`.**
  - `Force` blanket-demotes *every* policy error to a warning, including data hazards, which have nothing to do with intending destruction.
  - The enforcement level says exactly what is meant and leaves other policies able to block.
  - `destroy` already encodes the intent, so it sets it for you; `apply --destructive-actions allow` is the explicit long form.
  - **Supersedes** the recorded "CLI destroy must pass `Force=true`" sweep item.
