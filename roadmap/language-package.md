# Language package

A lossless syntax tree (CST) for NSQL, and `NSchema.Language` as the eventual home of the language layer —
lexer, parser, syntax, formatter, linter, scaffolding factories. Direction agreed 2026-07-20; nothing built.

## The CST rework

- Today: `NsqlNode` carries a required `SourcePosition` but no trivia; the formatter is a token
  reformatting stream (comment attachment, blank-line capping — better than remembered, but structurally
  capped).
- A full-fidelity tree (trivia on nodes, round-trippable to source) unlocks:
  - **Structural reformatting** — wrapping column lists, aligning definitions. The token-level rules ship
    first without it ([[nsql-linting]]); the CST is the ceiling-raiser, not a prerequisite.
  - **Formatter and linter converge on one foundation** instead of tokens-vs-nodes.

## Generation convergence

The Roslyn playbook: **build syntax nodes, print them through the formatter honoring the user's
editorconfig.** Every text-producing surface converges on one pipeline:

- **Plugin scaffolding stops returning raw strings** — plugins return typed nodes instead. The current
  string-returning scaffold surface is the mess this fixes.
- **Import output picks up the user's style for free** (today `DdlWriter` has one opinion).
- **Interactive bootstrap** — the eventual goal: a guided `nschema init` where you're walked through
  starting a project, picking your database, picking your state, and importing existing objects. The
  wizard assembles config-block and import nodes; rendering is the formatter's job.

## The package split

- **Rule: packages follow consumers, not size.** Core having grown is a symptom worth watching, not
  itself the disease.
- Concrete consumer #1: **plugins referencing typed syntax for scaffolding** — real as soon as the CST
  lands. Consumer #2, still hypothetical: an LSP server / editor extension that wants parse + lint +
  format without diff, state, and operations (pairs with the TextMate/second-extension plan in ROADMAP
  "Editor support").
- Don't cut the package until a consumer is real. Until then, **keep the language layer internally
  clean** — no lint rule or syntax type reaching into Plan/Diff/State — so the split stays a `git mv`,
  not a refactor.
- Dependency posture per [[nsql-linting]]: Core-the-engine stays dependency-free; the language/tooling
  layer is the dependency-tolerant zone.

## Open questions

1. CST design: separate green/red trees à la Roslyn, or trivia fields on the existing `Syntax/` nodes?
2. Does `NsqlWriter`/`DdlWriter` survive as a model→nodes projection feeding the formatter, or stay a
   direct model→text path?
3. Package name (`NSchema.Language` vs `NSchema.Nsql`) and whether [[nsql-linting]]'s
   `NSchema.EditorConfig` folds into it when the split happens.
