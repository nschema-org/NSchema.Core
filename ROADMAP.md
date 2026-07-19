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

### [Custom policies](roadmap/custom-policies.md)

I'd really like to support extra validation rules like "all tables must have a primary key called `id`" or "all tables must be pluralized".
I'm currently torn between going through `.editorconfig` and a plugin-based approach similar to `PROVIDER` and `BACKEND` blocks.

### [Plugin safety](roadmap/plugin-maturation.md)

At the moment, there's no guarantee that the plugins you install are compatible with the version of NSchema you're using. We're also missing a lot of
utility features like updating plugins, listing outdated plugins, supporting version ranges, lockfiles, etc.

### [Edges pass](roadmap/edges-pass.md)

Minor consistency cleanup, making sure that after all the refactoring we've done, none of the documentation has fallen out of line.

### Other, more vague, plans

- **Row-level security for Postgres.** Pure fidelity. No dependency on roles. Pilot kind for the per-kind handler decomposition.
- **Roles slice + grant fidelity** — model carries only schema and table grants today. Missing: routine `EXECUTE`, sequence `USAGE`, and
  `ALTER DEFAULT PRIVILEGES` (the big one — its absence forces the `RUN ALWAYS` workaround this roadmap calls a smell).
- **Partitioning** — pairs with change scripts; attach/detach is substantially data movement.
- **Editor support** — second file extension so editors can key a grammar without hijacking plain `.sql`. TextMate first.
  - Time-sensitive: custom constructs keep growing, and the opt-in extension gets costlier as projects accumulate `.sql` globs.
- **Template arguments** — `TEMPLATE x(arg)` / `APPLY TEMPLATE x('val')`. The `{schema}` brace syntax was chosen so this could generalize.
- **Script drift in the drift report** — surface "run-once body changed since it executed".
  - Caveat: that pair is recorded-vs-*project*, so the project becomes a third input to drift, which today needs only the store and live provider.
- **`lastWrite` state metadata** — who/when/version. Full audit log and event-sourced state both rejected.
- **Docs** — canonical CI recipe (drift check + plan-on-PR via exit codes and `--json`); rollback-story page stating plainly that run-once and
  change scripts are one-way.
