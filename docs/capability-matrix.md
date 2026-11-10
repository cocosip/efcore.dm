# Capability Matrix

Status: **living document**. Updated in the same commit as the code it describes.

## 1. Purpose

This document answers one question at any point in time: **does the provider support capability X today, and what is the evidence?**

Two things make it necessary:

1. The reference implementation is not a specification. It was measured at 114 source files with a self-authored test suite and no EF Core specification test project. Whatever it implemented is evidence of what one team needed, not of what the EF Core provider contract requires.
2. The roadmap states phases, not coverage. A phase can be "complete" while capabilities inside it remain missing, and a capability can span phases. Without a row per capability, gaps become invisible.

The matrix is therefore derived from the **EF Core provider contract**: the services a relational provider must supply, and the behaviour the specification suite exercises. The reference implementation appears only as a comparison column.

## 2. Rules

- A row exists because EF Core asks for the capability, not because the reference implementation had it.
- Status changes only with evidence. "Implemented" without a passing test is "Partial".
- Rows are added when a gap is discovered and are never deleted. A capability that turns out to be unnecessary is marked `Not applicable` with the reason, so the decision is recorded.
- The matrix and the [skip ledger](testing.md) measure different things and both are needed: the matrix says which capabilities exist, the ledger says which specification cases fail. Neither replaces the other.
- Coverage counts are not a metric. A row is either working or it is not.

## 3. Status vocabulary

| Status | Meaning | Required evidence |
|---|---|---|
| `Implemented` | Works end to end for the whole capability as EF Core defines it | A passing test, named in the Evidence column |
| `Partial` | Works for a named subset only | The subset and the missing part are both named |
| `In progress` | Code exists but the capability is not yet correct or not yet verified | The current observable behaviour |
| `Not started` | No code | — |
| `Deferred` | Deliberately postponed | The phase that picks it up |
| `Blocked` | Cannot be settled without a live server | What must be observed |
| `Not applicable` | EF Core requests it, Dameng has no equivalent | Dameng documentation or a recorded experiment |

`Blocked` and `Not applicable` are the only statuses that remove a row from the roadmap's critical path. Everything else is outstanding work.

## 4. Reference implementation coverage, as measured

Measured from the decompiled source tree, not from its documentation.

| Area | Files | Present |
|---|---|---|
| Storage | 26 | 17 type mappings, type mapping source, SQL generation helper, connection, transaction, database creator, execution strategy, transient exception detector |
| Query | 27 | Query SQL generator, method and member translator providers, nine translators, nullability processor, SQL translating visitor, postprocessor, parameter processor, compiled query cache key generator |
| Update | 4 | Modification command batch and factory, update SQL generator |
| Migrations | 5 | SQL generator, history repository, database lock, annotation provider, four schema and user operations |
| Scaffolding | 3 | Database model factory, code generator, data reader extension |
| Value generation | 7 | HiLo sequence generator, factory, state, cache, selector |
| Conventions | 4 | Convention set builder, value generation and store generation conventions |
| Design time | 2 | Design time services, annotation code generator |
| Diagnostics | 3 | Event ids, logging definitions, command cancellation interceptor |

What it did **not** have:

- No specification test project. All 70 test files are self-authored. Nothing in the reference proves its behaviour against EF Core's own contract.
- No `ISqlExpressionFactory`, no `IQueryCompilationContextFactory`, no `IExceptionDetector`, no `INavigationExpansionExtensibilityHelper`, no `IModificationCommandFactory`, no `IAggregateMethodCallTranslatorProvider`.
- No spatial (NetTopologySuite) support.
- A third-party annotations dependency (`JetBrains.Annotations`), which this project does not take.

The absence of those services does not by itself mean a capability was missing: several have working relational defaults. It does mean the reference cannot be used to conclude that a capability is *unnecessary*. Each row below is decided on its own.

## 5. Matrix

Evidence names a test class in `tests/DM.EntityFrameworkCore.Tests`, or `—` when there is none. Phase refers to [Roadmap](roadmap.md). Reference is `Yes` when the decompiled reference contained an implementation for the capability, `No` when it did not.

### A. Connectivity and configuration

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| Connection string option | `Implemented` | `DmProviderRegistrationTests` | P0a | Yes | |
| Externally owned `DbConnection` | `Implemented` | `DmProviderRegistrationTests` | P0a | Yes | Ownership is delegated to `RelationalOptionsExtension` |
| Command timeout | `Implemented` | `DmInheritedBehaviorTests`, `DmDbContextOptionsBuilderTests` | P1 | Yes | The value the context sets reaches the command it executes, whether it is set through `Database.SetCommandTimeout` or through the options builder |
| Provider options builder | `Implemented` | `DmDbContextOptionsBuilderTests` | P0a | Yes | `DmDbContextOptionsBuilder` derives from `RelationalDbContextOptionsBuilder`, which is the base type that puts the relational settings within reach of a `UseDm` call: command timeout, max batch size, migrations assembly, the collection translation mode and the rest. The reference additionally exposed `EnableRetryOnFailure` on it, which is not provided while there is no retrying strategy |
| Max batch size | `Deferred` | — | P3 | Yes | Follows Multiple statements per batch: with one command per batch there is nothing to cap, so the option is accepted and has no effect. Revisit with that row |
| Open, close, reopen, async | `Implemented` | `DmConnectionTests` | P1 | Yes | Opens are counted, so a nested open needs its own close. Async members are the driver fallbacks, so they run synchronously underneath |
| Transactions: begin, commit, rollback | `Implemented` | `DmConnectionTests`, `DmTransactionTests` | P1 | Yes | Commit and rollback reach the driver transaction; disposing without committing disposes it; the isolation level the caller asks for reaches it; a unit of work inside a transaction runs on it rather than opening a second one |
| Savepoints and nested transactions | `Partial` | `DmConnectionTests`, `DmTransactionTests` | P1 | Yes | The relational layer executes `SAVEPOINT x`, `ROLLBACK TO x` and `RELEASE SAVEPOINT x` as statements rather than through the driver transaction, so the driver methods for it are unused; a unit of work inside a transaction is bracketed by one. The three statements are asserted as generated. A second transaction is rejected while one is open. Dameng support for `RELEASE SAVEPOINT` is unverified |
| Ambient transaction enlistment | `Blocked` | — | P1 | Yes | Requires observing driver behaviour |
| Non-retrying execution strategy | `Implemented` | `DmInheritedBehaviorTests` | P1 | Yes | A unit of work runs once, because a retry would replay a statement whose failure may already have changed the database |
| Retrying execution strategy | `Not started` | — | P1 | Yes | Needs Dameng error codes |
| Transient error classification | `Blocked` | — | P1 | Yes | Error codes must be observed, not guessed |
| Cancellation token honoured on every async path | `Partial` | `DmInheritedBehaviorTests`, `DmCommandCancellationTests` | P1 | Yes | A token cancelled before the call stops the query from starting, and a token cancelled while an asynchronous statement runs reaches `DbCommand.Cancel` through the provider's interceptor, which the command double records. The save path is covered as well, since it runs its statement through a reader rather than a non query. A synchronous statement takes no token. What the real driver does with `Cancel` is unverified |
| Database exists check | `Implemented` | `DmDatabaseCreatorTests` | P1 | Yes | Implemented by attempting a connection; exercised indirectly, the failure branch is unverified |
| Has tables check | `Implemented` | `DmDatabaseCreatorTests` | P1 | Yes | Catalog query over `SYS.SYSOBJECTS`, excluding system objects and recycle bin entries |
| `EnsureCreated` | `Implemented` | `DmDatabaseCreatorTests` | P1 | Yes | Creates the model tables when the database exists and is empty |
| Database creation and deletion | `Not applicable` | `DmDatabaseCreatorTests` | P1 | Yes | Dameng creates a database with `dminit` and drops it by removing files; neither is a statement a connected session can issue |

### B. Type mapping

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| `int`, `long`, `short`, `byte`, `sbyte` | `Implemented` | `DmTypeMappingTests` | P1 | Yes | Store type names provisional until the P1 catalog check |
| `bool` | `Implemented` | `DmTypeMappingTests` | P1 | Yes | |
| `decimal` with precision and scale | `Implemented` | `DmTypeMappingTests` | P1 | Yes | Defaults to `DECIMAL(29,4)` when no facet is configured |
| `float`, `double` | `Implemented` | `DmTypeMappingTests` | P1 | Yes | |
| `string` with size, unicode and fixed length | `Implemented` | `DmTypeMappingTests`, `DmReverseTypeMappingTests` | P1 | Yes | `NVARCHAR2`, `NCHAR`, `VARCHAR2` and `CHAR`, sized when configured. The variable length names are the ones the reference provider writes; the manual states that `VARCHAR2` is used the same way as `VARCHAR`, and both names read back to a string A property that names no unicode setting takes the unicode type, because a .NET string is UTF-16 and the ansi type is written in the database character set; `IsUnicode(false)` takes `VARCHAR`/`CHAR`, which is the shape the reference provider declares for such a column. A string the model gives no length, and one longer than the 8188 bytes a bounded character column holds on an 8K page, becomes `CLOB`, or `NCLOB` when the property is unicode. A bounded type with no length is not a type the server accepts in a table definition, so naming the large object is what keeps DDL that a model without a length generates valid. A string that is part of a key or an index cannot be a large object, because Dameng indexes none, so one that names no length takes the bound the framework's own providers use for a key: 450 characters when unicode, 900 bytes when not, and one character when the length is fixed. Whether a national character column on an 8K page takes 8188 characters, at two bytes each, is not confirmed against a server; the reference provider names 32767 either way and leaves the length to the server The reference provider names 32767 characters as its own bound, which is the expression limit rather than the column one; a database built from that schema reads back as a sized string, so the two do not disagree when reading |
| `byte[]` with size | `Implemented` | `DmTypeMappingTests` | P1 | Yes | `BLOB` without a size, `VARBINARY(n)` with one |
| `DateTime` with fractional seconds | `Implemented` | `DmTypeMappingTests` | P1 | Yes | Precision suffix only when configured |
| `DateTimeOffset` | `Implemented` | `DmParameterBindingTests` | P1 | Yes | Stored as `DATETIME WITH TIME ZONE`, bound as `DbType.DateTimeOffset` with the offset intact |
| `TimeSpan` | `Implemented` | `DmParameterBindingTests`, `DmTypeMappingTests` | P1 | Yes | Stored as `INTERVAL DAY TO SECOND` and bound as `DbType.Time`. A literal names the interval type, `INTERVAL '1 02:03:04' DAY TO SECOND`, because a bare time string is a character value and does not compare with an interval column; a negative span carries the sign in front of the value |
| `DateOnly`, `TimeOnly` | `Implemented` | `DmTypeMappingTests`, `DmReverseTypeMappingTests` | P1 | Yes | Stored as `DATE` and `TIME(precision)`. Dameng gives a `TIME` column that names no precision a fractional second precision of 0, so the provider always names one: the precision the model configures, or six, which is the largest Dameng accepts and what the reference provider writes. The cast a `TimeOnly` conversion produces names it too, so a conversion does not truncate the value it is compared with |
| `Guid` | `Implemented` | `DmTypeMappingTests`, `DmReverseTypeMappingTests` | P1 | Yes | Stored as the 36-character text a `Guid` formats to, declared `CHAR(36)`, which is the shape the reference provider writes in its own migrations. A `CHAR(36)` column read from the catalog resolves back to a `Guid`, so a round trip does not turn the property into a string. Whether the driver writes the 36-character form for the declared type is unverified |
| `char`, `enum` | `Implemented` | `DmParameterBindingTests` | P1 | Yes | A character is stored as `VARCHAR(1)` and bound as a one character string; an enum is stored as `INT` and bound as its underlying integer, including the nullable form |
| JSON column type | `Not started` | — | P1 | Yes | A store type alone is not enough in EF Core 10: with one in place the model builds, the DDL and the writes are correct, and materialization then fails because the container column also needs a JSON reader/writer, which `JsonTypeMapping` exposes no hook for |
| Spatial types (NetTopologySuite) | `Deferred` | — | After P6 | No | Not required by the specification suite core cases; revisit after the first release |
| Store type to CLR reverse lookup | `Implemented` | `DmReverseTypeMappingTests` | P1 | Yes | Recovers the CLR type and the size, precision and scale written in the name. `DATE` maps to `DateTime` because Dameng keeps a time part in it, while the forward direction maps `DateOnly` to `DATE`. `FLOAT` maps to `double`, because it is FLOAT(53) and `REAL` is the single-precision type, which is what the reference provider maps it to as well; reading it as `float` would truncate a column either provider wrote for a double property. `DATETIME` resolves alongside `TIMESTAMP`, and the two zone-aware names to `DateTimeOffset`. A `CHAR(36)` column reads back as a `Guid`, the width this provider writes for one, so a character column of exactly that width that holds text reads back as a Guid too, which the catalog cannot distinguish |
| Concurrency token (`rowversion`) | `Not started` | — | P3 | Yes | |
| Type mapping clone semantics | `Not applicable` | — | P1 | Yes | The provider builds a mapping per facet set instead of cloning a base mapping, so no clone override is needed |

### C. Query

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| `ToQueryString()` for a simple projection | `Implemented` | `DmQuerySqlGenerationTests` | P0a | Yes | Works with no database involved |
| `Where`, `OrderBy`, `Select` | `Implemented` | `DmQuerySqlGenerationTests` | P2 | Yes | |
| `Join`, `GroupBy`, aggregates | `Implemented` | `DmQueryPipelineTests` | P2 | Yes | `INNER JOIN`, `LEFT JOIN`, `RIGHT JOIN` and `UNION`, with `GROUP BY` and `HAVING`, `COUNT`, `SUM`, `MIN` and `MAX`. The joins come from the base visitor, and the `LEFT JOIN` and `RIGHT JOIN` operators are the ones EF Core 10 adds; the statement they compose is pinned by a test |
| `Include`, split and single queries | `Implemented` | `DmQueryPipelineTests` | P2 | Yes | Collection loading joins and orders by the root key; the split query keeps the root statement separate |
| Paging (`Skip`, `Take`) | `Implemented` | `DmQueryPipelineTests` | P2 | Yes | `Take` alone emits `FETCH FIRST n ROWS ONLY`, `Skip` alone emits `OFFSET n ROWS`, and together `OFFSET n ROWS FETCH NEXT m ROWS ONLY`, which the DM SQL manual documents as its `ROW_LIMIT` clause in [数据的插入、删除和修改](https://eco.dameng.com/document/dm/zh-cn/pm/insertion-deletion-modification.html). `LIMIT n OFFSET m` is the alternative form and is not used |
| String methods and members | `Implemented` | `DmStringTranslationTests` | P2 | Yes | `Contains`, `StartsWith`, `EndsWith`, `ToUpper`, `ToLower`, `Trim`, `TrimStart`, `TrimEnd`, `PadLeft`, `PadRight`, `IndexOf`, `Replace`, `Substring`, `Length`, plus the `StringComparison` overloads of `Equals`, `Contains`, `StartsWith`, `EndsWith` and `IndexOf`, the `*Invariant` casing members, `Contains(char)`, `IndexOf` with a start position, and a `Trim`, `TrimStart` or `TrimEnd` over a literal character array. Padding maps to `LPAD` and `RPAD`, a character trim to `LTRIM` and `RTRIM` with that character or set, `Trim` over a set to the two nested, and `IndexOf` to `INSTR` shifted by one and moved by the start position. `LastIndexOf` and `Replace(char, char)` are not translated; the reference provider does not translate them either. An ignore-case comparison upper-cases both sides; a culture-sensitive comparison is compared by the column collation instead, which orders differently. Function names follow Oracle-compatible mode and are unverified against a server. |
| String composition (`IsNullOrWhiteSpace`, `Insert`, `Remove`) | `Implemented` | `DmStringTranslationTests` | P2 | No | `Insert` and `Remove` keep the part in front of the index with `SUBSTR` and join the rest back on; `IsNullOrWhiteSpace` tests both `IS NULL` and the trimmed empty value, because an empty string is a value of its own in one Dameng mode and NULL in another, so the test that does not hold never matches. A literal index is folded. An index outside the string is not rejected by the database the way .NET rejects it |
| String concatenation operator | `Implemented` | `DmStringTranslationTests` | P2 | Yes | Emits the double pipe operator; the base generator emits `+`, which is arithmetic for strings |
| Date and time date parts (`Year`, `Month`, `Day`, `Hour`, `Minute`, `Second`, `DayOfYear`, `Millisecond`) | `Implemented` | `DmDateTimeTranslationTests`, `DmDateTimeMethodTranslationTests` | P2 | Yes | The common parts are rendered as `EXTRACT(PART FROM value)`, on `DateTime` and `DateTimeOffset` alike, and on a `DATE`, a `TIME` and an interval. `DayOfYear` and `Millisecond` use `DATEPART(part, value)`, because `EXTRACT` has no such part; the reference provider reads every part with `DATEPART`. Unverified against a server |
| Date and time methods (`AddDays`, `Now`, `UtcNow`, `Today`, `Date`) | `Implemented` | `DmDateTimeMethodTranslationTests`, `DmDateTimeOffsetTranslationTests` | P2 | Yes | Day counts are added directly and sub-day units are scaled into a fraction of a day, including microseconds; months use `ADD_MONTHS`; `Date` uses `TRUNC`; `DateTimeOffset.UtcDateTime` uses `SYS_EXTRACT_UTC`. `DateTime` and `DateTimeOffset` share the arithmetic. The reference emitted SQL Server functions (`GETDATE`, `DATEPART`) for these. `CURRENT_TIMESTAMP` and `SYS_EXTRACT_UTC(SYSTIMESTAMP)` are unverified against a server |
| Math functions | `Implemented` | `DmMathTranslationTests` | P2 | Yes | `ABS`, `ACOS`, `ASIN`, `ATAN`, `ATAN2`, `CEIL`, `COS`, `EXP`, `FLOOR`, `SIGN`, `SIN`, `SQRT`, `TAN`, `TRUNC`, `POWER`, `ROUND`, `LN`, `LOG`. `Math.Max` and `Math.Min` resolve to `GREATEST` and `LEAST` through the translating visitor, which also covers `EF.Functions.Greatest` and `Least`, and `Math.Clamp` is those two nested. A rounding mode is honoured rather than dropped: rounds halves away from zero, which is what `ROUND` does, translate with their digit count, and rounding to even is left untranslated because one `ROUND` cannot express it. The reference provider passes the mode through where a digit count belongs. Unverified against a server |
| `Convert` and `ToString` | `Implemented` | `DmConversionTranslationTests` | P2 | Yes | `Convert` becomes `CAST(value AS type)` using the dialect type names; `ToString` becomes `TO_CHAR`, which needs no declared width. Unverified against a server |
| `new Guid()` and `Guid.NewGuid()` | `Implemented` | `DmGuidTranslationTests` | P2 | No | `Guid.NewGuid()` renders `NEWID()`, which is the function the reference provider uses for it, and the mapping stores the 36-character form. Dameng also has a `GUID()` that answers a 32-character string without the hyphens, so it is not used. `new Guid()` has no server meaning and is a value the client supplies. Whether `NEWID()` answers the hyphenated form in every Dameng version is unverified |
| `byte[].Length` | `Not started` | — | P2 | No | Measured: fails to translate. A byte array is stored as `BLOB` by default, and whether `LENGTH` accepts a `BLOB` rather than only a character or binary string is unverified |
| `LIKE` with `ESCAPE` | `Implemented` | `DmStringTranslationTests` | P2 | Yes | Wildcards in the search value are escaped with a backslash escape character |
| `TimeOnly` conversions (`FromDateTime`, `FromTimeSpan`) | `Implemented` | `DmTimeOnlyTranslationTests` | P2 | Yes | A time of day built from a timestamp is a cast to `TIME`; one built from an elapsed time is that `INTERVAL DAY TO SECOND` added to the epoch and cast. The generated SQL is unverified against a server |
| `TimeOnly` instance methods (`AddHours` and the rest of `Add...`) | `Partial` | `DmTimeOnlyTranslationTests` | P2 | Yes | `Hour`, `Minute` and `Second` read the part with `EXTRACT`. `Millisecond` is not provided, because the part is not in the `EXTRACT` form this dialect uses, and the `Add...` methods are not translated, which the reference provider does not translate either: a time of day that wraps past midnight needs arithmetic on the `TIME` type that neither implementation has established. Unverified against a server |
| `DateOnly` conversions and members (`FromDateTime`, `AddDays`, `AddMonths`, `AddYears`, `DayNumber`) | `Partial` | `DmDateOnlyTranslationTests` | P2 | No | `FromDateTime` truncates with `TRUNC` and then casts, because Dameng's `DATE` keeps a time part; `AddDays` adds the count to the date; `AddMonths` is `ADD_MONTHS`, and `AddYears` is that function with the count scaled by twelve, which a literal count folds to; `Year`, `Month` and `Day` read the part with `EXTRACT`. `DayNumber` is not provided: `TO_DAYS` counts from year zero in a way that disagrees with the difference to that year, and the offset to .NET's day number is unverified. The other members do not translate |
| `TimeSpan` members (`TotalMinutes`, `Add` and the rest) | `Partial` | `DmTimeSpanTranslationTests` | P2 | No | The parts `Days`, `Hours`, `Minutes` and `Seconds` read the interval part with `EXTRACT`, and a part that is not a total truncates the seconds because `EXTRACT(SECOND ...)` keeps the fraction that the member drops. Each of the five totals sums the parts in seconds and scales the result. `Add` and `Subtract` are the interval operators. `Duration`, `Negate`, `Ticks` and `Milliseconds` are not translated. Whether Dameng signs each part of a negative interval the way a negative `TimeSpan` does is unverified |
| `DateTime.Subtract` returning a `TimeSpan` | `Partial` | `DmDateTimeMethodTranslationTests` | P2 | No | A date minus an interval is a date. The difference of two dates is not translated: the SQL development guide states that subtracting two dates answers a number of days rather than an interval, and the conversion to an interval is behind, or fails under, Oracle compatible mode, which a dialect that must work in every mode cannot rely on. The shape stays untranslated instead of declaring a `TimeSpan` the server would not send back, and a test records the measurement. See `version-matrix.md` §9 for the sources |
| Null semantics processor | `Not applicable` | `DmStringTranslationTests` | P2 | Yes | The difference between an empty string and NULL is a server parameter, not a shape: under `COMPATIBLE_MODE=2` the empty string is NULL, and under the default mode it is a value of its own, so the same statement means both things depending on the instance. A translator cannot see the mode, which is why the dialect never branches on it. `IsNullOrEmpty` and `IsNullOrWhiteSpace` test both conditions, so they hold either way; nothing else is normalised |
| Parameter-based SQL processor | `Implemented` | `DmQuerySqlGenerationTests`, `DmCollectionParameterTests` | P2 | Yes | The base processor is enough: a parameter renders with a colon prefix, which Oracle-compatible Dameng accepts, and both collection translation modes are verified. Nothing in the processor depends on the database beyond that |
| Compiled query cache key | `Implemented` | `DmInheritedBehaviorTests` | P2 | Yes | The default key already covers everything the translation depends on, because the translation is a function of the model and the query expression |
| Collection parameter translation | `Implemented` | `DmCollectionParameterTests` | P2 | No | The relational layer expands the collection and the provider's generator renders either mode: one parameter per element by default, the values as literals under `UseParameterizedCollectionMode(ParameterTranslationMode.Constant)`. A context that shares a model but differs only in the mode reuses the first translation, because the translation is cached against the compiled query |
| `FromSql` and composable raw SQL | `Implemented` | `DmDmlQueryTests` | P2 | Yes | Passed through verbatim, or wrapped as a derived table when composed |
| `ExecuteUpdate`, `ExecuteDelete` | `Implemented` | `DmDmlQueryTests` | P3 | No | EF Core 7+ contract. The table alias is declared with `AS`, which the manual writes as `[AS] <表别名>` |
| Inheritance mapping (TPH, TPT, TPC) | `Implemented` | `DmInheritanceTests` | P2 | Yes | One table per hierarchy with a discriminator, one table per type with the key generated in the base table only, and one table per concrete type. A hierarchy mapped per concrete type has no generated key until the model names a generator, which is what EF Core does for every provider |
| JSON querying | `Deferred` | — | After P6 | No | The store type exists in the dialect; query translation is not required for the first release |
| Query filters and global filters | `Implemented` | `DmInheritedBehaviorTests` | P2 | Yes | A filter becomes a predicate of the query, translated like any other |

### D. Update

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| `INSERT`, `UPDATE`, `DELETE` | `Implemented` | `DmUpdateSqlTests` | P3 | Yes | No `RETURNING` clause, because Dameng accepts it only as `RETURNING ... INTO`, which binds to a variable and not to a result set. An insert that has to read a generated value adds a second statement |
| Multiple statements per batch | `Deferred` | `DmUpdateSqlTests` | P3 | Yes | One statement per round trip; the affected row count comes from the driver return value and could not be attributed to individual commands in a shared batch |
| Store-generated value readback | `Implemented` | `DmGeneratedValueTests` | P3 | Yes | After an insert or an update, a second statement selects the generated columns. An insert is matched by `sql%ROWCOUNT = 1` and by `scope_identity()` for an identity column or the sequence current value for a sequence default; an update is matched by the same where clause and the same parameters as the statement it follows. A computed column therefore comes back after an update, and the statement shape has not been run against a server |
| Concurrency conflict detection | `Implemented` | `DmUpdateSqlTests` | P3 | Yes | A statement affecting a row count other than one raises `DbUpdateConcurrencyException` |
| Modification command factory | `Not applicable` | `DmUpdateSqlTests` | P3 | No | The relational default builds the `ModificationCommand` shape this provider's batch already consumes, so substituting a factory would add a type with no behaviour |
| `SaveChanges` transaction wrapping | `Implemented` | `DmConnectionTests` | P3 | Yes | A unit of work that builds more than one command is wrapped and committed; a single command runs without a transaction |

### E. Migrations

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| Migrations SQL generator (DDL) | `Implemented` | `DmMigrationsSqlTests` | P4 | Yes | Table, column, index, constraint, sequence, schema and data shapes, each pinned by a statement test: drop table and column, add and drop a primary key, a unique constraint and a foreign key, restart and drop a sequence, drop a schema, raw SQL, and insert, update and delete of seeded rows. A sequence takes no type clause and spells its options as one word, an alteration repeats every option, and an alter, rename or schema operation is implemented here rather than by the base class. A column that stops generating its value loses the attribute with `DROP IDENTITY` in front of the modification, which the reference provider also writes, and the whole path is pinned through the model differ rather than only through a hand written operation; the other direction is rejected with a message, because no statement that turns an existing column into an identity column has been verified, and a migration that silently did nothing would leave the model expecting a generated value. The restart shape and the identity statement itself are unverified against a server |
| Table and column comments (`HasComment`) | `Implemented` | `DmMigrationsSqlTests`, `DmDatabaseModelFactoryTests` | P4 | No | A comment is its own statement in Dameng: `COMMENT ON TABLE ... IS` and `COMMENT ON COLUMN ... IS`, written after the statement that creates or alters the object, and an empty string removes one. Reverse engineering reads both back through `ALL_TAB_COMMENTS` and `ALL_COL_COMMENTS`. The syntax comes from a published article on DM comments rather than the SQL manual, and the catalog views are unverified |
| History repository | `Implemented` | `DmHistoryRepositoryTests` | P4 | Yes | Catalog existence check against `SYS.SYSOBJECTS`, unverified against a server |
| Migration lock | `Implemented` | `DmHistoryRepositoryTests` | P4 | Yes | Exclusive table lock released by the transaction; no lock row, so a failed run cannot strand one |
| Schema operations | `Implemented` | `DmMigrationsSqlTests` | P4 | Yes | `CREATE SCHEMA` runs inside a block that asks `SYS.SYSOBJECTS` first, so a schema the database administrator created does not fail the migration. The catalog query is not yet confirmed against a server |
| User operations | `Implemented` | `DmMigrationsSqlTests` | P4 | Yes | Creating a user creates the schema it owns in a second statement. Privileges are not granted: the reference grants DBA as part of the operation, which a migration should not decide |
| Migrations annotation provider | `Implemented` | `DmAnnotationProviderTests` | P4 | Yes | The identity attribute and a non default seed and increment reach the column operation the differ produces, so the generated migration shows them and a replay reads them; the mirrored key of a table per type hierarchy carries none |
| `MigrationBuilder` extensions | `Implemented` | `DmMigrationsSqlTests` | P4 | Yes | `IsDm()` lets one migration assembly guard its provider specific operations |
| Idempotent script generation | `Implemented` | `DmHistoryRepositoryTests` `DmIdempotentScriptTests` | P4 | Yes | Every guarded statement reads the catalog in a PL/SQL block and runs through `EXECUTE IMMEDIATE`, because Dameng has no `IF NOT EXISTS` for DDL: the history table and each migration row through `SYS.SYSOBJECTS` and the history table, and a create of a table, sequence, index, constraint or added column through the Oracle compatible views, with the owner compared against `USER` when the migration names no schema and the table named as well for a column, whose name is unique within a table rather than within a schema. Whether DDL inside such a block is accepted, and whether the catalog views report the objects under those names, is not yet confirmed against a server |
| Migration script composition | `Implemented` | `DmMigrationScriptTests` | P4 | Yes | A whole script is composed through the migrator rather than operation by operation, which is what a deployment runs: every batch separator is a slash on a line of its own, because the writer appends the next statement directly after it, and a script opens no transaction, because Dameng starts one with the first statement it runs and its manual defines no `START TRANSACTION`; `COMMIT` stays. The guards of an idempotent script nest as blocks and close in the order they opened. The shape was wrong in both places before the test existed, and no server was needed to see it |
| Schema agreement between the DDL paths | `Implemented` | `DmSchemaConsistencyTests` | P4 | Yes | The create script the relational database creator builds and the statements a differ produces are compared statement for statement over a model that carries identity, hi-lo, a sequence, a default value, comments, a fixed length string, a Guid, a byte array, a timestamp with a precision, an offset timestamp, a date and an elapsed time. Both paths read the same model and the same generator, so they agree today; the test is what keeps a change to one layer from making them disagree, which would only show up on a database |
| Model differ integration | `Implemented` | `DmModelDifferTests` | P4 | Yes | The EF Core differ compares the two relational models and keeps the provider metadata: a new hi-lo property produces the schema and the sequence, a column that appears produces an added column rather than a modified one, and a widened column produces the matching `MODIFY`. Changing the value generation strategy between identity and hi-lo yields an `ALTER` that only repeats the type, because Dameng defines identity at column creation; a migration that switches strategy needs a hand-written operation |

### F. Scaffolding and design time

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| Database model factory (reverse engineering) | `Partial` | `DmDatabaseModelFactoryTests` | P4 | Yes | Reads tables, columns, table and column comments, the primary key, unique constraints, indexes, foreign keys and sequences through the Oracle compatible views in `Dialect/`. A constraint whose columns were not read is skipped rather than half built. The queries themselves are unverified against a server, and views are not read because EF Core 10 gives reverse engineering no place for one: the factory options carry tables and schemas and the model carries tables and sequences, so a test pins that contract and fails when a slot appears |
| Code generator | `Partial` | `DmProviderConfigurationCodeGeneratorTests` `DmDatabaseModelFactoryTests` | P4 | Yes | The provider configuration code is generated: `UseDm` with the connection string and no provider options, since the provider declares none. The model it is generated from now comes from the database model factory, so what remains unverified is the catalog queries that factory reads |
| Annotation code generator | `Implemented` | `DmSnapshotTests` `DmMigrationCodeGeneratorTests` | P4 | Yes | The relational generator is enough: an annotation the generator does not name is written to the snapshot as `HasAnnotation`, which is how the value generation strategy, the identity seed and the hi-lo sequence reach the next migration |
| Design-time services | `Implemented` | `DmSnapshotTests` | P4 | Yes | Declared through `DesignTimeProviderServices`, with the runtime services and the relational design services. A generated snapshot file was produced through it, so the tooling can build one and it compiles |
| Provider model validator | `Implemented` | `DmModelValidatorTests` | P1 | Yes | Rejects an identity column that is not an integer, one that also has a default expression, and a table with two of them, all while the model is built |
| Compiled model support | `Implemented` | `DmCompiledModelTests` | P6 | Yes | The generated source carries the provider annotations, the hi-lo sequence name and schema, and the sequence with its increment, because the relational runtime annotation generator emits an annotation it does not name as a plain value. The source is compiled and loaded in the test, and a context built from it produces the same create script and the same query SQL as the ordinary model. The store type is not pinned, which is correct: it is resolved from the type mapping source at run time |

### G. Value generation

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| Identity columns | `Implemented` | `DmValueGenerationStrategyTests`, `DmFluentApiTests` | P5 | Yes | The model marks an integer key, the DDL emits `IDENTITY(seed,increment)` from the model or property setting, and an insert reads the generated key back |
| Sequences | `Implemented` | `DmHiLoValueGenerationTests`, `DmGeneratedValueTests` | P5 | Yes | A sequence can be declared in the model, created by a migration, read in blocks for hi-lo, and used as a key default whose value is read back after the insert. The provider never advances a sequence to produce a client side value one at a time |
| HiLo over a sequence | `Implemented` | `DmHiLoValueGenerationTests` | P5 | Yes | A block is reserved per round trip, sized by the sequence increment, and shared by every context. The default sequence is added by the model convention |
| Value generator selector and cache | `Implemented` | `DmHiLoValueGenerationTests` | P5 | Yes | Chooses the hi-lo generator for the strategy and keeps the block state in the singleton cache |
| Store generation convention | `Implemented` | `DmValueGenerationStrategyTests` | P5 | Yes | An integer key the store generates becomes an identity column unless the model or the property says otherwise, and a key that carries a default expression is recorded as sequence generated instead of also becoming an identity column |
| Client-side value generation | `Implemented` | `DmInheritedBehaviorTests` | P5 | Yes | A key the store does not generate is filled before the insert, and a key the store does generate is tracked as temporary until the read back replaces it |

### H. Conventions and metadata

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| Provider convention set builder | `Implemented` | `DmProviderRegistrationTests` | P0a | Yes | Required for the relational model to be built |
| Value generation strategy convention | `Implemented` | `DmValueGenerationStrategyTests` | P5 | Yes | Chooses `IdentityColumn`, respects an explicit value, and fails the model for a strategy the provider cannot honour |
| Fluent API: property extensions | `Implemented` | `DmFluentApiTests`, `DmAnnotationProviderTests` | P5 | Yes | `UseHiLo` and `UseIdentityColumn`, each with a typed overload, plus the annotation accessors behind them, including the strategy of one table which answers whether that table generates the value. The `IConventionPropertyBuilder` helpers are not provided |
| Fluent API: model builder extensions | `Implemented` | `DmFluentApiTests` | P5 | Yes | `UseHiLo` and `UseIdentityColumns`, which set the strategy every generated property inherits. The size, service tier and performance level settings of the reference are not provided |
| Fluent API: model and entity type extensions | `Partial` | `DmFluentApiTests` | P5 | Yes | `DmModelExtensions` exposes the hi-lo sequence, identity seed and increment settings. The temporal and period extensions of the reference are not provided |
| `DmValueGenerationStrategy` annotation | `Implemented` | `DmValueGenerationStrategyTests` | P5 | Yes | `DmValueGenerationStrategy` keeps the Dameng member order, so an existing model keeps its annotations |
| Database facade extensions | `Implemented` | `DmDatabaseFacadeTests` | P1 | Yes | `IsDm` compares the provider name with this assembly, which is the same answer the migration builder extension gives |
| `DbFunctions` extensions | `Partial` | `DmDateDiffTranslationTests`, `DmIsDateTranslationTests` | P2 | Yes | `DateDiffYear`, `DateDiffMonth`, `DateDiffDay`, `DateDiffHour`, `DateDiffMinute`, `DateDiffSecond` and `DateDiffMillisecond` over `DateTime` and `DateTimeOffset`, each in a nullable form, translate to `DATEDIFF(datepart, date1, date2)`, which the DM SQL manual documents; the same four also take a `TimeSpan`, which the statement receives where a date would sit, as the reference provider does, and whether `DATEDIFF` accepts an interval is unverified; `IsDate` translates to `ISDATE(value) = 1`, because Dameng has no boolean type for the flag to be used as a condition. A report in the Dameng community has a day difference counted across calendar boundaries rather than in 24 hour spans, so a fractional count is not what `DateDiffDay` answers; the exact rule is unverified. Called outside a query these compute the answer in memory, counting the elapsed time; the reference provider subtracted the hour, minute and second components alone, which could not answer a difference past a day. Microsecond and nanosecond are not provided, because their datepart spelling in `DATEDIFF` is unverified, and the reference `FreeText` and `Contains`, which need a full-text index, are not provided |

### I. Diagnostics

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| Provider logging definitions | `Implemented` | `DmProviderRegistrationTests` | P0a | Yes | |
| Provider event ids | `Implemented` | `DmDiagnosticsTests` | P6 | Yes | `DmEventId` declares the provider range from 30000 under the model validation category, starting with the decimal default warning |
| Command cancellation interceptor | `Implemented` | `DmCommandCancellationTests` | P1 | Yes | Registered for the whole provider. It binds the token of the three asynchronous execution entry points to `DbCommand.Cancel`, keeps the binding while a reader is consumed, and releases it when the command ends; the binding is held weakly, so the interceptor never retains a command. A synchronous statement takes no token and is not covered |
| Sensitive data logging compliance | `Implemented` | `DmUpdateSqlTests`, `DmQueryPipelineTests` | P6 | Yes | Every statement the provider builds carries parameters through the relational command builder, so a value never enters the log text, and the provider formats no value of its own |

### J. Testing and tooling

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| T1 unit tests for `Dialect/` | `Implemented` | `DmIdentifierTests`, `DmParametersTests`, `DmDialectConstantsTests` | P0a | Yes | |
| Dialect isolation guard | `Implemented` | `DialectLayerIsolationTests` | P0a | No | This project's rule |
| Documentation consistency guards | `Implemented` | `DocumentationTests` | P0a | No | This project's rule. A relative link has to resolve, every document in `docs/` has to be indexed, a cited decision has to be defined in the architecture record, and a cited phase in the roadmap. Each guard was mutation tested: a broken link, an unindexed document, an undefined decision and an undefined phase each fail the suite |
| Public surface guard | `Implemented` | `PublicSurfaceTests` | P0a | No | This project's rule. The exported types are pinned as a list, and the date difference overloads are counted, so a type that appears or disappears and an overload that is dropped fail the suite. A consumer compiles against this surface, which makes either a breaking change |
| T2 SQL shape tests | `Implemented` | `DmQuerySqlGenerationTests` | P0a | Yes | Complete statement asserted, expression placed where server execution is required |
| T3 specification suite | `Not started` | — | P0b | **No** | The reference never had one |
| T4 differential comparison | `Not started` | — | P6 | No | Diagnostic only |
| Live validation console | `Blocked` | — | P6 | Yes | The program is written and builds, and every step awaits a live instance: it creates its own prefixed tables, marks its rows with a run identifier, drops only what it created, and returns a non-zero exit code when a step fails. Eighteen steps are declared, and one of them applies a shipped migration, reads a row back through the model and applies the migration again, which is the only way this provider's migration path is exercised against a server. A dry run compares the shipped snapshot with the model and generates the idempotent script of the migration, failing when either drifts; both were mutation tested. Running it against an unreachable server reports the driver's refusal and exits non-zero, which is the extent of what can be observed here |
| Package consumer smoke test | `Implemented` | `scripts/package-smoke.ps1` | P6 | Yes | Packs the provider, restores a project that references it as a package into its own NuGet cache, compares the SHA-256 of the resolved package with the packed one, and runs the consumer, which builds a model and a create script without connecting to a server |
| Package metadata | `Implemented` | `PackagingTests` | P6 | Yes | The id, version, MIT licence, README, author, project URL, repository URL and tags are in the packed nuspec, and the expanded package carries the documentation file beside the assembly, and the LICENSE text at the root, so the terms travel with the copy. A test reads the version the build defines and asserts the assembly reports it, that the EF Core dependency range stays bounded, and that the documentation file is beside the assembly |

## 6. Current position

Rows by status, updated when the matrix changes:

| Status | Count |
|---|---|
| `Implemented` | 88 |
| `In progress` | 0 |
| `Partial` | 10 |
| `Blocked` | 3 |
| `Deferred` | 4 |
| `Not started` | 6 |
| `Not applicable` | 4 |
| **Total rows** | **115** |

The count is a rough indicator of distance, not a progress bar. The progress bar is the [skip ledger](testing.md).

## 7. Updating this document

1. Change a row's status in the same commit that changes the behaviour.
2. Name the test that proves it. If there is no test, the status is `Partial` or `In progress`, never `Implemented`.
3. Add rows when a new EF Core contract surface is discovered. Record the discovery in the row's Note.
4. Recompute the counts in section 6. `CapabilityMatrixTests` fails when the summary and the rows disagree, when the rows do not add up to the declared total, or when an implemented row names no test, so the step is enforced rather than remembered.
5. When a capability is judged unnecessary, mark it `Not applicable` and state the reason. Do not delete the row.
