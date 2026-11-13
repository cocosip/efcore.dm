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

T3 is a coverage tier, not a feature decision. A capability's own row in the [Capability Matrix](capability-matrix.md)
says whether it is supported; not inheriting a base does not make the capability unsupported, and
inheriting one does not prove it.

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

The specification package is built for the framework of its EF Core line, which is not always the provider's: 9.0.22 ships `net9.0` assets while the provider targets `net8.0`, so on `efcore/9.0` the specification project sets its own `TargetFramework` to `net9.0`. The test project follows the package, never the other way round.

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

When a translation is unclear, the reference provider is used as a black-box: the same model and query,
SQL printed by both, parameters and whitespace normalized, differences justified. A match is not proof of
correctness; a live run is the final standard. The reference is not a build dependency and its decompiled
source is never copied (see [Provenance](provenance.md)).

The comparison ran twice, on 2026-11-11. On SQL only, 23 queries produced 12 differences, none a defect
here (escaping, EF Core 10 parameter names and collection parameterisation, `LENGTHB` for byte arrays,
`EXTRACT` for interval parts). Executed against the server, 108 answers matched and 8 differed: six were
queries this provider answers and the reference cannot, one was a member the reference mistranslates
(`Date` without truncation), and one was a row both refuse. The record is kept so a future difference is a
difference from a known baseline; no behaviour changed because of it.

## 5. Live database matrix

| Dimension | Values |
|---|---|
| Dameng server | Versions in production use. First reached on 2026-11-11: DM 8.1.4.80 |
| Compatibility mode | The default instance and the Oracle-compatible one both ran the console on 2026-11-11, as reported by the environment's operator; the dialect does not branch on the mode (D4). PostgreSQL-compatible mode is still a P6 exit criterion and has not been exercised |
| Operating system | Windows x64, Linux x64 |
| Target framework | `net8.0` on the EF Core 8/9 branches, `net10.0` on EF Core 10, `net11.0` from EF Core 11. The specification project runs on the framework its package ships: `net9.0` on `efcore/9.0` |
| Driver | The `DM.DmProvider` version the package depends on, which is the floor the provider is compiled against; EF Core 10 consumes its `net9.0` asset |

### What the live runs measured

The acceptance console has 33 steps on the EF Core 9 branch (34 before the `right join` step was dropped, because
`RightJoinExpression` does not exist on this line) and passed on DM 8.1.4.80 in both compatibility modes on 2026-11-13.
It covers the query surface, the retry and cancellation behaviour, JSON, intervals, computed columns,
ambient transactions and the migrations steps, including four migrations started at once. The steps do
not only re-run what the unit tests already say: they are where the server's shape is observed, and each
step pins the measurement that decided a translation. The console leaves no table, sequence or history
row behind, and `scripts/design-time-smoke.ps1` covers `dotnet ef` end to end against a live instance.

The server accepts a declared length larger than a row can hold: on 8.1.4.80 a single value above about
4000 bytes is refused with `记录超长` whatever the column declares. A string or byte array past the
provider's ceiling is therefore mapped to `NCLOB`/`BLOB`, which is why the specification case that stores
9000 characters and the one that stores 9000 bytes pass.

### What the specification suite measured

`tests/DM.EntityFrameworkCore.SpecificationTests` runs against a live instance, is not part of the
solution and does not run in CI; the pipeline builds it and runs the skip ledger guard.

| Suite | Results | Result |
|---|---|---|
| `BuiltInDataTypesDmTest` | 31 | 30 pass, 1 skipped |
| `StoreGeneratedDmTest` | 211 | all pass |
| `NorthwindWhereQueryDmTest` | 476 | 475 pass, 1 skipped (the ledger case) |
| `NorthwindNavigationsQueryDmTest` | 146 | all pass |
| `NorthwindMiscellaneousQueryDmTest` | 954 | 947 pass, 7 skipped (server-ordering and limit cases) |
| `NorthwindBulkUpdatesDmTest` | 164 | 156 pass, 8 skipped (three upstream skips and five ledger cases) |
| `PrimitiveCollectionsQueryDmTest` | 242 | 222 pass, 20 skipped (one upstream skip and 19 ledger cases) |
| `NorthwindJoinQueryDmTest` | 121 | 118 pass, 3 skipped (two upstream, one ledger case) |
| `NorthwindSelectQueryDmTest` | 360 | 356 pass, 4 skipped (the four ledger cases) |
| `NorthwindGroupByQueryDmTest` | 507 | 500 pass, 7 skipped (four upstream, three ledger cases) |
| `NorthwindAggregateOperatorsQueryDmTest` | 420 | 418 pass, 2 skipped (the two ledger cases) |
| `NorthwindIncludeQueryDmTest` | 232 | 230 pass, 2 skipped (two empty-list ordering cases) |
| `UpdatesDmTest` | 36 | 35 pass, 1 skipped (filtered unique index) |
| `UpdatesDmTest` | 36 | 35 pass, 1 skipped (filtered unique index) |
| `MigrationsDmTest` | 131 | 112 pass, 19 skipped |
| `SkipLedgerTests` | 1 | all pass |

Whole project: 4032 results, 3957 passed, 75 skipped, 0 failed. A skipped theory is reported once even
though its two async forms are both data rows, so the 75 cases in the ledger appear as 75 skipped
results: 63 this project declares, plus the 12 the shipped EF Core 9 base skips. The specification project
runs its collections serially (`xunit.runner.json`) because
every fixture shares one database and a fixture that recreates the Northwind tables would otherwise race
another class. The store drops the objects its model declares; the updates fixture and the migrations
suite own a schema of their own (`DM_SPEC_UPDATES`, `DM_SPEC_MIGRATIONS`), and the migrations class is
serialized because the database is shared with an application that has 247 tables of its own. The
primitive-collection fixture runs on the default-compatibility instance because its seed writes an empty
string into a required column, which Oracle-compatible mode stores as null and the column refuses. Every
case that does not run or does not pass is in
[Skipped.md](../tests/DM.EntityFrameworkCore.SpecificationTests/Skipped.md) with its measurement.

### Specification bases inherited

All T3 specification bases are now inherited: `NorthwindBulkUpdatesRelationalTestBase` and
`PrimitiveCollectionsQueryRelationalTestBase` were added on 2026-11-13 beside the query sets, `Updates`
and `Migrations`. The T3 suites are green with only recorded skips, so T3 is complete as a coverage
tier; the skips are the server boundaries and translation gaps listed in the ledger, not unrecorded
failures. `EF.Parameter` collection translation is implemented and the four cases that depended on it
now run; the remaining skips are server limits or ordering assertions.

The reference provider has no specification suite, and its `TranslatePrimitiveCollection` handles
collection parameters only, so its code is a translation reference for individual gaps rather than a T3
coverage template. This provider's mapped-collection path is ahead of it.

### Server boundaries that no provider change can move

The cases that do not pass are recorded with their measurement in the skip ledger. The boundaries they
share are these, so a later reader does not mistake them for work left undone:

| The server does not provide | Measured answer | How the provider responds |
|---|---|---|
| A filtered index | `语法分析出错` for `CREATE INDEX … WHERE` | `CreateIndexOperation` with a filter throws with the measurement |
| Moving a table or a sequence to another schema | no statement for either move | the generator refuses and names the reason |
| Restarting a sequence | `未指定 ALTER SEQUENCE 的选项` | `RestartSequenceOperation` throws with the measurement |
| Turning an existing column into an identity column | the server refuses the alteration in place | the operation is refused rather than silently doing nothing |
| Making a column `NOT NULL` while it holds nulls | the server refuses the alteration | the migration fails on the server, as it does for SQL Server |
| Changing a string column to JSON | `数据类型的变更无效` | the case is in the ledger; the provider does not hide the error |
| A spatial column (`GEOMETRY`, `SDO_GEOMETRY`, `ST_GEOMETRY`, `POINT`) | `非法的基类名[…]` for each; `SF_GEOMETRY` does not resolve | spatial support is `Not applicable` until a server version provides a geometry type |
| `FREETEXT`, a `CONTAINS` language term, a `NANOSECOND` part | the names do not resolve on 8.1.4.80 (`语法分析出错` or `无法解析的成员访问表达式`) | the methods exist for parity with the reference provider and are covered by shape tests |
| A flag that says a column is virtual, or its collation | `DATA_DEFAULT` is the same for a virtual column and a default; no collation column | scaffolding keeps a default as a default and leaves the computed expression and collation unrecoverable |
| Cancelling a running command | the driver's `Cancel` is the only hook | the provider registers `Cancel` on the token, as the reference provider does |

The runs found these defects that no shape test could have found, and each is fixed:

- A byte array used as a key or an index was mapped to `BLOB`, and the server refuses to create an index on a large object column (`试图在BLOB/CLOB/用户自定义数据类型列[Id]上建索引`), so the table could not be created at all. It now takes the bounded `VARBINARY(900)` a key needs, measured to be accepted as a primary key.
- A byte array literal was written as `X'0414'`, which a bounded binary column accepts and a `BLOB` column rejects with `数据类型不匹配`. It is now `HEXTORAW('0414')`, which every binary type accepts.
- An insert that carries an explicit identity value was refused with `仅当指定列列表，且SET IDENTITY_INSERT为ON时，才能对自增列赋值`. The statement is now bracketed with `SET IDENTITY_INSERT ... ON` and `OFF` in the same command.
- A boolean column used directly as a predicate was rejected with `查询使用值表达式作为过滤条件`, and so was the same column under `NOT` or beside a comparison. The predicate is now rewritten so that every boolean it contains is compared with `1`, which the same server answered.
- An ANSI string declared `HasMaxLength(3)` was mapped to `VARCHAR2(3)`, which counts bytes, so a non-ASCII value the model says fits overflowed. The ANSI types now name the character unit and the model factory reads it back, so the same column comes out of a scaffold as it went in.

The two gaps the first run left, the byte length of an ANSI string and a boolean column used directly as a predicate, are fixed as the last two bullets record. The one case in this set's ledger is a server limitation rather than a provider defect, and is now marked skipped in the test class instead of failing the run.

### When no live environment is available

Work is not reclassified as complete. Rules:

- The phase remains open and is marked as awaiting live verification.
- The release decision is made by a human, and the release notes state what was not covered.
- Live CI jobs are never marked optional or allowed to fail silently.

`samples/DmProviderValidation` is the end-to-end acceptance program. It covers connection open and close, synchronous and asynchronous commands, cancellation, CRUD, transaction commit and rollback, savepoints, optimistic concurrency, identity and sequence readback, `ExecuteUpdate` and `ExecuteDelete`, type round trips, and the left join, and one migration applied twice. Each step reports on its own, so one failure does not hide the rest.

Its `query:` steps ask the server a question and compare the answer with the value .NET computes from the rows the step wrote: the string functions, the date parts and the arithmetic (including a month end, where a clamp and a time of day are both asserted), the date differences, the math functions, paging and ordering, grouping and aggregates, null semantics, and a boolean condition in a projection. A translation that is generated but wrong passes every shape test and fails here; both defects found so far were found by these steps or by the step next to them.

Migrations are part of it as far as one migration can be applied and applied again: the sample ships a migration and its snapshot for a table of its own, applies it, reads a row back through the model, and applies it a second time against a database where every statement has already run. Concurrent migration locking is exercised by the console's concurrent-deployment step. Applying a migration *down* and a custom history table are not covered by the console; the `dotnet ef` add/update/list/remove/scaffold path is covered by `scripts/design-time-smoke.ps1`, and the migrations specification suite has no infrastructure base yet.

It reads the connection string from its first argument, from `ConnectionStrings__Dm`, or from user secrets (`dotnet user-secrets set "ConnectionStrings:Dm" "<connection string>" --project samples/DmProviderValidation`), in that order of specificity, so no credential has to sit in the repository; `--secrets=<key>` reads `ConnectionStrings:<key>` for a second instance. It never prints the string. It creates its own prefixed tables through the relational database creator rather than through `EnsureCreated`, because a Dameng database is made with the `dminit` tool before a connection can reach it and the catalog may already hold tables the run does not own. Each run marks its rows with an identifier, isolates them from another run, drops only the tables it created, and returns a non-zero exit code when any step fails. `--keep` leaves the tables behind for inspection. `--dry-run` builds the model and generates the schema script without a database, prints the declared steps, and returns a non-zero exit code when the model no longer produces the tables and the sequence a run needs. It also compares the shipped migration snapshot with the model and generates the idempotent script of that migration, failing when the snapshot no longer matches or the script has lost its guards or gained a statement Dameng does not define; that is the check a build machine can run, and it is why the sample does not rot between the runs that have an instance.

### Feature probe

`samples/DmProviderFeatureProbe` is the second live program, run by hand and never by the pipeline: the solution builds it, so it cannot rot, but no workflow invokes it. It asks about the EF Core modelling and query surface rather than the server's quirks: entity definitions and annotations, identity, Guid, string and composite keys, hi-lo, indexes, defaults, computed columns, shadow properties, concurrency tokens, query filters, TPH/TPT/TPC, owned same-table and JSON types, complex types, value converters, primitive collections, every mapped CLR type in its nullable and non-nullable form, all four relationship shapes with their loading and delete behaviour, projections, filtered includes, grouping, `FromSql`, raw commands, transactions, `ExecuteUpdate`/`ExecuteDelete`, no-tracking and compiled queries.

It creates 32 prefixed tables and a sequence through the relational database creator, reports each of its 44 steps separately, and drops them unless `--keep`; `--dry-run` needs no database and checks that the model still produces every declared table and the sequence, and that step names are unique. It resolves the connection string the same way the acceptance console does and shares its user-secrets id, so one `dotnet user-secrets set` serves both.

No step is in the probe's ledger any more. Two gaps the first run measured were fixed in the provider rather than recorded: containment on a primitive collection now expands through `TranslatePrimitiveCollection` into the server's `JSON_TABLE` row source, and the boolean rewrite of the query pipeline now also reaches the `ExecuteUpdate` and `ExecuteDelete` predicates, whose commands are built outside `VisitSelect`. `DmPrimitiveCollectionTests` and `DmDmlQueryTests` pin both shapes, so neither depends on a live run. The ledger stays separate from `Skipped.md`: it records steps of this program, and an entry carries the server's message.

```pwsh
dotnet run --project samples/DmProviderFeatureProbe -- "<connection string>"
dotnet run --project samples/DmProviderFeatureProbe -- --dry-run
dotnet run --project samples/DmProviderFeatureProbe -- --list
dotnet run --project samples/DmProviderFeatureProbe -- --filter=relationship
```

It is written at the EF Core 8 API floor and has no migration, so an `efcore/8.0` or `efcore/9.0` branch receives the folder with only the target framework changed.

The connection string must set `convertToTz=True`. The model carries `DateTimeOffset` columns, and without that option the driver reads a `DATETIME WITH TIME ZONE` column as `DateTime`, which the materializer cannot cast; the probe refuses such a connection string with exit code 2 instead of reporting six misleading `InvalidCastException` failures.

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





