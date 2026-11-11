# Testing

Status: **decided**. Changes to this document require review.

## 1. Acceptance model

Provider correctness is defined by the EF Core specification test suite, not by self-authored SQL assertions.

| Layer | Content | Nature | Cost |
|---|---|---|---|
| T1 | Unit tests for `Dialect/` pure functions, error classification, identifier escaping | No database | milliseconds |
| T2 | SQL shape assertions via `ToQueryString()` plus parameter inspection | No database; **never sufficient on its own** | milliseconds |
| T3 | **EF Core specification test suite**, executed against a live database | **Primary gate** | minutes |
| T4 | Differential comparison against the official provider for the same query | Diagnostic aid, not a gate | minutes |
| T5 | Package smoke test: packs the provider, restores a consumer into an isolated NuGet cache, compares the SHA-256 of the resolved package, runs the consumer | No database; needs a pack, so it is not part of `dotnet test` | about a minute |

### Constraint on T2

When a method is called only in the final projection, EF Core may read the column and evaluate in the client. `ToQueryString()` not throwing does **not** prove server-side translation.

Statements the provider executes cannot be observed through `ToQueryString()`. Those are asserted by running `SaveChanges` against a recording connection: a `DbConnection` double that captures command text and reports a configurable affected row count. It is a stub rather than a database, so it proves the SQL and the pipeline, not the server's acceptance of that SQL.

Every translation test places the expression in a position that requires server execution: `WHERE`, `ORDER BY`, a JOIN key, or a table-valued function call. It then asserts the complete generated SQL.

## 2. Specification test suite

### Packages

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Relational.Specification.Tests" />
```

| Branch | Package version | Test framework |
|---|---|---|
| `efcore/10.0` (EF Core 10) | `10.0.14` | xUnit v2 (`xunit.core` 2.9.3) |
| `dev` (EF Core 10) | `10.0.14` | xUnit v2 |
| `efcore/9.0` | `9.0.22` | xUnit v2 |
| `efcore/8.0` | `8.0.33` | xUnit v2 |
| EF Core 11 | `11.0.0-rc` | **xUnit v3** (`xunit.v3.assert` 4.x) |

### Dependency pinning

The specification test package pins its own dependency versions, which must be honoured:

- `xunit.core` and `xunit.assert` 2.9.3, `xunit.runner.visualstudio` 3.1.3 on the EF Core 8/9/10 branches
- `NetTopologySuite` 2.6.0, `Newtonsoft.Json` 13.0.4
- `Microsoft.CodeAnalysis.*` 5.0.0, `Mono.TextTemplating` 3.0.0

Rule: the **specification test project** is exempt from central package management, because the EF Core test package pins `xunit.core` and `xunit.assert` at 2.9.3 and its own analysis and templating versions. The unit test project follows the central versions, since it inherits nothing from EF Core. Do not raise the specification test project's xunit versions independently of the EF Core version: they move together.

`Microsoft.EntityFrameworkCore.Design` is build and test tooling and is never part of the package, so it is pinned to a current patch (`$(EfCoreDesignVersion)`) instead of the dependency range. Resolving the range floor for it also resolves transitive packages that NuGet audit reports as vulnerable, and pinning keeps `TreatWarningsAsErrors` meaningful for everything else. The provider itself still compiles against the floor of the range.

### Test framework follows the EF Core version

The test framework version is bound to the EF Core version and is not unified across the repository. EF Core 11 moved its specification assemblies from xUnit v2 to xUnit v3. The EF Core 11 branch migrates its specification test project to `xunit.v3` packages, the `Microsoft.Testing.Platform` runner, and the v3 assertion API, and converts `[ConditionalFact]` to `[Fact]` and `[ConditionalTheory]` to `[Theory]`.

### Project structure

```
tests/
  DM.EntityFrameworkCore.Tests/                      T1, T2, branch-specific regressions
  DM.EntityFrameworkCore.PackageConsumer/             T5: restores the packed provider as an application does
  DM.EntityFrameworkCore.SpecificationTests/         T3: inherits EF Core specification base classes, live only
    TestUtilities/                      the test store, its factory and the shared fixture
    Skipped.md                          skip ledger
scripts/
  package-smoke.ps1                     T5: packs, restores in an isolated cache, runs the consumer
  design-time-smoke.ps1                 live tooling: migrations and scaffolding through `dotnet ef`
samples/
  DmProviderValidation/                 live end-to-end acceptance
```

## 3. Skip ledger

Tests that do not pass are recorded, never deleted. The ledger is the roadmap.

The ledger and the [Capability Matrix](capability-matrix.md) measure different things and neither replaces the other: the ledger records which specification cases fail, the matrix records which capabilities exist. A capability can be missing while every ledger entry is explained.

Each entry contains the test class or case, the reason, a category, and a tracking issue.

| Test class or case | Reason | Category | Issue |
|---|---|---|---|
| `BuiltInDataTypesTestBase...` | Dameng has no `INTERVAL YEAR TO MONTH` equivalent | Server limitation | #12 |

The category is exactly one of:

| Category | Meaning | Policy |
|---|---|---|
| `Not implemented` | Provider work outstanding | Must reference an issue and a roadmap phase |
| `Server limitation` | Dameng has no equivalent capability | Must cite Dameng documentation or a recorded experiment |
| `EF Core limitation` | Behaviour of EF Core itself | Must cite the EF Core version and a reference |
| `Known defect` | Provider is wrong | Must mention the blocking issue. Cleared before a release. |

Rules:

1. The count of `Not implemented` entries is the only progress metric.
2. `Known defect` entries do not cross a release boundary. A release either clears them or lists them as known issues in its notes.
3. The ledger is per branch and is never synchronized between branches, because baselines differ.
4. A new ledger entry is committed together with the code it describes, so it appears in review.

## 4. Differential comparison

When the correct Dameng translation is unclear, the official provider is used as a black-box reference.

1. Same entity model, same LINQ query.
2. Generate SQL with this provider and with the official `DM.Microsoft.EntityFrameworkCore`, via `ToQueryString()`.
3. Normalize parameter names and whitespace, then compare. Any remaining difference must be intentional.

Boundaries, from [Provenance](provenance.md): black-box comparison only. The official provider's decompiled source is never read or copied. Differences that are intentional are documented in [Version Matrix](version-matrix.md) or in the dialect notes.

A match is not proof of correctness: both implementations can be wrong in the same way. Execution against a live database remains the final standard.

## 4b. Differential comparison with the reference provider

The reference provider, `DM.Microsoft.EntityFrameworkCore` 9.0.0.43760, is decompiled outside this
repository and is not a build dependency, but the package is a restore away, so its statements can be
compared with this provider's. Two console projects were built on 2026-11-11 against the same model, one
on EF Core 9 with the reference package and one on EF Core 10 with this provider, and both printed
`ToQueryString()` for the same twenty-three queries with no database attached. Twelve differ, and none of
them is a defect here:

| Query | Reference | This provider | Verdict |
|---|---|---|---|
| `StartsWith`, `EndsWith`, `Contains` | `LIKE 'ab%'` | `LIKE 'ab%' ESCAPE '\'` | This provider escapes, which a value holding `%` or `_` needs |
| `Skip`/`Take`, a date parameter | `:__p_0`, `:__at_0` | `:p`, `:p0`, `:at` and a `DbType` annotation | Parameter naming of EF Core 9 against EF Core 10 |
| `Contains` over an array | `IN (1, 2, 3)` | `IN (:ids1, :ids2, :ids3)` | EF Core 10 parameterises a collection by default; both are correct and this one reuses the plan |
| `CreatedAt.AddDays(1)` | `DATEADD(day, CAST(1.0E0 AS INT), …)` | `"CreatedAt" + 1.0` | Both add a day; the addition is what this dialect already documents for a fractional part |
| `Count()` in a projection | `CAST(COUNT(*) AS INT)` | `COUNT(*)` | `COUNT` is already an `INT` here, and the reading path casts anyway |
| `Payload.Length` | the whole column | `LENGTHB("Payload")` | The reference leaves it to the client; `LENGTH` would be the length of the hexadecimal form, measured earlier, so `LENGTHB` is the answer |
| `Span.Hours` | the whole column | `EXTRACT(HOUR FROM "Span")` | The reference leaves it to the client; `EXTRACT` is the member's value |

The other eleven queries — a predicate, a conjunction, `LIKE`, ordering with paging beyond the parameter
names, a grouping with a sum, a join, a projection, `??`, `HasValue`, a comparison, a subquery with
`Any`, `Distinct`, an `Average`, an enum comparison and a byte array length — are the same statement in
both providers. The comparison therefore found no behaviour to change, and its value is the record: a
future difference is a difference from a known baseline.

### 4c. Differential comparison, executed against the server

The same two projects were then pointed at the Oracle-compatible instance and made to run their queries, not
just print them: each created its own two tables, seeded three rows with a known string, a decimal, a
timestamp, a positive and a negative interval and one null payload, and printed the answer of seventy-six
queries, ordered so that the two runs line up. A hundred and eight answers are identical, including every string
function, the rounding and the sign, every date and interval part, the casts, the grouping with six
aggregates, the set operations, the correlated subqueries, the two bulk operations and every `DateDiff` unit
between two timestamps. Eight differ:

| Check | Reference | This provider | Verdict |
|---|---|---|---|
| `CreatedAt.Date` | the timestamp unchanged, `2020-01-02 03:04:05` | midnight, `2020-01-02 00:00:00` | The reference's statement does not truncate a timestamp, so its answer is the column. `TRUNC` is what the member means and what this provider emits |
| `Math.Truncate` | the server refuses the statement: `无法解析的成员访问表达式["TRUNCATE"]` | `TRUNC(...)` | The reference names a function this server does not have |
| `Name.StartsWith(…, OrdinalIgnoreCase)` | reported as untranslatable | matches `abc` and `Abc ` | Translated through `UPPER`, the shape `DmStringTranslationTests` pins |
| `Payload.Length` in a predicate | reported as untranslatable | `LENGTHB("Payload") > 1` | Translated, as `DmByteArrayTranslationTests` records, because `LENGTH` answers the length of the hexadecimal form of a binary value on this server |
| `CreatedAt.TimeOfDay` in a projection | the server refuses the statement: `无法转换的数据类型` | the column, with the member applied while the row is materialised | Neither provider translates the member: this one fetches the timestamp and answers the span of its day, which a query cannot then use in a predicate |
| `CreatedAt.Date == new DateTime(...)` in a predicate | answered no row | answered the row | The reference compares the column with the timestamp without truncating it, so a date the column holds never equals it in that comparison. This provider truncates, which is what the member means |
| `DateTimeOffset.AddHours` | `InvalidCastException: Invalid cast from 'System.DateTime' to 'System.DateTimeOffset'` | the shifted offset | The reference reads a datetimeless value from a column that carries a zone and cannot materialise it back || `Payload!.Length` on a row whose payload is null | `NullReferenceException` | `InvalidOperationException: Nullable object must have a value` | Both refuse the row, because the model says the value is there; the query is only valid for a non-null payload, and neither answer is a translation of it |

Six of the eight are therefore places where this provider answers a query the reference cannot, one is a
member the reference mistranslates, and one is a row both refuse. The tables the two runs created were
dropped again afterwards, and no view, foreign key or stored object referred to them.

### 4d. `DbFunctions`, executed

The `DateDiff` units were executed against the Oracle-compatible instance on 2026-11-11, from the same seed of
three rows and against the reference provider in the same run: `DateDiffDay` answered 1, 33 and 730 days from
2020-01-01, `DateDiffHour` 27, 796 and 17543, `DateDiffMinute` 1624, 47765 and 1052639, `DateDiffSecond` 97445,
2865906 and 63158399, `DateDiffMonth` 0, 1 and 23, and `DateDiffYear` 0, 0 and 1, which are the boundaries
crossed rather than whole units. The same pairs reversed answered the negated counts, which is what the in-memory
form does. `DateDiffMillisecond` answered 1000 for a one-second pair, and `DateDiffMicrosecond` answered 1000000
for the same pair when both arguments are timestamps. Both units were also refused, with `数据溢出`, for pairs
separated by more than the `int` the method returns can hold — about 24.8 days of milliseconds and about 35.8
minutes of microseconds — and the microsecond form was refused for a pair whose second argument is the column
plus a fraction of a day, which is the shape `AddSeconds` leaves behind, because the sum is then a number of days
rather than a timestamp. The reference provider refused the same two shapes, so neither refusal is a divergence.

### 4e. Inheritance, executed

The three strategies were executed against the Oracle-compatible instance on 2026-11-11, each on its own tables
which the run created and dropped: a hierarchy mapped to one table answered its rows through the base set and
through `OfType`, an update changed the derived column and a delete removed the row; a hierarchy mapped per type
did the same, with the key generated in the base table and written into the derived one; and a hierarchy mapped
per concrete type did the same from a sequence the model named.

The per-type run found a defect that the shape tests had not reached: the insert of the derived row was wrapped
in `SET IDENTITY_INSERT … ON`, which the server refuses for a table that declares no identity
(`表[DM_LAYER_TPT_CATS]不存在IDENTITY列`), because the provider decided the flag from the property's strategy
alone rather than from the column the statement writes. The flag now follows the same rule the DDL follows — the
column of the table being written has to be the one that generates — and both cases are pinned by a test: the
derived insert carries no flag, and an assigned key carries it on the base table only.

A hierarchy mapped per concrete type needs a key generator that is unique across the tables; the run used a
sequence named by the model, which is the shape EF documents. A per-table identity answers 1 in every table, so
the two derived rows collide on the key before any statement runs, which is the model's job rather than the
provider's.

## 5. Live database matrix

| Dimension | Values |
|---|---|
| Dameng server | Versions in production use. First reached on 2026-11-11: DM 8.1.4.80 |
| Compatibility mode | The default instance and the Oracle-compatible one both ran the console on 2026-11-11, as reported by the environment's operator; the dialect does not branch on the mode (D4). PostgreSQL-compatible mode is still a P6 exit criterion and has not been exercised |
| Operating system | Windows x64, Linux x64 |
| Target framework | `net8.0` on the EF Core 8/9 branches, `net10.0` on EF Core 10, `net11.0` from EF Core 11 |
| Driver | The `DM.DmProvider` version the package depends on, which is the floor the provider is compiled against; EF Core 10 consumes its `net9.0` asset |

### What the live runs measured

The first runs, on 2026-11-11 against both compatibility modes, passed all eighteen acceptance steps. They also failed one step before the provider changed: `SELECT EXISTS (SELECT 1 ...)` with no `FROM` clause was rejected with `语法分析出错`, which is what made the boolean projection and pseudo FROM clause rows of the [Capability Matrix](capability-matrix.md) exist. A live run that only confirmed what the tests already said would have found nothing; the value of the run is the shape it rejects.

Recorded 2026-11-11: the console now declares thirty-four steps, and all thirty-four passed against DM 8.1.4.80 in both compatibility modes — `--secrets=Dm` on the Oracle-compatible instance (`COMPATIBLE_MODE` 2) and `--secrets=DmFull` on the default one. That run covers the steps added after the eighteen-step one: the interval between two timestamps and the date difference of two intervals, the computed column read back after an update, cancellation, and the three migration steps, one of which starts four migrations at once. The run left no table, sequence or history behind. `scripts/design-time-smoke.ps1` passed against the Oracle-compatible instance in the same session: the tooling added a migration, generated its script, applied it twice, listed it, scaffolded the schema back into a model that builds, and removed the migration.

The query steps were then added, taking the run to twenty-five, a date difference step to twenty-six, a retry step to twenty-seven, and a JSON column step to twenty-eight. They failed one of them on the default-mode instance only: `ADD_MONTHS` answered a timestamp at day precision there, so `AddMonths` returned midnight instead of the time it was given. The same statement kept the time on the Oracle-compatible instance, which is why a single instance would have hidden it. The provider now names `TIMESTAMPADD`, measured on both modes to clamp and to keep the time, and the step pins a month end so neither property can regress silently.

The retry step measured the classification twice in one run: a connection whose server side session was closed reported `6027` and left the driver reporting `Open`, so the strategy closed it, the retried statement answered on a new connection, and a statement against a table that does not exist reported `-2106` and reached the caller on the first attempt. Both halves pass on both compatibility modes.

The query steps also compare a byte array length: `LENGTHB` of a four byte value answered 4 on both modes, and `Length > 0` and `Count()` both reach the server as that function rather than as `LENGTH`, which measured the stored hexadecimal form instead (8 for a `VARBINARY(64)` holding four bytes).

The JSON column step measured what the server does with a document. `JSON` is a type a table accepts, the driver reads and writes it as text, and the server normalizes the document it stores, so a document written with spaces is read back without them. `JSON_VALUE` answers a member as text; the `RETURNING` form that would type it is rejected with `返回值的数据类型无效`, which is why a member that is not text is cast from that text. A boolean is the exception: the server stores it as `true`, and casting that text to `BIT` answers `无效的数字`, so it is compared with the text and answered as the condition shape the provider uses. A `TIMESTAMP` and a `DECIMAL` member both cast from their text, measured on the same run. `JSON_QUERY` answers an object or an array only and returns null for a scalar, and `ISJSON`, `JSON_TABLE` and the `doc.member` form are not defined at all, so the generator uses none of them. A comparison against a whole `JSON` column is a syntax error. For a change inside a document the framework reports a value for a path rather than the document, and `JSON_SET` is the function that writes it, measured to answer `{"a":2}` for `JSON_SET('{"a":1}', '$.a', 2)`; `JSON_MODIFY`, `JSON_PUT` and `JSON_MERGE` do not exist. `JSON_SET(doc, '$', '{"b":2}')` answers `"{\"b\":2}"`, the document as a quoted string, so the root path is written as the column value instead. An array element is named by `[n]` in the path, measured to answer `a` for `'$.Items[0].Sku'`. The step writes two documents, reads them back, filters on a text member and on a number member, changes one member and reads it back; it passed on both compatibility modes.

An ambient transaction was measured in the same runs, and the measurement changed the code: a write inside a `TransactionScope` that was never completed **survived**, because the framework only enlists the scope's transaction for a provider that says ambient transactions are supported, and the base default is no. The driver's own enlistment works — a connection enlisted through `EnlistTransaction` rolled back with the scope — so the provider now turns the switch on, after which the write did not survive, a completed scope kept its row, and an explicit transaction begun inside a scope is rejected with the framework's conflict error.

The decompiled reference provider is available for comparison at
`D:\Code\dotnet\efcore.dm-backup\artifacts\decompiled` (`DM.Microsoft.EntityFrameworkCore` 9.0.0.43760), outside this repository. It confirmed one decision and showed a better shape for another: it also turns on `SupportsAmbientTransactions`, and it reads a generated identity back with the server's `INSERT … RETURN <columns>` clause instead of a second statement guarded by `sql%ROWCOUNT`.

The migration lock was exercised with four migrations started at once from a database none had run against, and that is where its design changed. The provider held an exclusive table lock on the history table, which sounded right and guarded nothing: measured on 8.1.4.80, the lock was released by the first `CREATE TABLE` of the migration, because the server commits around DDL, and a lock taken without a transaction was released immediately — a second session took it in 2 milliseconds. The second session of the four therefore ran the migration body too and failed with `对象[DM_VALIDATION_JOURNAL]已存在`. `DBMS_LOCK` is the primitive that fits: `DBMS_LOCK.REQUEST(id, 6, wait, FALSE)` answered `0` to the first session, `1` to a second one asking to wait, and still `1` after the holder ran DDL, which is the property a migration lock needs; `DBMS_LOCK.RELEASE(id)` answered `0`. Two limits were measured with it: the id has to stay below 2^30, where the package answers `3`, its parameter error, and `DBMS_LOCK.ALLOCATE_UNIQUE` does not exist, so the id comes from a hash of the database name that is the same in every process. The history table's create was made tolerant of the same race, because it happens before the lock is taken.

The idempotent script was executed as a script, which is what a generation-only check leaves out. Split at the dialect's batch terminator, which is one command of the generator per piece, it ran three times against 8.1.4.80: from an empty database it created the table, its index and the history row; run again it changed nothing and left one history row; and from the state a deployment that failed halfway leaves, the table present and the history table gone, it recorded the migration without touching the table. The last run is the one that found a defect in the guards: they compared the catalog's owner column with `USER`, which is the account and not the schema, so with a connection string that sets `schema=` the guard answered `0` for a table that was there and the block ran `CREATE TABLE` against it. `SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')` answers the schema the session writes to, measured to be `HIDOS` where `USER` answered `SYSDBA`, and the guards compare against it now.

The full-text functions were measured before the translation was written. `FREETEXT` does not resolve on the server (`无法解析的成员访问表达式[FREETEXT]`). `CONTAINS` does, but only on a column that has a full-text index: without one the server answers `列[…]未编制全文索引或全文索引数据错误`, and against an expression rather than a column it answers `无效的CONTAINS列表达式`, which is why the translation requires a column and refuses anything else. The index exists as an object the schema owns, `CREATE CONTEXT INDEX "name" ON "table" ("column")` succeeded, but `CONTAINS` still answered zero for a row that matched in three arrangements — index after the insert, index before the insert, and index after a commit — and no entry point to populate it resolves on the server: `CTX_DDL.SYNC_INDEX`, `SP_REFRESH_TEXT_INDEX` and `CALL` forms of both failed, and the dictionary exposes `SYSCONTEXTINDEXES` as a table rather than a package. The provider therefore translates the predicate, which was executed against 8.1.4.80 as `WHERE CONTAINS("d"."Text", :term) = 1` and returned no rows without an error, and leaves the index and its population to the application.

The refusals of a parallel transaction that the second specification base produced were traced to the driver and then to the framework, and the driver is not the cause. A failing single statement, a failing two-statement command of the shape this provider uses to read a generated value back, and a successful two-statement command whose reader is dropped after one row were each run inside a transaction and rolled back; a new transaction could be started afterwards every time. A case of the base passes on its own where it fails in the class, so the refusals are state carried from one case to the next, and the connection the suite hands in is one it owns, so nothing closes it between cases.

A second specification base was tried in the same runs, `StoreGeneratedTestBase`, and it paid for itself before it was accepted: its model creates a table with a key whose column is a string, and the statement generator wrote an identity on that column, which the server rejected with `非法IDENTITY列类型`; the same measurement showed `TINYINT` and `SMALLINT` are not identity types either, while `INT`, `BIGINT`, `INTEGER`, `NUMBER` and `DECIMAL` are. A `short` key therefore has no strategy, a converted key whose column is a character type is refused while the model is built, and the generator writes `IDENTITY` only on a column type that accepts one. Of its 211 cases 98 passed; the failing groups are the store's refusal of a parallel transaction, a `NOT NULL` column the fixture expects the store to fill, and the read-only property rules, and the base is not part of the project until those are settled.

The microsecond part of a value was measured too: `DATEPART(microsecond, ...)` answers the count within the second, so a value of `.123456` answered 123456 while .NET keeps 456 as the member within the millisecond, and the provider reduces that part modulo a thousand. `EXTRACT(MICROSECOND ...)` is rejected there (`[EXTRACT]中的参数数据非法`), and `DATEPART(nanosecond, ...)` is a syntax error, so no nanosecond member is translated. The `out`-parameter overloads of `Add` and `AddHours` are left untranslated, because the framework cannot express an out parameter.

Batching was measured as well, which is why the two rows about it stay deferred: a command holding three statements runs all of them, but the driver answers the count of the last statement only — two updates of one row each answered 1, and a batch whose last statement matched no row answered 0 while the earlier ones had changed their rows — so a shared batch cannot attribute a row count to each command. A multi-row `INSERT ... VALUES (...), (...)` does run and answers the number of rows inserted.

The finer parts of `DATEDIFF` were measured in the same runs, and the measurement changed the surface. `MICROSECOND` is a part the server names — a pair two microseconds apart answered 2 — so `DateDiffMicrosecond` is published, with a ceiling: it answers `数据溢出` as soon as the two values differ in their hour part, because the hours are multiplied into an int of microseconds, so even two minutes from 03:59 to 04:01 overflowed while two seconds inside one hour did not, and within one hour the value overflows past about thirty-five minutes. `NANOSECOND` is a syntax error there, so no nanosecond form is published. `DATEDIFF` also refuses an interval where it takes a date (`参数不兼容`, measured for every unit), so the four overloads that take a `TimeSpan` are computed from the interval instead: each value is read with `EXTRACT` for its seconds, the two totals are subtracted and reduced to the unit, and the result is truncated with `TRUNC` rather than cast, because `CAST(2.9 AS INT)` answered 3 on this server while `TRUNC` answered 2, -2 and -1 for 2.9, -2.9 and -1.5, which is what the in-memory form does. Getting there exposed a second defect: an interval parameter was bound as a time, so an expression over it answered `[EXTRACT]中的参数数据非法` and subtracting one interval from another answered `数据类型不匹配`; the mapping now asks the driver for its interval type. The answers were compared with .NET against the server for a span of exactly two hours, one past a day, a fraction of an hour and a negative span, in both directions.

Cancellation was measured against the same server. A token cancelled before the call stops the query from starting, which the unit tests cover with a command double. What the real driver does with `DbCommand.Cancel` is now recorded: it did not interrupt a running statement, it blocked until the statement ended — about eleven seconds for a statement with an eight second command timeout — and the pending read then threw a `NullReferenceException` from the driver rather than `OperationCanceledException`. The connection stayed usable and answered the next statement. A statement is therefore bounded by the command timeout rather than by the token.

The date and time members of `DateOnly` and `TimeOnly` were measured in the same runs. On a date, `+ n` adds days, `ADD_MONTHS` clamps the way .NET does (`2024-02-29` plus twelve months answered `2025-02-28`), and `TRUNC` then a cast to `DATE` builds a date from a timestamp. The day number does not come from a count to year one: the server's calendar skips the ten days of the Gregorian reform, so `DATEDIFF(DAY, DATE '1582-10-04', DATE '1582-10-15')` answered 1 and a count from `0001-01-01` answered 739619 for `2026-01-02` where .NET answers 739617; anchored on `1970-01-01` the count answered 739617. On a time of day, `TIMESTAMPADD` of a second count, cast back to `TIME`, wraps past midnight — `23:00:00` plus two hours answered `01:00` — and a fractional count is truncated, which is why an addition is expressed in seconds: an hour count of 1.5 answered one whole hour. A negative interval signs every part: `-1 2:3:4` answered -1 days and -2 hours, and `-0 00:00:01.5` answered -1.5 seconds.

The interval between two timestamps was measured in the same runs. Subtracting two dates answers a number of days, and `NUMTODSINTERVAL` of that number scaled to seconds turns it into an interval — on both compatibility modes, which the migration notes had placed the function behind: 86400 seconds answered one day, a fractional count kept its fraction, and a difference that runs backwards answered a negative interval. Reading it is not the same as reading an interval column: a column arrives as a `TimeSpan`, an expression as the driver's own interval type, so reading it as a time span answered `Object must implement IConvertible`. The parts of that type are the magnitude only, with the sign in the rendered value, and the fraction is in microseconds — `-1 1:1:1.333` answered 1 day, 1 hour, 1 minute, 1 second and 333000 — so the value is summed as ticks and negated from the sign, which matched .NET to the tick in both directions.

The count of a byte array was measured in both forms: `LENGTHB` answered 4 for a four-byte `VARBINARY(64)` and 5 for a five-byte `BLOB`, where `LENGTH` answered 8 for the first — the length of the hexadecimal form the server stores — and a null array answered a null count. The framework sends `Payload.Length > 0` and `Payload.Any()` to a translator as a collection method, but a comparison against another count as its own node, which the translating visitor now reads, so every form of the count translates.

The members of an interval that are not one of its parts were measured as well: `Ticks` is the second count scaled by ten million, `Milliseconds` is the seconds part scaled by a thousand and reduced modulo a thousand, which answered -500 for -5.5 seconds where a signed remainder is what .NET keeps, and `Duration` cannot be `ABS` over an interval (`参数不兼容`) so it is built as a new interval from the absolute second count. All three answered the values .NET answers, for a negative and for a positive interval.

Still unverified by a live run, and therefore still recorded as unverified in the matrix: the PostgreSQL-compatible mode. The idempotent script has since been executed as a script, and the migration lock with four deployments starting at once, which is recorded below.

### What the specification suite measured

`tests/DM.EntityFrameworkCore.SpecificationTests` was run against the Oracle-compatible instance on 2026-11-11: 32 cases of `BuiltInDataTypesDmTest`, 31 passed, and the one that did not is in the [skip ledger](../tests/DM.EntityFrameworkCore.SpecificationTests/Skipped.md) with the measurement that decided it. That case exposed a mapping rule which is now in place: a byte array longer than a row can hold is a `BLOB` rather than a `VARBINARY(n)` column that cannot store its own declared length, measured on a one column table where 7500 bytes were stored and 8000 were refused with `记录超长`. A key or an index keeps its bounded type even then, because the server refuses to index a large object. The store creates the tables of its own model and drops only those, because the database is one an application already uses and Dameng has no `CREATE DATABASE` to give the suite a fresh one. The project is not part of the solution and its cases do not run in CI; the pipeline builds it and runs the one part that needs no server, the skip ledger guard.

`StoreGeneratedDmTest` joined the project on 2026-11-11 and its 211 cases ran against the same instance: all passed, and the ledger gained no entry. It brings the store generated values of the model under the suite: identity keys, column defaults read back after an insert, the before-save and after-save behaviours of a generated value, and the wrapped and converted key types. That run closed the last gap the first trial had exposed, because the identity convention now reads the column a converter produces rather than the property's CLR type: an enum or a wrapped integer key is an identity column on its `INT` column, while an integer key converted onto a character column stays application supplied. One fixture choice is recorded beside the test rather than in the ledger: the backing field case gives its generated values as column defaults instead of a computed column, because a table whose only writable column is the identity is inserted with `DEFAULT VALUES`, and measured on 8.1.4.80 that expansion includes a virtual column and is refused with `不允许对虚拟列执行INSERT操作`. The provider's computed column support is covered by `DmComputedColumnTests` and the acceptance program instead.

`NorthwindWhereQueryDmTest` joined the project on the same day and its 408 cases ran against the instance: all passed, and the ledger gained no entry. Nine of them compare two constructed tuples or anonymous objects, which the relational pipeline does not translate for any provider, so the class asserts that failure the way the SQL Server test does (EF Core issue #14672) instead of expecting rows. The set paid for itself twice. Its model declares an index on the self-referencing manager key, and the server indexes a foreign key's columns itself: `CREATE INDEX` on that column then answered `此列列表已索引`, so a self-referencing foreign key is now added after the indexes of a script, which the server accepts and which leaves both the model's index and the constraint's own index in place. It also compares a boolean column with a condition, `Discontinued = (ProductID > 50)`, which the server rejects with `语法分析出错`; a condition used as an operand is now written as the `CASE WHEN ... THEN 1 ELSE 0 END` the projection rule already used.

`NorthwindJoinQueryDmTest` ran its 132 cases against the same instance: 126 passed, four are skipped by the shipped base itself (`#30677` and `#35028`), and one case is in the ledger as a server ordering limitation — the statement is the same `CROSS APPLY` the SQL Server test asserts, but the server does not keep the ordering of the inner subquery inside one customer, while the rows are the same set. The run found two defects that were fixed: a collection joined in a query was written as a bare `VALUES` inside a `UNION ALL`, which the server rejects with `语法分析出错` and which is now a `SELECT ... FROM DUAL` per row joined by `UNION ALL`; and a tautological join predicate was written as a bare `1`, which the server rejects with `查询使用值表达式作为过滤条件` and which is now compared with `1` like the statement's own predicate.

`NorthwindSelectQueryDmTest` ran its 372 cases against the same instance: 364 passed, eight did not (four cases in their two forms), and the shipped base skips none of them. The four are in the ledger as server limitations of two kinds: a nested `APPLY` whose inner query references the outermost table answers `不支持的语句类型` — measured on 8.1.4.80, the same statement without that reference parsed and answered — and one case picks a row without an ordering, so the server answered another row of the same set. A fifth case, the one that asked for the first row of a keyless entity, did not pass in that run because this project's store left the keyless table empty; the fixture now seeds it, and the case passes. The run found three defects that were fixed: a nested `Take` composed through `LEAST` left the function without a type mapping when an argument was a parameter; a condition in a `CASE` result was written as a bare condition, which the server rejects at the comparison; and a difference of two dates was translated although the server answers it with a number of days, which cannot be read back as a time span, so it is now projected as the date and subtracted by the client. The whole project then ran 1138 of 1155 cases against the instance: 13 failed, each in the ledger, and 4 are skipped upstream.

`NorthwindGroupByQueryDmTest` ran its 512 cases against the same instance: 502 passed, six did not (three cases in their two forms), and the shipped base skips four. The three are in the ledger under one server limitation: SQL Server accepts an outer reference where this server needs the referenced column to be grouped. The run found one defect that was fixed: a null-safe comparison of an `EXTRACT` key was written `MONTH IS NULL OR "OrderDate" IS NULL`, because the keyword argument propagated nullability like a value, and the server rejects it with `无效的列名[MONTH]`; a fragment no longer propagates.

`NorthwindAggregateOperatorsQueryDmTest` ran its 422 cases against the same instance: 420 passed and two did not (one case in its two forms), which is the ledger's one remaining `APPLY` case. The run found no provider defect. Two cases it did touch are worth recording. An average of nested averages is computed by the server to its own decimal scale, so the class compares it at three decimal places, which is what the SQL Server test does for the same reason; and a case that reads a keyless set ran into this project's store rather than the provider: the reference store is created from a script that fills the keyless tables the model maps, while this store is created from the model and only the seeded entities have a key, so the fixture now projects the keyless rows from the keyed ones. The whole project then ran 2062 of 2089 cases against the instance: 19 failed, each in the ledger, and 8 are skipped upstream.

The bound a bounded column can hold was measured while the max length case was worked on, and it is smaller than the type name suggests. On 8.1.4.80 a single value above about 4000 bytes is refused with `记录超长` whatever its column declares and however it is supplied: a 4000 character value in an `NVARCHAR2(8188)` column was refused through a parameter, a literal of 3500 characters was stored and 3900 was not, a 9000 byte value in a `VARBINARY(9000)` column was refused, and a 2000 character value in the same column was stored. So the type's declared length is a ceiling the server accepts in DDL, not a length the row can fill, and a value that needs more has to be a character or binary large object: the specification case that stores a 9000 character string passes because a string past the provider's ceiling is already an `NCLOB`, and the one that stores 9000 bytes started passing when a byte array past the ceiling became a `BLOB`. A column declared with a length between that bound and the ceiling is therefore legal and unusable at its full width, which is a property of the store and is why the ledger's remaining case cannot be written differently.

The runs found these defects that no shape test could have found, and each is fixed:

- A byte array used as a key or an index was mapped to `BLOB`, and the server refuses to create an index on a large object column (`试图在BLOB/CLOB/用户自定义数据类型列[Id]上建索引`), so the table could not be created at all. It now takes the bounded `VARBINARY(900)` a key needs, measured to be accepted as a primary key.
- A byte array literal was written as `X'0414'`, which a bounded binary column accepts and a `BLOB` column rejects with `数据类型不匹配`. It is now `HEXTORAW('0414')`, which every binary type accepts.
- An insert that carries an explicit identity value was refused with `仅当指定列列表，且SET IDENTITY_INSERT为ON时，才能对自增列赋值`. The statement is now bracketed with `SET IDENTITY_INSERT ... ON` and `OFF` in the same command.
- A boolean column used directly as a predicate was rejected with `查询使用值表达式作为过滤条件`, and so was the same column under `NOT` or beside a comparison. The predicate is now rewritten so that every boolean it contains is compared with `1`, which the same server answered.
- An ANSI string declared `HasMaxLength(3)` was mapped to `VARCHAR2(3)`, which counts bytes, so a non-ASCII value the model says fits overflowed. The ANSI types now name the character unit and the model factory reads it back, so the same column comes out of a scaffold as it went in.

The two gaps the first run left, the byte length of an ANSI string and a boolean column used directly as a predicate, are fixed as the last two bullets record. The one case in the ledger is a server limitation rather than a provider defect, and cannot be written differently.

### When no live environment is available

Work is not reclassified as complete. Rules:

- The phase remains open and is marked as awaiting live verification.
- The release decision is made by a human, and the release notes state what was not covered.
- Live CI jobs are never marked optional or allowed to fail silently.

`samples/DmProviderValidation` is the end-to-end acceptance program. It covers connection open and close, synchronous and asynchronous commands, cancellation, CRUD, transaction commit and rollback, savepoints, optimistic concurrency, identity and sequence readback, `ExecuteUpdate` and `ExecuteDelete`, type round trips, and left and right joins, and one migration applied twice. Each step reports on its own, so one failure does not hide the rest.

Its `query:` steps ask the server a question and compare the answer with the value .NET computes from the rows the step wrote: the string functions, the date parts and the arithmetic (including a month end, where a clamp and a time of day are both asserted), the date differences, the math functions, paging and ordering, grouping and aggregates, null semantics, and a boolean condition in a projection. A translation that is generated but wrong passes every shape test and fails here; both defects found so far were found by these steps or by the step next to them.

Migrations are part of it as far as one migration can be applied and applied again: the sample ships a migration and its snapshot for a table of its own, applies it, reads a row back through the model, and applies it a second time against a database where every statement has already run. Applying a migration *down*, a custom history table and concurrent migration locking are not covered: they are exercised through the `dotnet ef` commands that the roadmap names as exit criteria of the migrations phase.

It reads the connection string from its first argument, from `ConnectionStrings__Dm`, or from user secrets (`dotnet user-secrets set "ConnectionStrings:Dm" "<connection string>" --project samples/DmProviderValidation`), in that order of specificity, so no credential has to sit in the repository; `--secrets=<key>` reads `ConnectionStrings:<key>` for a second instance. It never prints the string. It creates its own prefixed tables through the relational database creator rather than through `EnsureCreated`, because a Dameng database is made with the `dminit` tool before a connection can reach it and the catalog may already hold tables the run does not own. Each run marks its rows with an identifier, isolates them from another run, drops only the tables it created, and returns a non-zero exit code when any step fails. `--keep` leaves the tables behind for inspection. `--dry-run` builds the model and generates the schema script without a database, prints the declared steps, and returns a non-zero exit code when the model no longer produces the tables and the sequence a run needs. It also compares the shipped migration snapshot with the model and generates the idempotent script of that migration, failing when the snapshot no longer matches or the script has lost its guards or gained a statement Dameng does not define; that is the check a build machine can run, and it is why the sample does not rot between the runs that have an instance.

## 6. Definition of done

A dialect capability or pipeline feature is complete only when all five hold:

1. A test failed before the implementation and passes after.
2. The test places the expression where server execution is required.
3. The specification test suite passes for that area, or the entry is in the ledger with an allowed category.
4. Live database execution passed, or the work is explicitly recorded as awaiting live verification with a reason.
5. Any API difference is recorded in [Version Matrix](version-matrix.md).

## 7. What is not measured

Test count is not a progress metric. The specification suite has a fixed set of cases defined upstream; the metric is the shrinking skip ledger.

New T2 tests are written only when:

- the translation is Dameng-specific and not covered by the specification suite,
- a specification test failure requires a minimal regression case, or
- the code under test is a pure function in `Dialect/`.

There is no coverage threshold and no test is written to raise coverage.