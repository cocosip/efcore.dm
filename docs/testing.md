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
| `dev` (EF Core 10) | `10.0.14` | xUnit v2 (`xunit.core` 2.9.3) |
| `efcore9` | `9.0.22` | xUnit v2 |
| `efcore8` | `8.0.33` | xUnit v2 |
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
  DM.EntityFrameworkCore.SpecificationTests/         T3: inherits EF Core specification base classes (planned)
    Fixtures/                           one fixture per test store
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

The query steps were then added, taking the run to twenty-five, and a date difference step to twenty-six. They failed one of them on the default-mode instance only: `ADD_MONTHS` answered a timestamp at day precision there, so `AddMonths` returned midnight instead of the time it was given. The same statement kept the time on the Oracle-compatible instance, which is why a single instance would have hidden it. The provider now names `TIMESTAMPADD`, measured on both modes to clamp and to keep the time, and the step pins a month end so neither property can regress silently.

Still unverified by a live run, and therefore still recorded as unverified in the matrix: the `EF.Functions` overloads that take a `TimeSpan`, the idempotent script executed as a script rather than generated, the migrations lock held by two concurrent processes, retry classification, and the PostgreSQL-compatible mode.

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
