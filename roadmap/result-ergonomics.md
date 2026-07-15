# Result ergonomics

Making `Result<T>` pleasant where it is currently ceremonial. **Design not started — parked deliberately.**

- **Gate:** open. Mostly additive, so probably post-release — but confirm before the freeze, since combinators on a public type are a public shape.
- **Repo:** Core.
- Non-goal: taking a dependency. `Result` stays hand-rolled (ruled 2026-06-28).

## The shape we have

- Ours is an **And**, not an **Or**: value *and* diagnostics, always. ErrorOr is value *xor* errors.
- That difference is load-bearing — the full-artifact-on-block rule depends on it. Do not borrow it away.
- Two typed shapes plus one rule: bare `T` for total+silent; `Result<T>` for everything else; never `!` on `Value` — honest check, or `Require()`
  where a producer-side invariant holds.

## Problem 1: accumulating diagnostics across many results

The pattern Tom named: accumulate a `List<Diagnostic>` across lots of different results, then combine them with a final value.

- **Exemplar: `MigrationWorkflow.ComputePlan`, lines ~50–80.** Three sequential reads (project, state, current), each guarded, each concatenating
  a chain that grows by one term per read:
  - `desired.Diagnostics`
  - `desired.Diagnostics.Concat(read.Diagnostics)`
  - `desired.Diagnostics.Concat(read.Diagnostics).Concat(currentRead.Diagnostics)`
  - then a `List<Diagnostic>` of all three.
- Hand-maintained, and every new read makes every downstream guard longer.

**Candidate: an And-result is a Writer** — a value plus an accumulated log, where bind concatenates the logs.

- Add `Select` / `SelectMany` and the passage becomes query syntax:
  `from project in GetProject(scope) from state in ReadState() from current in ReadCurrent() select Plan(...)`
- Diagnostics accumulate automatically; failure short-circuits. Writer-plus-short-circuit is precisely our semantics.
- Deletes the guard-and-concat ladder rather than tidying it.
- Open: is LINQ query syntax too clever for the house style? A plain collector (`bag.Take(result, out var value)`) is the duller alternative.

**Before designing: survey the real accumulation sites.** `ComputePlan` is the best example, not necessarily the representative one.

## Problem 2: Match / Switch with And-semantics

- Tom's framing: they could survive with **different semantics** to ErrorOr's.
  - Allow multiple cases to run, without returning a value.
  - Separate handlers for warnings and errors.
- Reads as a distinct need from accumulation — mostly presentation, mostly CLI-side. Keep the two separate so neither warps the other.
- Open: in an And-shape there is no either/or to match on. The honest analogue is closer to "run this if unblocked".

## Problem 3: factory + conversion ergonomics

What is worth borrowing from ErrorOr, which Tom rates for exactly this:

- **Implicit conversions.** ErrorOr converts implicitly from `T`, from a single error, and from a list. Audit how much ceremony that would delete at
  our seam boundaries.
- **Factory ergonomics.** ErrorOr's typed factories are the same instinct as our internal `{Area}Diagnostics` catalogs. Open question: do *policy*
  diagnostics deserve public factories, given the catalogs are deliberately internal (wording is implementation, not contract)?
- **`Code` + `Description` + `Metadata`.** ErrorOr's `Code` is exactly a rule id — see [custom policies](custom-policies.md), where that is already
  ruled onto a `PolicyDiagnostic` subtype rather than base `Diagnostic`. Its metadata bag is the structured-context-as-data on-ramp the catalogs
  were noted as enabling; a **typed address** beats an untyped bag for us.
