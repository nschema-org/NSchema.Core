# NSchema DDL grammar

NSchema DDL borrows SQL's `CREATE TABLE` shape and column grammar so it reads instantly to anyone who works with
databases, but it is its own bounded, normalized language — **not** a SQL dialect. It describes *desired state*:
you write the final shape of the schema, never `ALTER`/migration steps. Every construct maps 1:1 onto the
`DatabaseSchema` domain model, so the parser is a thin front-end over that model.

It is dialect-agnostic by construction: dialect-specific spelling (type names like `serial`, expression
functions like `now()`) is an *output* concern owned by the `ISqlGenerator`, never the input grammar. The two
places raw, possibly dialect-flavored SQL is accepted — `DEFAULT`, `CHECK (…)`, and index `WHERE` expressions —
are stored verbatim and passed through untouched.

## Design decisions

These were settled deliberately; the rationale matters for anyone extending the grammar.

1. **Declarative, not imperative.** The grammar has no `ALTER`/migration-step productions. A parse of `ALTER …`
   is a clean error directing the author to express the final state. This is why the SQL-familiarity is a help,
   not the "which statements are allowed?" trap of accepting arbitrary SQL.
2. **Canonical types, dialect output.** Input types map to `SqlType`; unknown type names become
   `SqlType.Custom(raw)`. Dialect translation happens only in the generator.
3. **Fixed column-modifier order.** Modifiers appear in one canonical order (below). Order-flexibility is parser
   cost with no authoring benefit for a generated/canonical format.
4. **Constraint names are always required.** Every constraint is written `CONSTRAINT <name> …`. The name is the
   comparer's match key (its diff identity); anonymous constraints can't diff stably, so they are not allowed.
5. **Indexes are inline** in the table body (cohesion: the whole table's desired state in one block), rather than
   external `CREATE INDEX` statements.
6. **Grants are statements**, not table-body items — they're cross-cutting (one role across many objects), which
   matches `GRANT` in real SQL.
7. **Comments are doc-comments, not a clause.** See [Comments](#comments).

## Lexical

```ebnf
(* ignored — ordinary source comments and whitespace *)
line-comment   = "--" , { any-char - newline } ;
block-comment  = "/*" , { any-char } , "*/" ;

(* captured — doc-comments, attached to the following declaration (see Comments) *)
doc-line       = "---" , { any-char - newline } ;
doc-block      = "/**" , { any-char } , "*/" ;

ident          = ( letter | "_" ) , { letter | digit | "_" } ;
qualified-name = ident , "." , ident ;            (* schema.table, or schema.table for FK references *)
string         = "'" , { any-char - "'" | "''" } , "'" ;   (* '' escapes a single quote *)
integer        = digit , { digit } ;
```

### Expressions (the one hard token)

`DEFAULT`, `CHECK (…)`, and index `WHERE` hold arbitrary SQL the model stores as an opaque string.

```ebnf
paren-expr     = "(" , balanced-tokens , ")" ;     (* CHECK (…), WHERE (…): capture balanced parens *)
default-expr   = token-run-until( top-level "," | top-level ")" | "COMMENT" | "RENAMED" ) ;
```

An unparenthesised `DEFAULT` expression runs until a `,` or `)` **at the enclosing list's paren depth**, or a
reserved column-modifier keyword. So `DEFAULT now()` and `DEFAULT coalesce(a, b)` work (their inner commas are at
depth ≥ 1). The canonical **writer** always parenthesises non-trivial defaults to stay safely inside this rule.

## Comments

There are two kinds of comment, distinguished lexically — the same `//` vs `///` mental model as Rust/JSDoc/C# XML
docs:

| Syntax          | Meaning                                                                                                                                                                      |
|-----------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `--`, `/* */`   | **Source comment.** A note for whoever reads the file. Stripped; never persisted.                                                                                            |
| `---`, `/** */` | **Doc-comment.** Attaches to the *immediately following declaration* and becomes that object's **catalog comment** (`COMMENT ON …`), flowing to the model's `Comment` field. |

A doc-comment may precede any commentable declaration: a `CREATE SCHEMA`, a `CREATE TABLE`, a column, or a
constraint. There is **no** `COMMENT '…'` clause — doc-comments are the single mechanism, which keeps the grammar
clean and puts documentation inline next to the thing it documents.

> Note: a `---` doc-comment is not just a note — it emits a `COMMENT ON …` in the migration. Use `--` for notes
> you don't want persisted to the database catalog.

```sql
-- internal: revisit index strategy           (stripped)
--- All registered users.                       (becomes the table's catalog comment)
CREATE TABLE app.users
(
    --- Primary contact; verified at signup.      (becomes the column's catalog comment)
    email text NOT NULL,
    --- Enforced at the app tier too.             (becomes the constraint's catalog comment)
    CONSTRAINT users_age_chk CHECK (age >= 0)
);
```

## Document and statements

```ebnf
document   = { [ doc-comment ] , ( statement | config-block | deployment-script ) } ;
statement  = ( create-schema | create-table | create-view | create-enum | create-sequence
             | create-function | create-procedure | create-extension | create-trigger | create-index
             | drop-schema | drop-table | drop-view | drop-enum | drop-sequence
             | drop-function | drop-procedure | drop-extension | grant ) , ";" ;
```

A flat statement list; schema membership is by qualified name, exactly like SQL — no nesting.

A document may also contain top-level **configuration blocks** (`config-block`) — see
[Configuration blocks](#configuration-blocks) — and **deployment scripts** (`deployment-script`) — see
[Deployment scripts](#deployment-scripts). The schema parser **ignores** both; neither is part of the
desired-state model.

## Configuration blocks

Following Terraform's model, orchestration configuration (the state backend, the live-database provider, project
settings) may live in the DDL alongside the schema. Unlike Terraform's HCL, the blocks are **SQL-statement
shaped** — keeping one consistent grammar in the file — mirroring Postgres storage parameters
(`CREATE TABLE … WITH (option = value, …)`):

```ebnf
config-block   = ident , [ ident ] , "(" , [ config-attr , { "," , config-attr } ] , ")" , ";" ;
config-attr    = config-key , "=" , config-value ;
config-key     = ident , { "." , ident } ;
config-value   = string | [ "-" ] , integer | "true" | "false" | ident ;
```

```sql
NSCHEMA (
  dialect = 'postgres',
  transaction_mode = 'single'
);

BACKEND file (
  path = 'state/app.nsstate'
);

PROVIDER postgres (
  schema_search_path = 'app',
  connection_timeout = 1000
);
```

Notes on the shape:

- The block keyword (`NSCHEMA` / `BACKEND` / `PROVIDER`) and the optional label (`file`, `postgres`) are **bare
  identifiers** — consistent with bare identifiers everywhere else in the DDL; double quotes are still unused.
  `NSCHEMA` has no label.
- String **values** are single-quoted (`'postgres'`), SQL-style. Values may also be integers (optionally
  negative), `true`/`false`, or a bare identifier (`transaction_mode = single`).
- Attributes are a **flat** comma-separated list — no nested blocks. Group related settings with a dotted key
  (`pool.max = 10`), which is captured verbatim as a single key.

This is deliberately distinct from the schema statements, and three rules keep it tractable:

1. **The schema parser ignores them.** `DdlReader` produces both from one read: `DdlReader.Instance.Read(source)`
   returns a `DdlDocument` whose `Schema` holds the schema statements and whose `Config` holds the config blocks
   for a front-end to read. The core captures config blocks into a generic `ConfigBlock` model but never
   *interprets* them — interpretation (mapping a block to provider/state/dialect registration) lives in the
   front-end. This is how one file can hold both, à la Terraform's intermixed `terraform` / `provider` /
   `resource` blocks.
2. **Config blocks are static — no interpolation.** They must be resolvable in a lightweight bootstrap pass
   *before* the core is configured (the analogue of `terraform init`), so they cannot reference variables or
   computed values. This is also why Terraform forbids interpolation in its `backend` block.
3. **No secrets.** Connection strings and credentials stay in environment variables / CLI overlay, never in a
   committed config block. Only stable, non-secret settings (backend *type* and path, dialect, search paths)
   belong here. Configuration precedence remains CLI args > environment > config block > defaults.

The matching forward-compatibility rule for the parser: **an unrecognised top-level block keyword is captured, not
an error** — so a config type a front-end adds later (e.g. `WORKSPACE`) parses today, and adding its semantics
touches only the front-end, never the core.

## Deployment scripts

Some migration steps are imperative and can't be expressed declaratively — backfills, `CREATE EXTENSION`, data
fixes, `CREATE INDEX CONCURRENTLY`. These are declared inline as **deployment scripts** that run as raw SQL around
the computed migration: every `PRE DEPLOYMENT` body runs before the migration's statements, every `POST DEPLOYMENT`
body after.

```ebnf
deployment-script = ( "PRE" | "POST" ) , "DEPLOYMENT" , string ,
                    [ "(" , [ script-option , { "," , script-option } ] , ")" ] ,
                    "AS" , dollar-body , ";" ;
script-option     = ident , "=" , config-value ;
dollar-body       = "$$" , … , "$$" | "$" , tag , "$" , … , "$" , tag , "$" ;
```

```sql
PRE DEPLOYMENT 'enable_citext' AS $$
    CREATE EXTENSION IF NOT EXISTS citext;
$$;

POST DEPLOYMENT 'reindex' (run_outside_transaction = true) AS $$
    CREATE INDEX CONCURRENTLY idx_widgets_name ON app.widgets (name);
$$;
```

Notes on the shape:

- The **name** is a single-quoted string, used in plan output and logs.
- An optional `( … )` clause carries script options. The only option today is
  `run_outside_transaction = true`, for statements the database forbids inside a transaction (e.g.
  `CREATE INDEX CONCURRENTLY`). An unknown option is an error.
- `AS` introduces the body, exactly as for a view (`CREATE VIEW … AS …`).
- The **body** is a dollar-quoted block (`$$ … $$` or `$tag$ … $tag$`) — the same opaque-SQL device used for
  function bodies. Dollar-quoting lets the body contain its own `;` and single quotes without escaping; the inner
  content is taken verbatim (delimiters stripped, surrounding whitespace trimmed) and is **not** dialect-translated.

Like configuration blocks, deployment scripts are **not part of the desired-state schema**: the same
`DdlReader.Instance.Read(source)` returns them on `DdlDocument.Scripts` (alongside `Schema` and `Config`). They are
plan orchestration, so they live on the migration plan rather than in the diff. The schema-only writer
(`DdlWriter.Instance.Write(DatabaseSchema)`) does not emit them; the full-document writer
(`Write(DdlDocument)`) does — it round-trips config blocks, schema, and scripts losslessly (config first, then
schema, then scripts), which is what `fmt` uses to reformat a file in place.

### Schemas

```ebnf
create-schema = "CREATE" , [ "PARTIAL" ] , "SCHEMA" , ident , [ "RENAMED" , "FROM" , ident ] ;
drop-schema   = "DROP" , "SCHEMA" , ident ;                (* -> DroppedSchemas *)
```

`PARTIAL` sets `SchemaDefinition.IsPartial` (tables not listed are left alone rather than dropped). `RENAMED FROM`
sets `OldName`.

### Tables

```ebnf
create-table = "CREATE" , "TABLE" , qualified-name , [ "RENAMED" , "FROM" , ident ] ,
               "(" , table-item , { "," , table-item } , ")" ;
drop-table   = "DROP" , "TABLE" , qualified-name ;         (* -> DroppedTables (explicit drop, partial schema) *)

table-item   = [ doc-comment ] , ( column-def | pk-def | fk-def | unique-def | check-def | index-def ) ;
```

### Columns

```ebnf
column-def   = ident , type ,
               [ "NOT" , "NULL" | "NULL" ] ,
               [ "IDENTITY" , [ "(" , identity-opt , { "," , identity-opt } , ")" ] ] ,
               [ "DEFAULT" , ( paren-expr | default-expr ) ] ,
               [ "RENAMED" , "FROM" , ident ] ;

identity-opt = ( "START" | "INCREMENT" | "MINVALUE" ) , integer ;
type         = ident , [ "(" , integer , [ "," , integer ] , ")" ] ;
```

Absence of `NOT NULL` means nullable (SQL default). `type` maps to `SqlType`: known names (`int`, `bigint`,
`text`, `boolean`, …), parametrised `varchar(n)` / `char(n)` / `decimal(p,s)`, and any unknown name →
`SqlType.Custom(raw)`. Common SQL spelling aliases normalize to the canonical name so a SQL-flavoured schema
round-trips against introspection — e.g. `integer`→`int`, `bool`→`boolean`, `real`→`float`, `numeric(p,s)`→
`decimal(p,s)`, `timestamp`→`datetime`, `timestamptz`→`datetimeoffset`, `uuid`→`guid`, `bytea`→`varbinary`
(plus the Postgres `int2`/`int4`/`int8`/`float4`/`float8` spellings). The modifier order above is fixed.

### Constraints

Names are mandatory; structural changes drop-and-recreate, but a doc-comment change alone is applied in place
(`COMMENT ON CONSTRAINT`), never a recreate.

```ebnf
pk-def     = "CONSTRAINT" , ident , "PRIMARY" , "KEY" , "(" , col-list , ")" ;
fk-def     = "CONSTRAINT" , ident , "FOREIGN" , "KEY" , "(" , col-list , ")" ,
             "REFERENCES" , qualified-name , "(" , col-list , ")" ,
             [ "ON" , "DELETE" , ref-action ] , [ "ON" , "UPDATE" , ref-action ] ;
unique-def = "CONSTRAINT" , ident , "UNIQUE" , "(" , col-list , ")" ;
check-def  = "CONSTRAINT" , ident , "CHECK" , paren-expr ;

ref-action = "NO" , "ACTION" | "CASCADE" | "SET" , "NULL" | "SET" , "DEFAULT" ;
col-list   = ident , { "," , ident } ;
```

### Indexes (inline)

```ebnf
index-def  = [ "UNIQUE" ] , "INDEX" , ident , "(" , col-list , ")" , [ "WHERE" , paren-expr ] ;
```

`UNIQUE (…)` (a `unique-def`) is a **unique constraint**; `UNIQUE INDEX` is a **unique index** — SQL's own
distinction, mapping to the two distinct model concepts (`UniqueConstraint` vs `TableIndex { IsUnique }`). A
unique index can be partial (`WHERE`); a unique constraint cannot.

### Grants

```ebnf
grant      = "GRANT" , ( table-priv , { "," , table-priv } , "ON" , qualified-name
                       | "USAGE" , "ON" , "SCHEMA" , ident ) ,
             "TO" , ident ;
table-priv = "SELECT" | "INSERT" | "UPDATE" | "DELETE" ;
```

`GRANT … ON <table>` → `TableGrant`; `GRANT USAGE ON SCHEMA <schema>` → `SchemaGrant`.

### Views

```ebnf
create-view = "CREATE" , [ "MATERIALIZED" ] , "VIEW" , qualified-name , [ "RENAMED" , "FROM" , ident ] ,
              "AS" , view-body ;                            (* view-body: opaque text up to the top-level ';' *)
drop-view   = "DROP" , [ "MATERIALIZED" ] , "VIEW" , qualified-name ; (* -> DroppedViews (explicit drop, partial schema) *)
create-index = "CREATE" , [ "UNIQUE" ] , "INDEX" , ident , "ON" , qualified-name ,
               "(" , ident , { "," , ident } , ")" , [ "WHERE" , "(" , expr , ")" ] ;
```

The `view-body` is everything after `AS` up to the terminating top-level `;` — captured **verbatim** and never
interpreted, exactly like a `CHECK` expression. Parentheses are balanced and string literals/comments are skipped,
so a `;` inside them does not end the definition.

NSchema does scan the body for the objects the view reads — the targets of its `FROM` and `JOIN` clauses, at any
nesting depth, minus names bound by a `WITH` CTE — and records them as `View.DependsOn`. These drive ordering: a
view is **created after** the tables and views it reads and **dropped before** them, with views ordered amongst
themselves by their dependency graph (a cycle is rejected). The scan is deliberately shallow; it over-collects
rather than under-collects, since a reference that names no planned object simply produces no ordering edge.

A **materialized** view (`CREATE MATERIALIZED VIEW`) stores its result set and is the same model type as a plain
view, distinguished by a flag (matching `pg_class`'s `relkind`). Because there is no `CREATE OR REPLACE
MATERIALIZED VIEW`, a body change to a materialized view — or converting a view to/from materialized — is planned
as a **drop + recreate**, whereas a plain view's body change is an in-place `CREATE OR REPLACE`.

Only a materialized view may carry **indexes**, declared as standalone `CREATE [UNIQUE] INDEX … ON s.v`
statements (a plain view or a table cannot — table indexes are inline). Like a `GRANT`, an index names its
materialized view via `ON` and is attached to it when the document is built (targeting an unknown or
non-materialized relation is an error). There is no `DROP INDEX`: an index absent from a materialized view's
declaration is dropped, mirroring inline table indexes.

```sql
CREATE MATERIALIZED VIEW app.daily_totals AS SELECT date, sum(amount) FROM app.sales GROUP BY date;
CREATE UNIQUE INDEX daily_totals_date_ix ON app.daily_totals (date);
```

### Enums

```ebnf
create-enum = "CREATE" , "ENUM" , qualified-name , [ "RENAMED" , "FROM" , ident ] ,
              "(" , [ string , { "," , string } ] , ")" ;
drop-enum   = "DROP" , "ENUM" , qualified-name ;            (* -> DroppedEnums (explicit drop, partial schema) *)
```

```sql
CREATE ENUM app.order_status ('pending', 'shipped', 'delivered');
```

The values are an **ordered** list (the order is the type's comparison order, as in Postgres) and must be unique
within the enum. A column uses the enum by naming it as its type (`status order_status`). Enum evolution is
additions-only: new values may be inserted anywhere, but removing or reordering existing values cannot be planned —
it requires manually recreating the type.

### Sequences

```ebnf
create-sequence = "CREATE" , "SEQUENCE" , qualified-name , [ "RENAMED" , "FROM" , ident ] ,
                  [ "(" , seq-option , { "," , seq-option } , ")" ] ;
seq-option      = "AS" , ident
                | ( "START" | "INCREMENT" | "MINVALUE" | "MAXVALUE" | "CACHE" ) , [ "-" ] , integer
                | "CYCLE" ;
drop-sequence   = "DROP" , "SEQUENCE" , qualified-name ;    (* -> DroppedSequences (explicit drop, partial schema) *)
```

```sql
CREATE SEQUENCE app.order_id (AS bigint, START 100, INCREMENT 5, MAXVALUE 999999, CACHE 10, CYCLE);
```

The option style mirrors a column's `IDENTITY (…)` clause. An omitted option means the database provider's
default applies. Each option may appear at most once.

### Extensions

```ebnf
create-extension = "CREATE" , "EXTENSION" , ext-name , [ "VERSION" , string ] ;
drop-extension   = "DROP" , "EXTENSION" , ext-name ;       (* -> DroppedExtensions (explicit drop only) *)
ext-name         = ident | string ;
```

```sql
CREATE EXTENSION citext;
CREATE EXTENSION postgis VERSION '3.4';
CREATE EXTENSION 'uuid-ossp';
```

Extensions are **database-global**, not schema-scoped: an extension is named once per database, so it is declared
at the top level (not inside a `CREATE SCHEMA`) and is never qualified by a schema. The name may be a quoted
string when it is not a bare identifier (e.g. `'uuid-ossp'`). `VERSION` is optional; when omitted, whatever
version the provider installs is accepted, and the version is never compared (so an omitted version cannot show
as drift). A version change plans an update in place.

Unlike every other object, an extension that exists in the database but is absent from the desired schema is
**left alone** — it is removed only by an explicit `DROP EXTENSION`. Extensions are shared infrastructure (a
database always has some installed by default), so absence must never imply a drop.

### Functions and procedures

```ebnf
create-function  = "CREATE" , "FUNCTION" , qualified-name , [ "RENAMED" , "FROM" , ident ] ,
                   "(" , [ arg-text ] , ")" , definition-text ;
create-procedure = "CREATE" , "PROCEDURE" , qualified-name , [ "RENAMED" , "FROM" , ident ] ,
                   "(" , [ arg-text ] , ")" , definition-text ;
drop-function    = "DROP" , "FUNCTION" , qualified-name ;   (* -> DroppedFunctions (explicit drop, partial schema) *)
drop-procedure   = "DROP" , "PROCEDURE" , qualified-name ;  (* -> DroppedProcedures (explicit drop, partial schema) *)
```

```sql
CREATE FUNCTION app.add_tax(amount numeric, rate numeric) RETURNS numeric LANGUAGE sql AS $$
  SELECT amount * (1 + rate);
$$;
```

Both captures are **opaque**: `arg-text` is the verbatim text inside the parentheses (quote- and paren-aware,
but **not** dollar-quote aware), and `definition-text` is everything after the closing parenthesis up to the
top-level `;` — **dollar-quote aware**, so a `;` inside `$$ … $$` (or a tagged `$body$ … $body$`) does not end
the statement. A procedure is identical except its definition has no `RETURNS` clause.

Two rules carry over from the database:

1. **No overloading** — one routine per name. Declaring the same name twice is an error.
2. **Functions and procedures share one name space** within a schema (they live in the same catalog), so a
   function and a procedure with the same name is an error.

The argument list is part of the routine's identity: changing it plans a **drop + recreate** (replacing in
place under a different signature would create a separate overload in the database). A definition-only change
replaces in place, like a view body change.

### Triggers

```ebnf
create-trigger = "CREATE" , "TRIGGER" , ident , timing , events , "ON" , qualified-name ,
                 [ "FOR" , "EACH" , ( "ROW" | "STATEMENT" ) ] , [ "WHEN" , "(" , expr , ")" ] ,
                 "EXECUTE" , ( "FUNCTION" | "PROCEDURE" ) , func-name , "(" , [ arg-text ] , ")" ;
timing         = "BEFORE" | "AFTER" | "INSTEAD" , "OF" ;
events         = event , { "OR" , event } ;
event          = "INSERT" | "DELETE" | "TRUNCATE" | "UPDATE" , [ "OF" , "(" , ident , { "," , ident } , ")" ] ;
func-name      = ident , [ "." , ident ] ;
```

```sql
CREATE TRIGGER users_audit
  AFTER INSERT OR UPDATE OF (email)
  ON app.users
  FOR EACH ROW
  WHEN (new.email IS NOT NULL)
  EXECUTE FUNCTION app.log_change();
```

A trigger is **table-scoped** but written as a standalone statement that names its table via `ON` — like a
`GRANT`, it is attached to that table when the document is built (referencing an undeclared table is an error).
The function the trigger executes must exist; the planner creates a trigger after both its table and the
function it calls, and drops it before either. `FOR EACH` defaults to `STATEMENT` when omitted. The `WHEN`
condition and the function `arg-text` are captured **opaque** (verbatim), like a `CHECK` expression.

Triggers are **table members** (named uniquely per table), so — like indexes and constraints — they are not
renameable and have no separate `DROP TRIGGER`: a trigger absent from a declared table's set is dropped, and a
structural change is planned as a drop + recreate (only a comment-only change is applied in place).

## Construct → model mapping

| DDL construct                              | Model target                                                       |
|--------------------------------------------|--------------------------------------------------------------------|
| `CREATE [PARTIAL] SCHEMA s`                | `SchemaDefinition` (`IsPartial`)                                   |
| `CREATE TABLE s.t (…)`                     | `SchemaDefinition` + `Table`                                       |
| `CREATE VIEW s.v AS …`                     | `SchemaDefinition` + `View` (`Body` opaque, `DependsOn` derived)   |
| `RENAMED FROM x` (schema/table/column)     | `OldName`                                                          |
| `name type [NOT NULL] [DEFAULT e]`         | `Column` (`Type`→`SqlType`, `IsNullable`, `DefaultExpression`)     |
| `IDENTITY (…)`                             | `Column.IsIdentity` + `IdentityOptions`                            |
| `CONSTRAINT n PRIMARY KEY (…)`             | `Table.PrimaryKey` (`PrimaryKey`)                                  |
| `CONSTRAINT n FOREIGN KEY … REFERENCES …`  | `ForeignKey` (`OnDelete`/`OnUpdate`→`ReferentialAction`)           |
| `CONSTRAINT n UNIQUE (…)`                  | `UniqueConstraint`                                                 |
| `CONSTRAINT n CHECK (e)`                   | `CheckConstraint` (`Expression` = `e`, opaque)                     |
| `[UNIQUE] INDEX n (…) [WHERE e]`           | `TableIndex` (`IsUnique`, `Predicate`)                             |
| `GRANT … ON s.t TO r`                      | `TableGrant`                                                       |
| `GRANT USAGE ON SCHEMA s TO r`             | `SchemaGrant`                                                      |
| `DROP TABLE s.t` / `DROP SCHEMA s`         | `DroppedTables` / `DroppedSchemas`                                 |
| `DROP VIEW s.v`                            | `DroppedViews`                                                     |
| `CREATE MATERIALIZED VIEW s.v AS …`        | `View` with `IsMaterialized = true`                                |
| `CREATE [UNIQUE] INDEX n ON s.v (…)`       | `TableIndex` on the materialized view `s.v` (`View.Indexes`)       |
| `CREATE ENUM s.e ('a', 'b')`               | `SchemaDefinition` + `EnumType` (ordered `Values`)                 |
| `CREATE SEQUENCE s.q (…)`                  | `SchemaDefinition` + `Sequence` (`SequenceOptions`)                |
| `DROP ENUM s.e` / `DROP SEQUENCE s.q`      | `DroppedEnums` / `DroppedSequences`                                |
| `CREATE FUNCTION s.f(…) …`                 | `SchemaDefinition` + `Function` (`Arguments`/`Definition` opaque)  |
| `CREATE PROCEDURE s.p(…) …`                | `SchemaDefinition` + `Procedure` (`Arguments`/`Definition` opaque) |
| `DROP FUNCTION s.f` / `DROP PROCEDURE s.p` | `DroppedFunctions` / `DroppedProcedures`                           |
| `CREATE EXTENSION e [VERSION 'v']`         | `DatabaseSchema` + `Extension` (root-level, `Version` optional)    |
| `DROP EXTENSION e`                         | `DroppedExtensions` (root-level; explicit drop only)               |
| `CREATE TRIGGER t … ON s.tbl …`            | `Trigger` on the named table (`Table.Triggers`; no drop statement) |
| `---` / `/** */` before a declaration      | that object's `Comment`                                            |

## Worked example

```sql
--- Storefront schema.
CREATE SCHEMA shop;

--- Line items for an order.
CREATE TABLE shop.order_items RENAMED FROM line_items
(
    order_id
    int
    NOT
    NULL,
    product_id
    int
    NOT
    NULL,
    quantity
    int
    NOT
    NULL
    DEFAULT
    1,
    unit_price
    numeric
(
    12,
    2
) NOT NULL,
    --- Free-text note; was previously called "comment".
    note text RENAMED FROM comment,
    CONSTRAINT order_items_pkey PRIMARY KEY
(
    order_id,
    product_id
),
    CONSTRAINT fk_order_items_order
    FOREIGN KEY
(
    order_id
) REFERENCES shop.orders
(
    id
) ON DELETE CASCADE,
    CONSTRAINT fk_order_items_product
    FOREIGN KEY
(
    product_id
) REFERENCES shop.products
(
    id
)
  ON DELETE RESTRICT,

    --- Quantity must be positive.
    CONSTRAINT chk_order_items_qty CHECK
(
    quantity >
    0
),
    INDEX ix_order_items_product
(
    product_id
),
    INDEX ix_order_items_flagged
(
    order_id
) WHERE
(
    note
    IS
    NOT
    NULL
)
    );

GRANT SELECT, INSERT, UPDATE ON shop.order_items TO app_rw;
GRANT SELECT ON shop.order_items TO app_ro;
```
