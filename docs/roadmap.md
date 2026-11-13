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
| P7 | In progress on `efcore/9.0`. The offline gate passes on 2026-11-13: zero-warning build, the unit and SQL shape suite, the acceptance dry run, the package smoke test and the skip ledger guard. The live specification run, the ledger re-measurement and `v9.0.1` are pending |
| P8-P9 | Not started |

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
| Precondition | P7 complete, since branches chain |
| Delivers | Same as P7 against EF Core 8; `v8.0.x` tags stay on `efcore/8.0`. The migrations lock API does not exist in EF Core 8 and is not carried over. |
| Exit criteria | Same as P7; `v8.0.x` published |

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

