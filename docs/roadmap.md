# Roadmap

Status: **decided**. Dates and ordering may change; exit criteria do not.

## 1. Principles

1. **Vertical slices.** Every phase leaves something runnable end to end. Work is not split as "finish all type mappings, then start queries".
2. **The skip ledger is the progress bar.** Phase exit is expressed as a reduction in the specification test skip ledger, not as "file X is finished".
3. **Phase exit does not imply coverage.** A phase can close while capabilities inside it remain `Not started` in the [Capability Matrix](capability-matrix.md). The matrix, not the phase list, is the authoritative statement of what is supported.
4. **Phases may run in parallel, split by file scope.** P1, P3 and P4 touch disjoint directories.
5. **EF Core 10 first.** The maintenance branches for EF Core 9 and 8 are created after the EF Core 10 line passes release acceptance.

## 2. Phases

| Phase | Status |
|---|---|
| P0-P5 | Complete: the offline and live gates pass and each capability is in the matrix |
| P6 | Code complete; the package and the Windows live matrix are verified, the Linux run, release tag and NuGet publish are pending |
| P7 | Code complete on `efcore/9.0`. Everything measured on 2026-11-13 passes: zero-warning build, 561 unit and SQL shape cases, the 33-step acceptance dry run, the package smoke test, the ledger guard, the live acceptance console (33 of 33 steps) and the live specification suite (4034 results, 3961 passed, 73 skipped, 0 failed). `v9.0.1`, the Linux run and the PostgreSQL-compatible run are pending |
| P8 | Code complete on `efcore/8.0` on 2026-11-13. Offline: zero-warning build, 553 unit and SQL shape cases, the 32-step acceptance dry run, the specification project build, the ledger guard and the package smoke test pass on SDK 8.0.404. The live acceptance console and the live specification suite are pending an instance |
| P9 | Not started |

### P0 — Walking skeleton

Split into two gates so that construction is never blocked by environment availability. Most of a database provider can be built and verified without a database; only the execution gate needs one.

| Item | Detail |
|---|---|
| P0a — Offline skeleton | Solution and project layout, `Dialect/` layer, `UseDm()`, options extension, service registration, minimal type mapping source, relational connection, SQL generation helper, `LICENSE`, provenance record, CI skeleton |
| P0a exit | Zero-warning build; unit tests pass; the `Dialect/` isolation test passes; `ToQueryString()` produces SQL for a simple query with no database involved |
| P0b — Execution gate | Specification test project wired; `SELECT 1` against a live instance; driver connectivity and cross-TFM asset behaviour recorded; initial skip ledger produced |
| P0b exit | `SELECT 1` succeeds; the specification suite runs and produces the ledger; driver findings are recorded |

P0b establishes the facts every later phase depends on, including whether the driver's `net9.0` asset behaves correctly under `net10.0`. It is a gate on *completing* P0, not on starting the work.

### P1 — Storage and CRUD

| Item | Detail |
|---|---|
| Delivers | Complete type mapping source for all CLR-to-Dameng mappings, connection, transaction, execution strategy, database creator (`EnsureCreated` / `EnsureDeleted`), basic identity readback |
| Exit criteria | Specification tests for built-in data types, store-generated values and connection behaviour pass; live CRUD passes |

### P2 — Query pipeline

Largest phase, roughly 35–40% of the total effort.

| Item | Detail |
|---|---|
| Delivers | Query SQL generator (paging, `||` concatenation, `CAST` around `COUNT` and `SUM`, Dameng functions), member translator provider, method call translator provider (string, date and time, math, `new Guid`, `object.ToString`), SQL translating visitor (`LIKE` with `ESCAPE`), nullability processor (override differences only, never copy wholesale), query translation postprocessor, parameter-based SQL processor |
| Exit criteria | Specification tests for `Where`, `Select`, `Join`, `GroupBy`, `Include`, string, date and time, and math pass; remaining ledger entries are categorized as server limitations or EF Core limitations |

### P3 — Update pipeline

| Item | Detail |
|---|---|
| Delivers | Modification command batch and factory, update SQL generator, concurrency tokens, `ExecuteUpdate` and `ExecuteDelete` |
| Exit criteria | Specification tests for updates, batching and concurrency pass |

### P4 — Migrations and scaffolding

| Item | Detail |
|---|---|
| Delivers | Migrations SQL generator, history repository with custom table and schema support, migrations lock over a `DBMS_LOCK` named lock, schema and user operations, database model factory driven by `Dialect/` catalog queries, design-time services, annotation code generator, code generator |
| Exit criteria | `dotnet ef migrations add`, `update` and `remove` work; `dotnet ef dbcontext scaffold` produces a model that compiles and passes its smoke test; concurrent migrators serialize correctly on a live database |

### P5 — Value generation

| Item | Detail |
|---|---|
| Delivers | Identity, sequence, HiLo, value generator selector |
| Exit criteria | Specification tests for sequences and store-generated values pass |

### P6 — EF Core 10 release

| Item | Detail |
|---|---|
| Delivers | Packaging with tag, package and assembly version agreement; consumer smoke test in an isolated NuGet cache; full live acceptance on Windows and Linux in both compatibility modes; final documentation |
| Exit criteria | The produced package works in a fresh consumer project; the EF Core dependency range carries an upper bound; no `Known defect` ledger entries remain; `DmProviderValidation` passes every step |
| Produces | Tag `v10.0.x` on `efcore/10.0`. The tag belongs to this line and is not a precondition for any other line |

### P7 — EF Core 9 maintenance branch

| Item | Detail |
|---|---|
| Precondition | P6 code complete on `efcore/10.0`. The `v10.0.x` tag is not a precondition: a tag belongs to the line that owns it, and this line's start waits for no other line's release |
| Delivers | Branch created from `efcore/10.0` at the moment the line starts, not from a tag; target framework `net9.0`; EF Core dependency `[9.0.0,10.0.0)`; specification tests at 9.0.22; reverse adaptation per [Version Matrix](version-matrix.md); `v9.0.x` tags stay on `efcore/9.0` |
| Exit criteria | Specification tests pass for every executable case; live acceptance passes; `v9.0.x` published |

### P8 — EF Core 8 maintenance branch

| Item | Detail |
|---|---|
| Precondition | P7 code complete. The branch exists: `efcore/8.0`, cut from `efcore/9.0` at `3e937a2` on 2026-11-13 |
| Delivers | Target framework `net8.0`; every EF Core package a range with an 8.0.0 floor (`[8.0.0,9.0.0)` for the dependency, `Design` and `UserSecrets`, and the specification package); a classic `.sln` and `global.json` at SDK 8.0.404, because that SDK cannot read `.slnx`; specification cases at xUnit v2 (`xunit` 2.9.3, because the 2.4.2 analyzer the 8.0.0 package pins rejects the EF base classes); the migrations lock removed, because EF Core 8 declares no lock members; the parameterized-collection option plumbing removed, because `TranslateParameterizedCollectionsToConstants/Parameters` is EF Core 9 API; `v8.0.x` tags stay on `efcore/8.0` |
| Steps | 1. Retarget and re-range in one commit (`net8.0`, `[8.0.0,9.0.0)`) and let the compiler list what is absent. 2. Delete rather than shim what EF Core 8 does not declare. 3. Rebuild the T2 baselines against the 8 SQL shapes. 4. Rewrite the skip overrides against the 8 signatures and rebuild the ledger from a live run, never from the 9 copy. 5. Run the offline gate, then the live acceptance console and the live specification suite. 6. Update the per-line documents in the same commits. 7. Move each measured row of [Version Matrix](version-matrix.md) §2 into the measured section and delete the rows that turned out not to matter |
| Exit criteria | The live specification suite passes with every remaining skip recorded and measured; `DmProviderValidation` passes every step; the package resolves and runs in an isolated consumer; `v8.0.x` published |


### P9 — EF Core 11

| Item | Detail |
|---|---|
| Precondition | EF Core 11 reaches general availability and a decision is taken to support it |
| Delivers | `dev` and `master` retargeted to `net11.0`; `efcore/10.0` keeps serving the `10.0.x` line; reverse adaptation across generic type mappings, structured `JsonPath`, collation quoting, and the xUnit v3 test migration |
| Exit criteria | Specification suite runs on xUnit v3 and passes; live acceptance passes on .NET 11; `v11.0.x` published from `master` |

## 3. Ordering

```
P0 --> P1 --+--> P2 --+
            +--> P3 --+--> P6 --> P7 --> P8
            +--> P4 --+
            +--> P5 --+
```

- **Serial:** P0a, then P0b, then P1. The skeleton is a precondition for everything; the live facts recorded in P0b are a precondition for believing any later result.
- **Parallel:** after P1, phases P2, P3, P4 and P5 write to disjoint directories (`Query/`, `Update/`, `Migrations/` with `Scaffolding/`, `ValueGeneration/`). Shared files such as service registration and `Dialect/` have a single owner who merges last.
- **Serial:** maintenance branches are created only after P6, and chain from newest to oldest.
- P9 is scheduled independently of P6–P8.

## 4. Prerequisites

These are requirements, not open questions. Work that depends on them does not start until they are met.

| Requirement | Blocks |
|---|---|
| A reachable Dameng instance with credentials able to create a schema | P0b, and every phase exit criterion from P1 onward |
| A Dameng instance in PostgreSQL-compatible mode | P6 exit criterion |
| A Linux x64 environment with a .NET runtime matching the branch target framework | P6 exit criterion |
| A nuget.org trusted-publishing policy for this repository and workflow | P6 |
| A list of capabilities required by existing deployments | Backport scope for P7 and P8 |

Nothing in P0a requires a database. A missing live environment delays completion of P0b and later phases; it does not delay P0a or the start of P1 construction.

If a prerequisite is unavailable, the affected phase stays open. It is not marked complete.

Recorded 2026-11-12: an instance is reachable — DM 8.1.4.80, in the default and in the Oracle-compatible mode, with credentials that can create and drop tables in a schema of the operator's choosing. That closes the first row for P0b and for the phase exits up to P5. The PostgreSQL-compatible instance, the Linux environment, the trusted-publishing policy and the deployment list remain open.

Recorded 2026-11-13: the EF Core 9 line passes its own gates on `efcore/9.0` (4034 specification results, 3961 passed, 73 skipped, 0 failed; live acceptance 33 of 33 steps). `v9.0.1` is not published yet, which is the owner's decision and not a precondition for P8; `efcore/8.0` was cut from `efcore/9.0` the same day.

Recorded 2026-11-13 on `efcore/8.0`: the adaptation's offline gate passes on SDK 8.0.404 — zero-warning Release build, 553 unit and SQL shape cases (0 failed), the 32-step acceptance dry run, the specification project build against 8.0.0 with the skip ledger guard, and the package smoke test, which restores and runs a consumer of `Sharp.DM.EntityFrameworkCore` 8.0.1 on `net8.0`. The live acceptance console and the live specification suite are not measured on this branch: the instance used by the 9 line (DM 8.1.4.80) was not reachable while this work was done.
