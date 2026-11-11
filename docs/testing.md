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

The query steps were then added, taking the run to twenty-five, a date difference step to twenty-six, a retry step to twenty-seven, and a JSON column step to twenty-eight. They failed one of them on the default-mode instance only: `ADD_MONTHS` answered a timestamp at day precision there, so `AddMonths` returned midnight instead of the time it was given. The same statement kept the time on the Oracle-compatible instance, which is why a single instance would have hidden it. The provider now names `TIMESTAMPADD`, measured on both modes to clamp and to keep the time, and the step pins a month end so neither property can regress silently.

The retry step measured the classification twice in one run: a connection whose server side session was closed reported `6027` and left the driver reporting `Open`, so the strategy closed it, the retried statement answered on a new connection, and a statement against a table that does not exist reported `-2106` and reached the caller on the first attempt. Both halves pass on both compatibility modes.

The query steps also compare a byte array length: `LENGTHB` of a four byte value answered 4 on both modes, and `Length > 0` and `Count()` both reach the server as that function rather than as `LENGTH`, which measured the stored hexadecimal form instead (8 for a `VARBINARY(64)` holding four bytes).

The JSON column step measured what the server does with a document. `JSON` is a type a table accepts, the driver reads and writes it as text, and the server normalizes the document it stores, so a document written with spaces is read back without them. `JSON_VALUE` answers a member as text; the `RETURNING` form that would type it is rejected with `返回值的数据类型无效`, which is why a member that is not text is cast from that text. A boolean is the exception: the server stores it as `true`, and casting that text to `BIT` answers `无效的数字`, so it is compared with the text and answered as the condition shape the provider uses. A `TIMESTAMP` and a `DECIMAL` member both cast from their text, measured on the same run. `JSON_QUERY` answers an object or an array only and returns null for a scalar, and `ISJSON`, `JSON_TABLE` and the `doc.member` form are not defined at all, so the generator uses none of them. A comparison against a whole `JSON` column is a syntax error. For a change inside a document the framework reports a value for a path rather than the document, and `JSON_SET` is the function that writes it, measured to answer `{"a":2}` for `JSON_SET('{"a":1}', '$.a', 2)`; `JSON_MODIFY`, `JSON_PUT` and `JSON_MERGE` do not exist. `JSON_SET(doc, '$', '{"b":2}')` answers `"{\"b\":2}"`, the document as a quoted string, so the root path is written as the column value instead. An array element is named by `[n]` in the path, measured to answer `a` for `'$.Items[0].Sku'`. The step writes two documents, reads them back, filters on a text member and on a number member, changes one member and reads it back; it passed on both compatibility modes.

An ambient transaction was measured in the same runs, and the measurement changed the code: a write inside a `TransactionScope` that was never completed **survived**, because the framework only enlists the scope's transaction for a provider that says ambient transactions are supported, and the base default is no. The driver's own enlistment works — a connection enlisted through `EnlistTransaction` rolled back with the scope — so the provider now turns the switch on, after which the write did not survive, a completed scope kept its row, and an explicit transaction begun inside a scope is rejected with the framework's conflict error.

A second specification base was tried in the same runs, `StoreGeneratedTestBase`, and it paid for itself before it was accepted: its model creates a table with a key whose column is a string, and the statement generator wrote an identity on that column, which the server rejected with `非法IDENTITY列类型`; the same measurement showed `TINYINT` and `SMALLINT` are not identity types either, while `INT`, `BIGINT`, `INTEGER`, `NUMBER` and `DECIMAL` are. A `short` key therefore has no strategy, a converted key whose column is a character type is refused while the model is built, and the generator writes `IDENTITY` only on a column type that accepts one. Of its 211 cases 98 passed; the failing groups are the store's refusal of a parallel transaction, a `NOT NULL` column the fixture expects the store to fill, and the read-only property rules, and the base is not part of the project until those are settled.

The microsecond part of a value was measured too: `DATEPART(microsecond, ...)` answers the count within the second, so a value of `.123456` answered 123456 while .NET keeps 456 as the member within the millisecond, and the provider reduces that part modulo a thousand. `EXTRACT(MICROSECOND ...)` is rejected there (`[EXTRACT]中的参数数据非法`), and `DATEPART(nanosecond, ...)` is a syntax error, so no nanosecond member is translated. The `out`-parameter overloads of `Add` and `AddHours` are left untranslated, because the framework cannot express an out parameter.

Batching was measured as well, which is why the two rows about it stay deferred: a command holding three statements runs all of them, but the driver answers the count of the last statement only — two updates of one row each answered 1, and a batch whose last statement matched no row answered 0 while the earlier ones had changed their rows — so a shared batch cannot attribute a row count to each command. A multi-row `INSERT ... VALUES (...), (...)` does run and answers the number of rows inserted.

The finer parts of `DATEDIFF` were measured in the same runs, and the measurement changed the surface. `MICROSECOND` is a part the server names — a pair two microseconds apart answered 2 — so `DateDiffMicrosecond` is published, with a ceiling: it answers `数据溢出` as soon as the two values differ in their hour part, because the hours are multiplied into an int of microseconds, so even two minutes from 03:59 to 04:01 overflowed while two seconds inside one hour did not, and within one hour the value overflows past about thirty-five minutes. `NANOSECOND` is a syntax error there, so no nanosecond form is published. `DATEDIFF` also refuses an interval where it takes a date (`参数不兼容`, measured for every unit), so the four overloads that take a `TimeSpan` stopped being translated: a query reports them as untranslatable instead of sending a statement the server rejects, while the in-memory form still computes them.

Cancellation was measured against the same server. A token cancelled before the call stops the query from starting, which the unit tests cover with a command double. What the real driver does with `DbCommand.Cancel` is now recorded: it did not interrupt a running statement, it blocked until the statement ended — about eleven seconds for a statement with an eight second command timeout — and the pending read then threw a `NullReferenceException` from the driver rather than `OperationCanceledException`. The connection stayed usable and answered the next statement. A statement is therefore bounded by the command timeout rather than by the token.

The date and time members of `DateOnly` and `TimeOnly` were measured in the same runs. On a date, `+ n` adds days, `ADD_MONTHS` clamps the way .NET does (`2024-02-29` plus twelve months answered `2025-02-28`), and `TRUNC` then a cast to `DATE` builds a date from a timestamp. The day number does not come from a count to year one: the server's calendar skips the ten days of the Gregorian reform, so `DATEDIFF(DAY, DATE '1582-10-04', DATE '1582-10-15')` answered 1 and a count from `0001-01-01` answered 739619 for `2026-01-02` where .NET answers 739617; anchored on `1970-01-01` the count answered 739617. On a time of day, `TIMESTAMPADD` of a second count, cast back to `TIME`, wraps past midnight — `23:00:00` plus two hours answered `01:00` — and a fractional count is truncated, which is why an addition is expressed in seconds: an hour count of 1.5 answered one whole hour. A negative interval signs every part: `-1 2:3:4` answered -1 days and -2 hours, and `-0 00:00:01.5` answered -1.5 seconds.

The interval between two timestamps was measured in the same runs. Subtracting two dates answers a number of days, and `NUMTODSINTERVAL` of that number scaled to seconds turns it into an interval — on both compatibility modes, which the migration notes had placed the function behind: 86400 seconds answered one day, a fractional count kept its fraction, and a difference that runs backwards answered a negative interval. Reading it is not the same as reading an interval column: a column arrives as a `TimeSpan`, an expression as the driver's own interval type, so reading it as a time span answered `Object must implement IConvertible`. The parts of that type are the magnitude only, with the sign in the rendered value, and the fraction is in microseconds — `-1 1:1:1.333` answered 1 day, 1 hour, 1 minute, 1 second and 333000 — so the value is summed as ticks and negated from the sign, which matched .NET to the tick in both directions.

The count of a byte array was measured in both forms: `LENGTHB` answered 4 for a four-byte `VARBINARY(64)` and 5 for a five-byte `BLOB`, where `LENGTH` answered 8 for the first — the length of the hexadecimal form the server stores — and a null array answered a null count. The framework sends `Payload.Length > 0` and `Payload.Any()` to a translator as a collection method, but a comparison against another count as its own node, which the translating visitor now reads, so every form of the count translates.

The members of an interval that are not one of its parts were measured as well: `Ticks` is the second count scaled by ten million, `Milliseconds` is the seconds part scaled by a thousand and reduced modulo a thousand, which answered -500 for -5.5 seconds where a signed remainder is what .NET keeps, and `Duration` cannot be `ABS` over an interval (`参数不兼容`) so it is built as a new interval from the absolute second count. All three answered the values .NET answers, for a negative and for a positive interval.

Still unverified by a live run, and therefore still recorded as unverified in the matrix: the `EF.Functions` overloads that take a `TimeSpan`, the idempotent script executed as a script rather than generated, the migrations lock held by two concurrent processes, and the PostgreSQL-compatible mode.

### What the specification suite measured

`tests/DM.EntityFrameworkCore.SpecificationTests` was run against the Oracle-compatible instance on 2026-11-11: 31 cases of `BuiltInDataTypesDmTest`, 29 passed, and the two that did not are in the [skip ledger](../tests/DM.EntityFrameworkCore.SpecificationTests/Skipped.md) with the measurement that decided each. The store creates the tables of its own model and drops only those, because the database is one an application already uses and Dameng has no `CREATE DATABASE` to give the suite a fresh one. The project is not part of the solution and its cases do not run in CI; the pipeline builds it and runs the one part that needs no server, the skip ledger guard.

The suite found three defects that no shape test could have found, and each is fixed:

- A byte array used as a key or an index was mapped to `BLOB`, and the server refuses to create an index on a large object column (`试图在BLOB/CLOB/用户自定义数据类型列[Id]上建索引`), so the table could not be created at all. It now takes the bounded `VARBINARY(900)` a key needs, measured to be accepted as a primary key.
- A byte array literal was written as `X'0414'`, which a bounded binary column accepts and a `BLOB` column rejects with `数据类型不匹配`. It is now `HEXTORAW('0414')`, which every binary type accepts.
- An insert that carries an explicit identity value was refused with `仅当指定列列表，且SET IDENTITY_INSERT为ON时，才能对自增列赋值`. The statement is now bracketed with `SET IDENTITY_INSERT ... ON` and `OFF` in the same command.
- A boolean column used directly as a predicate was rejected with `查询使用值表达式作为过滤条件`, and so was the same column under `NOT` or beside a comparison. The predicate is now rewritten so that every boolean it contains is compared with `1`, which the same server answered.
- An ANSI string declared `HasMaxLength(3)` was mapped to `VARCHAR2(3)`, which counts bytes, so a non-ASCII value the model says fits overflowed. The ANSI types now name the character unit and the model factory reads it back, so the same column comes out of a scaffold as it went in.

The two remaining provider gaps the suite exposed are the byte length of an ANSI string and a boolean column used directly as a predicate; both are recorded in the ledger with the measurement and the fix each needs.

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