# Plugin maturation

The `PLUGIN` block, the engine handshake, version ranges, the lockfile, and `plugin update` / `outdated`.

## The `PLUGIN` block

Separates **dependency declaration** from **configuration**, following Terraform's `required_providers` /
`provider` split.

```
PLUGIN pg ( source = 'nschema-org/postgres', version = '~> 5.0' );
PLUGIN s3 ( source = 'nschema-org/aws',      version = '~> 5.0' );

DATABASE pg ( connection_string = '...' );
STATE    s3 ( bucket = 'app-state' );
```

- **`PLUGIN` names the package, never the capability.** Capability-as-label would let one package be
  referenced twice at different versions.
- **`DATABASE` and `STATE` imply the capability.** `DATABASE` loads an `INSchemaDatabasePlugin`; `STATE`
  loads an `INSchemaStatePlugin`. The label references a declared `PLUGIN`.
- **A qualified form is the growth path**, if a package ever offers several plugins of the same capability:
  - `PLUGIN engines ( source = 'nschema-org/all', version = '[5.0,6.0)' );`
  - `DATABASE engines.postgres ( connection_string = '...' );`
  - The `postgres` identifier goes to something like an `INSchemaPluginCatalog`, which returns the right
    plugin.
  - Unlikely to be needed for database or state. **Custom policies are the plausible driver** — one analyzer
    package shipping many rule sets is the normal shape ([[custom-policies]]).

### Why the split

- **Dependency metadata and runtime config currently share one namespace.** `ConfigStatement` is just
  `(Identifier? Label, IReadOnlyList<ConfigAttribute> Attributes)` — so `version` sits in the same bag as
  `host` and `bucket`, and the resolver reserves a key inside the plugin author's attribute space. A plugin
  wanting its own `version` attribute collides.
- **It makes one-version-per-project structural.** Env overlays merge per-attribute on matching
  `(Type, Label)`, so a version *can* differ per env — which is why the old design needed a resolution-time
  validation over the union of envs. That check only existed because the version sat somewhere mergeable.
- **The merge rule falls out of the shape:** `PLUGIN` declarations are project-wide and never merge;
  `DATABASE` / `STATE` are per-env and do. The per-env thing is the connection string, never the version.
- **The label stops being a magic string.** `postgres` used to look like a database name (PG ships one).
  Now it is a local name you declared and can spell however reads best.

### Supersedes

- The **PLUGIN-declaration idea was superseded on 2026-07-11** in favour of keyed block merge with the
  version on the block. Reinstated: that design was settled *before* pinning and the lockfile were designed.
- **Keyed block merge survives** — it just stops carrying the version.

## Engine handshake

- **Status: does not exist.** `Plugins/` holds the plugin interfaces, `PluginSettings`, `ScaffoldContext`,
  `ConfigValue`/`ConfigValueKind`. No version metadata, no compatibility check.
- Was on the v5 list from 2026-07-10 and did not ship with the rest of the phases.
- **The argument is economic, not compatibility.** 5.0 already breaks the provider SPI (`ISqlDialect`,
  statements-only), so every provider re-releases anyway. Slipping it buys a second re-release round for the
  sake of one attribute.
- Goal: a plugin targeting NSchema 4 loaded into NSchema 5 says *"pg targets NSchema 4, this is NSchema 5 —
  update your pin"*, not a raw `MissingMethodException` out of ALC resolution.
- **Validation lives in Core, not the CLI** (ruled): so every host enforces identically.
- Second job: the version resolver filters candidates by engine compatibility.

## Version ranges + lockfile

- Designed 2026-07-11; nothing built.
- **Bare version = EXACT.** Ranges are opt-in.
- **`nschema.lock` is thin** — no content hashes.
- **`plugin update`** = highest-in-range, rewrites the lock only.
- `plugin list` / `show` / cache shipped 2026-06-27.
- **Open: is the lock keyed by package or by capability?** The `PLUGIN` block answers this — package — so the
  lock keys on `source`. Worth confirming it survives the qualified form.

## `engine_version` has no home yet

- The agreed cheap alternative to CLI/Core uncoupling is an `engine_version` range **assertion** in config —
  same version grammar as plugin pins, but it asserts, because the engine is compiled in. The fix is
  `dotnet tool update`.
- **There is no `NSCHEMA` statement.** The config grammar's whole statement set is `DATABASE` and `STATE`
  (was `PROVIDER` / `BACKEND`). The assertion needs a home before it can be built.

## Why the CLI keeps the loader

- CLI/Core uncoupling was considered and **rejected** (2026-07-10).
- The CLI consumes Core's whole consumer surface, so loading Core dynamically would freeze that API into a
  contract assembly exactly when 5.0 reshapes it, and would force ALC-within-ALC plugin resolution.
- Terraform precedent: engine in the binary, only providers as plugins.

## Why Plugins stays in Core

- Deepest-shared-assembly rule: plugins must reference Core anyway (introspector, dialect, builder types), so
  a separate contract package buys nothing.
- Classification: the audience taxonomy gains **host** — the `Plugins` root is consumed by hosts, implemented
  by plugin authors. Core is the venue, not a party.
