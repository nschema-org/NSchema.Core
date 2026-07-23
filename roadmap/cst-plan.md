# CST implementation plan

Working plan for the lossless-tree rework agreed in [[language-package]]. Written 2026-07-22 so any
session can pick it up cold; update status markers as phases land. Branch: `feature/cst`.

Scope-address parsing (`--scope`, `Address.Parse`) is **deliberately deferred** — it needs only the
lexer, not the CST, and will be planned separately.

## Design rulings (settled 2026-07-22)

- **One tree.** The existing `Project/Nsql/Syntax/` records *become* the CST. No separate AST above
  it, no third tree — the architecture is already Roslyn-shaped (syntax → `DocumentProjector` as
  binder → domain model), so the fix is making the one tree lossless.
- **Roslyn's trivia model without Roslyn's machinery.** No green/red split, no parent pointers, no
  incremental reparse, no codegen. Immutable records, hand-written. Revisit only if an LSP becomes
  a real consumer.
- **The total ownership rule** (what makes round-trip provable, no exceptions):
  1. Every character of source belongs to exactly one token (as raw text or trivia).
  2. Trivia belongs to tokens: trailing trivia runs up to and including the first newline after the
     token; everything past that is the next token's leading trivia. (Roslyn's attachment rule.)
  3. Every token belongs to exactly one node, stored in a field; printing walks fields in source
     order.
- **Load-bearing test:** `Print(Parse(s)) == s`, byte-for-byte, over the entire fixture corpus,
  including error-recovery fixtures. Everything else in this plan serves that property.
- **Keyword casing is preserved** — keyword case is itself a formatting rule, so the tree must keep
  it. Keywords already lex as `Identifier` with their text, so this falls out; it does mean keyword
  tokens are stored on nodes (most of the field-count growth).
- **Doc comments stay tokens, not trivia.** They are semantic (projected into the model), and the
  parser already consumes `DocComment` in the statement loop. Only whitespace, newlines, and
  non-doc comments become trivia.
- **Opaque spans stay opaque.** `CaptureRawSpan` / `CaptureParenthesized` output becomes a verbatim
  raw token in the tree. The formatter never formats inside them (consistent with the
  cosmetic-only view-body ruling).
- **Synthetic trees carry no trivia.** Factories build tokens with empty trivia and
  `SourcePosition.None`; the formatter's job is normalising a trivia-less tree into styled text.
  This answers [[language-package]] open question #2: `NsqlWriter` survives as model→nodes, and
  printing belongs to the formatter.

## Phase 1 — token layer (full-fidelity lexing)

Status: **done** — full-fidelity lexer + `Trivia`/`Token.Raw`/`Leading`/`Trailing`; formatter kept
behaviour-neutral via a trivia→comment-token flatten shim (see review note). Round-trip property landed.

Goal: the lexer emits every character; behaviour-neutral for everything downstream.

- New `Trivia` type in `Project/Nsql/Tokens/`: kind (`Whitespace`, `EndOfLine`, `LineComment`,
  `BlockComment`) + verbatim text.
- `Token` (`Tokens/Token.cs`) gains:
  - `Raw` — the verbatim source text (quotes, escapes, delimiters included). `Text` keeps its
    current decoded-payload meaning; both are needed (`Text` for semantics, `Raw` for printing).
  - `Leading` / `Trailing` trivia lists, attached per the ownership rule. EOF token holds the
    file's trailing trivia as leading.
  - Width/end derived from `Raw`, not stored.
- `NsqlLexer`: delete the `emitComments` flag — always full fidelity. Non-doc comments and
  whitespace become trivia instead of skipped/optional tokens. `LineComment`/`BlockComment` leave
  `TokenKind` (they are trivia kinds now).
- `NsqlFormatter` migrates onto trivia-bearing tokens (it is the only `emitComments: true` caller).
  Keep its token-stream algorithm; replace the `Lead`/`Item` comment-attachment heuristic with the
  trivia attachment where natural. **Behaviour-neutral: existing formatter snapshots must not
  change.**
- Tests:
  - Lexer round-trip property: concat of `Leading + Raw + Trailing` over the token stream equals
    the source, run over every fixture in the test corpus.
  - Full existing suite green (unfiltered run).

## Phase 2 — node layer (token-bearing tree)

Status: **done** (arch guard test deferred — see below). Every statement, member, clause, and leaf across
all four families + the config/lock grammars is token-bearing; `Print(Parse(s)) == s` holds over the whole
corpus incl. error-recovery fixtures. `Position` is computed from the first token; `Tokens/` are public;
`NsqlSourceDocument` shares `ToSource`/`FilePath`/`EndOfFile`. Two ceilings to remember for Phase 4:
sequence/identity options, domain tail clauses, FK actions, column modifiers, and the trigger header print
as **verbatim raw spans** (structure them if the formatter needs to reflow inside) — everything list-shaped
is a typed `SeparatedSyntaxList`.

- **Foundation (done):** base printer — `NsqlNode.Children` (tokens + child nodes, source order) +
  `ToSource()`; `NsqlChild` union; `Token.WriteTo`. Leaf nodes are token-truth (`Identifier` holds
  its `Token`, `Value => Token.Text`, `Identifier.Synthetic`; `QualifiedName` gained a `Dot` token).
  `Tokens/` types are public.
- **Structural-node representation ruling (2026-07-22, settled with Tom).** The Roslyn-shaped
  end-state is *tokens as the single source of truth* — no semantic field stored beside a token; a
  fact like `IsNullable` is either computed over tokens or (if genuinely semantic) lives on the
  projected model, the binder's job, not the syntax node. Roslyn affords that with codegen +
  green/red, both of which this plan cut. So we stage: **Phase 2 attaches tokens additively to
  parsed nodes and keeps the existing semantic fields untouched** (so `NsqlWriter`/projector/
  `SyntaxBuilder` stay snapshot-neutral), and **Phase 4 collapses the duplication** — `SyntaxBuilder`
  becomes a token factory (the `SyntaxFactory` model), `NsqlWriter` converges onto the token
  printer, and the transient semantic bools go computed-or-to-the-model. The stored bool beside a
  parsed node's keyword tokens is **scaffolding with a Phase-4 demolition date**, not the design.
- **Nullable tokens (Phase 2) → non-nullable + missing sentinel (Phase 4).** A `Token?` on a node
  currently means "synthetic (no source backing)", *not* "optional in the grammar": a parsed node
  fills every slot, a `SyntaxBuilder` node fills none. That conflates two things. Phase 4 splits
  them: genuinely-optional tokens (`QualifiedName.Dot`, a `PARTIAL` keyword) stay `Token?`
  permanently; grammatically-mandatory tokens become **non-nullable with a zero-width "missing"
  token** sentinel (Roslyn's `IsMissing`) for error recovery — which the `SkippedTokensNode` step
  also leans on — once `SyntaxBuilder` synthesizes real tokens. Mandatory `Token?` today is the same
  scaffolding.
- Each node stores its tokens — including keywords and punctuation — as fields, and exposes its
  children (nodes + tokens, source order) through a virtual enumeration the printer and `Position`
  walk. Hand-written per node (~60 records in `Syntax/`); the round-trip property catches any
  omitted field. If this proves too error-prone, a source generator is the fallback — don't start
  there.
- Typed accessors survive so `DocumentProjector` barely changes: `Identifier.Value` computed from
  its token (done); `NsqlStatement.Doc` keeps its string this phase with the `DocComment` token
  added alongside (the string goes computed in Phase 4).
- Parser (`NsqlParser` + 8 partials): construction sites updated to thread tokens into nodes.
  `Expect`/`Match` return the consumed token rather than discarding it.
- Error recovery (done): `Resync()` no longer throws tokens away — following Roslyn's
  `SkippedTokensTrivia`, the discarded run becomes a `TriviaKind.Skipped` trivia on the next token
  (or EOF), so a document with errors round-trips, `Statements` stays "parsed statements only", and
  the config/lock grammars get recovery for free. Also fixes the dangling-doc-at-EOF round-trip gap.
- `SyntaxBuilder` builds synthetic tokens (empty trivia, `None` position). `NsqlWriter` output must
  be snapshot-neutral this phase.
- API surface: `Tokens/` types go public (nodes hold them; the linter seam in [[nsql-linting]]
  wants token access anyway). This is a **breaking Core change** — coordinated consumer bump, same
  dance as v5.
- **Arch guard test — DEFERRED (revisit later).** Skipped for now: a previous attempt at slice/arch
  tests "ended up a mess" (Tom, 2026-07-22). The [[language-package]] split still wants the guarantee
  that `NSchema.Project.Nsql.*` references nothing in Plan/Diff/State — revisit with a cleaner
  approach before that split is cut, not as part of this phase.
- Tests:
  - **The round-trip property**: `Print(Parse(s)) == s` over the full corpus + new error-recovery
    fixtures. Lands here and never leaves.
  - Full suite green, unfiltered.

## Phase 3 — tree formatter

Status: **step 1 done** (2026-07-22). Step 2 (structural + editorconfig rules) **deferred to its own
design session** — migrate all existing behaviour onto the CST first, then build rules on top. Depends
on phase 2.

- Formatter rewritten as a generic tree walk over one parse; existing snapshots + idempotence hold.
- `Format` now returns `Result<string, NsqlDiagnostic>` (was `string`): the value is always the
  formatted text (formatting is total over the lossless tree), syntax errors ride as `Error`
  diagnostics, and each statement a rewrite would change rides as a `Warning` — the seam `fmt --check`
  consumes (files/places that aren't canonical). Statement-level granularity for now; becomes
  rule-based in step 2.
- Made `ScriptStatement` permissive: a run condition on a change event no longer throws from the
  constructor (that dropped the node and broke lossless formatting) — the parser reports it as an
  error diagnostic and keeps the node.
- The fake `NSCHEMA` keyword is gone from the formatter tests.

- **Parsing collapsed to one reader (done, 2026-07-22).** One grammar, one reader: `NsqlReader.Read`/
  `ReadFile` parse declarations, directives, **and** config/lock blocks into one `NsqlDocument` (blocks
  are `NsqlStatement`s). Deleted: `NsqlBlockDocument`, the `ParseConfiguration`/`ParseLock` grammars,
  `ReadConfiguration`/`ReadLock`, and all file-type validation (no `Misplaced` diagnostic). "Which
  statements belong in this file" is a **consumer** decision — each subsystem picks out the statements
  it understands and ignores the rest (`ProjectAssembler` already ignored blocks; `ConfigurationAssembler`/
  `LockFileManager` now `.OfType<BlockStatement>()`). Rationale (Tom): the file split is a CLI
  file-organization choice, not a language property — Terraform-style "any file, routed by type" is the
  north star. The formatter is now a single generic tree walk over one parse (rules in one place).
  **Still TODO:** strip the fake `NSCHEMA` keyword from the formatter tests (bundled with the rewrite).

- Rewrite `NsqlFormatter` as a trivia-rewriting walk over the tree; formatting is a pure trivia
  transformation. Public `Format(string)` signature unchanged.
- Step 1: port existing behaviour — blank-line capping, comment placement, 2-space indent, doc
  comments in canonical `---` form. **Existing snapshots must match before any new rules.**
- Step 2: the ceiling-raisers — structural rules (column-list wrapping, alignment) and the
  editorconfig-backed knobs from [[nsql-linting]] (`indent_style`, `indent_size`, `end_of_line`,
  `insert_final_newline`, `nschema_` style keys). Config plumbing is [[nsql-linting]]'s resolver
  seam, not this plan's.
- Tests: snapshot every rule (Verify); idempotence property `Format(Format(s)) == Format(s)` over
  the corpus.

## Phase 4 — generation convergence

Status: **in progress**. Depends on phases 2–3.

Every NSQL-producing surface becomes: build nodes → print through the formatter.

- **Token-factory foundation (done, 2026-07-22).** The formatter prints synthetic (trivia-less) trees
  as valid NSQL — synthesized inter-token spacing keyed off `Position.None`, position-independent
  header split, `Format(NsqlDocument)` tree entry. Every node across all families (incl. templates and
  config/lock blocks) is token-complete: mandatory tokens default to their canonical synthetic form
  (`Token.Keyword`/`Punctuation`, casing from `NsqlKeywords`, punctuation from new `NsqlSymbols`),
  optional keywords synthesize at their gate (`Field ?? Token.Keyword(…)`), and variable payloads ride
  as raw spans — self-rendered from the semantic field where derivable, else built by `SyntaxBuilder`
  reusing `NsqlWriter`'s fragment renderers (extracted to `internal`, shared not duplicated;
  identifier-escaping moved onto `Identifier`). Verified by a synthesis round-trip corpus (rich schema
  through the drift comparer; directives; a block; a template). Ceiling holds: the raw spans must
  re-parse to the same semantics — the round-trip enforces it.
- **`NsqlWriter` collapsed onto the formatter (done, 2026-07-23).** Its statement `switch` and the
  statement-level renderers are gone; `Write(document)` is now `NsqlFormatter.Format(document)`, so there
  is one writing path. The shared fragment renderers stay `internal` on `NsqlWriter` (the token factory's
  raw-span source — a follow-up could relocate them to `SyntaxBuilder`). Two small formatter rules landed
  to keep output clean: token-kind spacing (tight closers/openers/dots, keyword-vs-name paren rule) and
  the "attached" grouping (grants/triggers/indexes/renames hug their subject). **New canonical:** a clause
  paren after a *name* now prints call-style (`enum('a')`, `orgs(id)`) — matching a clause paren exactly
  needs per-node context (deferred structural rules). `NsqlWriter`/formatter/template snapshots regenerated.
- **One text surface, named `NsqlWriter` (done, 2026-07-23).** Formatter + writer collapsed into a single
  `NsqlWriter` (the counterpart to `NsqlReader`). Verb by input: `Write(NsqlDocument|Database[, directives])`
  → `string` serializes a structure; `Format(string)` → `Result` reformats source (the only one that parses,
  hence the `Result`). `SyntaxBuilder` owns model→tree and the raw-span fragment renderers (moved there);
  `NsqlWriter` owns →text. `ImportOperation`'s accidental double-format collapsed to a single `Write(document)`.
- **Producers on the CST (done, 2026-07-23).** `LockFileManager.Write` builds `LOCK` `BlockStatement`s and
  renders them through `NsqlWriter.Write` — no more hand-built `LOCK ( … );` strings. Lockfiles are now
  multi-line canonical blocks (the block-broken form); machine-managed, still round-trips. `INSchemaPlugin`.
  `GetScaffoldTemplate` returns a `BlockStatement` instead of a string (breaking plugin-API change, no impl
  yet). Comments are not lost: a block-level `---` rides as the block's doc-comment, and inline `--` comments
  ride as `LineComment` trivia on a token (`Trivia`/`Token.Leading`/`Trailing` are public) — the formatter
  renders both.
- **Nullable-token split (done, 2026-07-23).** A `Token?` now means "grammatically optional" only. Every
  grammatically-mandatory slot is non-nullable `Token`: those derivable from a semantic field default to it
  (`CheckDefinition.ExpressionToken = Token.Span(Expression.Value)`, `BlockAttribute` key/value, routine
  kind/args/definition, view body, deployment phase, exclusion `WITH op`, block keyword) — dropping the
  `?? synth` from their `Children`; the rest (script/trigger bodies, trigger header, execute-function action,
  sequence interior) default to a new zero-width `Token.Missing` sentinel (`TokenKind.Missing`, `IsMissing`)
  that the parser or `SyntaxBuilder` always fills. The sentinel is also the seat for future error recovery
  (a mandatory token left missing instead of throwing). Genuinely-optional tokens stay `Token?`.
- **Still to do:** the deferred structural rules (which would let clause parens after a name take their space
  back).

- `SyntaxBuilder` graduates to the public syntax-factory surface (naming TBD when it lands).
- `NsqlWriter`: already model→nodes→text; swap its printer for the formatter. Import output
  (`ImportOperation`, which today does `NsqlFormatter.Format(NsqlWriter.Write(…))`) collapses to
  one pipeline and picks up user style for free.
- `LockFileManager` (`Configuration/Plugins/`): stops hand-building `LOCK ( … );` strings — builds
  block-statement nodes instead.
- Plugin scaffolding: `INSchemaPlugin.GetScaffoldTemplate` returns typed nodes instead of a
  string. **Breaking plugin-API change** — should ride the same major as phase 2's break so
  consumers migrate once (see open decisions).
- `SqlDialect` is out of scope: it produces executable provider SQL, not NSQL.
- Tests: snapshot each migrated producer; import/lockfile output should be byte-identical or the
  diff explicitly reviewed.

## Open decisions

- **Release train:** one major covering phases 2 and 4's breaks (consumers migrate once), or let
  the scaffold-surface break trail? Leaning single train; decide before phase 2 ships.
- **Package split** (`NSchema.Language` vs staying in Core) stays governed by [[language-package]]:
  don't cut until a consumer is real. Nothing in this plan blocks on it.
- **Kind-qualified addresses** — deferred with scope parsing.

## Conventions that bind (reminders for a cold session)

- Never commit; Tom stages and commits.
- Public-facing changes go in CHANGELOG.md (relative to the previous *released* version).
- A phase is only "green" on a full **unfiltered** `dotnet test` run, never a `--filter` subset.
- Snapshot-test every output surface (Verify); AAA comments; Shouldly + NSubstitute; xUnit v3.
