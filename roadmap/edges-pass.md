# Edges pass

A full sweep for edges that do not line up, and for simplifications the recent passes exposed.

- **Gate:** pre-release.
- **Repo:** Core (then CLI at the sweep).
- **The bar:** bundling scripts into directives is the reference example — not a tidy-up, but noticing that a thing held in three places was one
  thing all along. Look for that shape, not for lint.

## Why it runs late

- It wants to run after the public-surface decisions and the refactors, so it sweeps the final shape once rather than chasing moving code.
- XML docs on public types are **contract**, so a stale one is a real defect, not a typo.

## Found already

- **`PlanTarget.Recorded`** still documents *"falling back to the live database when nothing is recorded"*. The no-fallback doctrine killed that;
  CLAUDE.md now says the opposite. Dies with the [rename](targeted-operations.md) anyway, but the doc is wrong today.
- **`Diagnostic`'s summary is garbled:** *"A single structured finding, `<see cref="Result{T}"/>`, and the policy diagnostics."* That sentence does
  not parse — it reads like a merge casualty.
- **CLAUDE.md's `DiffReader` paragraph** goes stale the moment the static conversion lands.

## Candidate sweeps

- Public XML docs vs current truth, type by type. The three above came from reading four files — assume there are more.
- Doc drift across CLAUDE.md / ARCHITECTURE.md / CHANGELOG after ~10 phases of rulings, several of which reversed earlier ones.
- Things held in more than one place, per the scripts-are-directives shape.
- Naming: one word per sense. The sweep ratified `Schema` = the node, `Database` = the whole; check nothing has drifted back.
- Conditional-meaning fields — a property whose meaning depends on another property's value. `PlanArguments.Scope` was one; there may be others.
- Result convention: `Result` marks expected outcomes, total functions return bare values. Audit for both mistakes, not just the first.
- `Value!` count in src is zero. Keep it there; `Require()` is the sanctioned shape.
