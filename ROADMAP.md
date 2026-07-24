# Roadmap

This document, and the `roadmap` directory, contain plans for potential features within the NSchema ecosystem.

I use this as a way to explore different plans and designs, so please don't take anything you read here as a guarantee. That said, if you spot a feature
here that you have thoughts on, let me know!

## Where things are written down

- `ROADMAP.md` acts as a high level view of upcoming features/changes, broken into sections.
- `roadmap/` contains detailed documents on those features/changes, their design and their reasoning. They should be considered living design documents until a feature actually ships.
- A decision graduates *out* of here: to ARCHITECTURE.md when it becomes a rule, to the CHANGELOG when it ships.
- This roadmap covers the entire NSchema ecosystem, regardless of where the feature/change actually needs to be implemented.

## V5.0

- **There is no specific gate for v5.0.** It will release with clean code, no (known) bugs, and no breaking changes left over.
- As far as I'm aware, I'm the only user of NSchema, so backwards compatibility is less important than doing it right.

## Future plans

### [NSQL linting](roadmap/nsql-linting.md)

Lint and formatting for NSQL, configured through literal `.editorconfig` support — the style half of what was "custom policies".
Ruled 2026-07-20: lint runs at the syntax layer, suppression is path-scoped sections, and linting gets no NSQL grammar of its own.

### [Custom policies](roadmap/custom-policies.md)

Project and plan policies: correctness checks (like "all tables must have a primary key") and guardrails, plus the plugin seam for
user-written rules. The `.editorconfig`-vs-plugins tension resolved by splitting: style went to linting, and plugins land here,
where checks run inside the plan/apply pipeline.

### Other, more vague, plans

- **Row-level security for Postgres.** Pure fidelity. No dependency on roles. Pilot kind for the per-kind handler decomposition.
- **Roles slice + grant fidelity** — model carries only schema and table grants today. Missing: routine `EXECUTE`, sequence `USAGE`, and
  `ALTER DEFAULT PRIVILEGES` (the big one — its absence forces the `RUN ALWAYS` workaround this roadmap calls a smell).
- **Partitioning** — pairs with change scripts; attach/detach is substantially data movement.
- **Editor support** — second file extension so editors can key a grammar without hijacking plain `.sql`. TextMate first.
  - Time-sensitive: custom constructs keep growing, and the opt-in extension gets costlier as projects accumulate `.sql` globs.
  - Also helps [linting](roadmap/nsql-linting.md) (editorconfig sections key on globs) and is the on-ramp to the LSP consumer
    of the [language package](roadmap/language-package.md).
- **Template arguments** — `TEMPLATE x(arg)` / `APPLY TEMPLATE x('val')`. The `{schema}` brace syntax was chosen so this could generalize.
- **Script drift in the drift report** — surface "run-once body changed since it executed".
  - Caveat: that pair is recorded-vs-*project*, so the project becomes a third input to drift, which today needs only the store and live provider.
- **`lastWrite` state metadata** — who/when/version. Full audit log and event-sourced state both rejected.
- **Docs** — canonical CI recipe (drift check + plan-on-PR via exit codes and `--json`); rollback-story page stating plainly that run-once and
  change scripts are one-way.
