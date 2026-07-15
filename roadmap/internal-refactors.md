# Internal refactors

Three passes, all behind internal seams. None of them can break a consumer — except the one public static.

- Sequencing: run these *after* the public-surface decisions land, so internal churn happens once against a settled surface.

## 1. Project assembly reads as a pipeline

Goal: `ProjectAssembler` reads cohesively as a staged pipeline. Removing `DatabaseAggregator` is part of that, not the point of it.

- `DatabaseAggregator` (100 lines) is a redundant second implementation of the accumulator's dedup job.
- Target: one accumulator collects the whole project; `ProjectAssembler` (116 lines) reads as a pipeline; import and templates absorb into the
  accumulator.
- Current sizes: `DatabaseAccumulator` 330, `ProjectAssembler` 116, `DatabaseAggregator` 100.
- **Open (Tom to confirm):** cataloged per-add dedup.
- ~15 `DatabaseTests` move off `Combine`.
- Phase 10 already did the adjacent cleanup: `IncludeResolver` extracted, `TemplateStatement` base, `TemplateInstance` record.

## 2. Per-kind handler decomposition

Goal: the comparer stops being a monolith and formalizes into per-kind handlers behind a thin walk-and-dispatch orchestrator.

- `DatabaseComparer` is 1,493 lines over 14 partials. The lumps: `DatabaseComparer.cs` (392) and `.Tables` (252).
- Only `IDatabaseComparer` is public, and it does not change. Fully internal.
- **The directives work already handed us the signature.** Every per-kind partial now takes its own typed slice, and `CompareObjects` plus the seven
  per-kind partials thread current + desired + slice. The handler interface was discovered, not designed — as predicted when decomposition was
  deliberately not bundled into that pass.
- Orchestrator hands each handler: current, desired, its typed directive slice, owner. Root directives are the orchestrator's.
- Completeness arch test: every kind has its full handler set. Extends the per-kind parallel-structure preference.
- The expression-problem tax stays (a new kind still touches model / diff / comparer / linearizer / writer / parser / importer) but becomes
  **mechanically enforced**.
- **RLS (5.1) is the pilot kind.** Old kinds move when touched.
- Applies to the linearizer and writer too, not just the comparer. The writer's split rides the AST work naturally (domain → AST → text).

## 3. Statics — finish the job

- Already static classes: `NsqlReader`, `NsqlWriter`, `NsqlFormatter`.
- **Remaining: `DiffReader`** — a public sealed class with `public static DiffReader Default { get; } = new()`.
- Ruled: it goes static at the consistency pass. **This overturns the documented "plain new-able + `.Default`" shape**, so CLAUDE.md needs the edit.
- The motivation for `.Default` was no-DI-ceremony, which a static class serves better. `new X().Read` compiling beside `X.Instance.Read` means the
  singleton never actually committed to anything.
- The commitment, taken knowingly for `NsqlReader` and the same here: **statics cannot implement interfaces**, so genuine future polymorphism means
  a deliberate redesign at a major. Consistent with there being no `IDiffReader` by design.
- It is public and the CLI presenter is its only consumer, so it folds into the CLI sweep's repack.
