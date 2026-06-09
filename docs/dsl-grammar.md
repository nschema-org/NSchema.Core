# NSchema DDL grammar

> **Status: design specification.** This describes the canonical NSchema DDL — a declarative,
> dialect-agnostic schema language that the (not-yet-built) `DdlSchemaSerializer` will parse and emit. It is the
> reference the parser is implemented against, not yet a shipped feature.

NSchema DDL borrows SQL's `CREATE TABLE` shape and column grammar so it reads instantly to anyone who works with
databases, but it is its own bounded, normalized language — **not** a SQL dialect. It describes *desired state*:
you write the final shape of the schema, never `ALTER`/migration steps. Every construct maps 1:1 onto the
`DatabaseSchema` domain model, so the parser is a thin front-end over the same model the fluent API produces.

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

| Syntax        | Meaning                                                                            |
|---------------|------------------------------------------------------------------------------------|
| `--`, `/* */` | **Source comment.** A note for whoever reads the file. Stripped; never persisted.  |
| `---`, `/** */` | **Doc-comment.** Attaches to the *immediately following declaration* and becomes that object's **catalog comment** (`COMMENT ON …`), flowing to the model's `Comment` field. |

A doc-comment may precede any commentable declaration: a `CREATE SCHEMA`, a `CREATE TABLE`, a column, or a
constraint. There is **no** `COMMENT '…'` clause — doc-comments are the single mechanism, which keeps the grammar
clean and puts documentation inline next to the thing it documents.

> Note: a `---` doc-comment is not just a note — it emits a `COMMENT ON …` in the migration. Use `--` for notes
> you don't want persisted to the database catalog.

```sql
-- internal: revisit index strategy           (stripped)
--- All registered users.                       (becomes the table's catalog comment)
CREATE TABLE app.users (
  --- Primary contact; verified at signup.      (becomes the column's catalog comment)
  email text NOT NULL,
  --- Enforced at the app tier too.             (becomes the constraint's catalog comment)
  CONSTRAINT users_age_chk CHECK (age >= 0)
);
```

## Document and statements

```ebnf
document   = { [ doc-comment ] , statement } ;
statement  = ( create-schema | create-table | drop-schema | drop-table | grant ) , ";" ;
```

A flat statement list; schema membership is by qualified name, exactly like SQL — no nesting.

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
`SqlType.Custom(raw)`. The modifier order above is fixed.

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

ref-action = "NO" , "ACTION" | "CASCADE" | "SET" , "NULL" | "RESTRICT" | "SET" , "DEFAULT" ;
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

## Construct → model mapping

| DDL construct                          | Model target                                              |
|----------------------------------------|-----------------------------------------------------------|
| `CREATE [PARTIAL] SCHEMA s`            | `SchemaDefinition` (`IsPartial`)                          |
| `CREATE TABLE s.t (…)`                 | `SchemaDefinition` + `Table`                              |
| `RENAMED FROM x` (schema/table/column) | `OldName`                                                 |
| `name type [NOT NULL] [DEFAULT e]`     | `Column` (`Type`→`SqlType`, `IsNullable`, `DefaultExpression`) |
| `IDENTITY (…)`                         | `Column.IsIdentity` + `IdentityOptions`                  |
| `CONSTRAINT n PRIMARY KEY (…)`         | `Table.PrimaryKey` (`PrimaryKey`)                        |
| `CONSTRAINT n FOREIGN KEY … REFERENCES …` | `ForeignKey` (`OnDelete`/`OnUpdate`→`ReferentialAction`) |
| `CONSTRAINT n UNIQUE (…)`              | `UniqueConstraint`                                        |
| `CONSTRAINT n CHECK (e)`               | `CheckConstraint` (`Expression` = `e`, opaque)           |
| `[UNIQUE] INDEX n (…) [WHERE e]`       | `TableIndex` (`IsUnique`, `Predicate`)                   |
| `GRANT … ON s.t TO r`                  | `TableGrant`                                              |
| `GRANT USAGE ON SCHEMA s TO r`         | `SchemaGrant`                                             |
| `DROP TABLE s.t` / `DROP SCHEMA s`     | `DroppedTables` / `DroppedSchemas`                       |
| `---` / `/** */` before a declaration  | that object's `Comment`                                  |

## Worked example

```sql
--- Storefront schema.
CREATE SCHEMA shop;

--- Line items for an order.
CREATE TABLE shop.order_items RENAMED FROM shop.line_items (
  order_id    int           NOT NULL,
  product_id  int           NOT NULL,
  quantity    int           NOT NULL DEFAULT 1,
  unit_price  numeric(12,2) NOT NULL,
  --- Free-text note; was previously called "comment".
  note        text          RENAMED FROM comment,

  CONSTRAINT order_items_pkey PRIMARY KEY (order_id, product_id),

  CONSTRAINT fk_order_items_order
    FOREIGN KEY (order_id)   REFERENCES shop.orders (id)   ON DELETE CASCADE,
  CONSTRAINT fk_order_items_product
    FOREIGN KEY (product_id) REFERENCES shop.products (id) ON DELETE RESTRICT,

  --- Quantity must be positive.
  CONSTRAINT chk_order_items_qty CHECK (quantity > 0),

  INDEX ix_order_items_product (product_id),
  INDEX ix_order_items_flagged (order_id) WHERE (note IS NOT NULL)
);

GRANT SELECT, INSERT, UPDATE ON shop.order_items TO app_rw;
GRANT SELECT                 ON shop.order_items TO app_ro;
```
