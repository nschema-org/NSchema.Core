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

Status: **in progress**. The big mechanical phase; the public-API break.

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
- Error recovery: `Resync()` currently throws tokens away. Add a skipped-tokens node so documents
  with syntax errors still round-trip — this is what makes lint/format viable on broken files.
- `SyntaxBuilder` builds synthetic tokens (empty trivia, `None` position). `NsqlWriter` output must
  be snapshot-neutral this phase.
- API surface: `Tokens/` types go public (nodes hold them; the linter seam in [[nsql-linting]]
  wants token access anyway). This is a **breaking Core change** — coordinated consumer bump, same
  dance as v5.
- New arch guard test: the language layer (`NSchema.Project.Nsql.*`) references nothing in
  Plan/Diff/State — keeps the [[language-package]] split a `git mv`. (No slice tests exist today;
  this is the first.)
- Tests:
  - **The round-trip property**: `Print(Parse(s)) == s` over the full corpus + new error-recovery
    fixtures. Lands here and never leaves.
  - Full suite green, unfiltered.

## Phase 3 — tree formatter

Status: **not started**. Depends on phase 2.

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

Status: **not started**. Depends on phases 2–3.

Every NSQL-producing surface becomes: build nodes → print through the formatter.

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
