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
| Diagnostics | 2 | Logging definitions, command cancellation interceptor |

What it did **not** have:

- No specification test project. All 70 test files are self-authored. Nothing in the reference proves its behaviour against EF Core's own contract.
- No `IRelationalAnnotationProvider`, no `ISqlExpressionFactory`, no `IQueryCompilationContextFactory`, no `IExceptionDetector`, no `INavigationExpansionExtensibilityHelper`, no `IModificationCommandFactory`, no `IAggregateMethodCallTranslatorProvider`.
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
| Command timeout | `Not started` | — | P1 | Yes | Base option exists; not exercised |
| Max batch size | `Partial` | — | P3 | Yes | Read by the batch factory, not honoured yet |
| Open, close, reopen, async | `Not started` | — | P1 | Yes | Inherited from `RelationalConnection`; unverified |
| Transactions: begin, commit, rollback | `Not started` | — | P1 | Yes | |
| Savepoints and nested transactions | `Not started` | — | P1 | Yes | |
| Ambient transaction enlistment | `Blocked` | — | P1 | Yes | Requires observing driver behaviour |
| Non-retrying execution strategy | `Not started` | — | P1 | Yes | Relational default applies |
| Retrying execution strategy | `Not started` | — | P1 | Yes | Needs Dameng error codes |
| Transient error classification | `Blocked` | — | P1 | Yes | Error codes must be observed, not guessed |
| Cancellation token honoured on every async path | `Not started` | — | P1 | Yes | |
| Database exists check | `In progress` | `DmProviderRegistrationTests` | P1 | Yes | Implemented by attempting a connection; unverified |

### B. Type mapping

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| `int`, `long`, `short`, `byte`, `sbyte` | `Implemented` | `DmTypeMappingTests` | P1 | Yes | Store type names provisional until the P1 catalog check |
| `bool` | `Implemented` | `DmTypeMappingTests` | P1 | Yes | |
| `decimal` with precision and scale | `Implemented` | `DmTypeMappingTests` | P1 | Yes | Defaults to `DECIMAL(29,4)` when no facet is configured |
| `float`, `double` | `Implemented` | `DmTypeMappingTests` | P1 | Yes | |
| `string` with size, unicode and fixed length | `Implemented` | `DmTypeMappingTests` | P1 | Yes | `VARCHAR`, `NVARCHAR`, `CHAR` and `NCHAR`, sized when configured |
| `byte[]` with size | `Implemented` | `DmTypeMappingTests` | P1 | Yes | `BLOB` without a size, `VARBINARY(n)` with one |
| `DateTime` with fractional seconds | `Implemented` | `DmTypeMappingTests` | P1 | Yes | Precision suffix only when configured |
| `DateTimeOffset` | `Partial` | — | P1 | Yes | Mapping exists; not covered by a test |
| `TimeSpan` | `Partial` | — | P1 | Yes | Mapping exists; not covered by a test |
| `DateOnly`, `TimeOnly` | `Implemented` | `DmTypeMappingTests` | P1 | Yes | |
| `Guid` | `Implemented` | `DmTypeMappingTests` | P1 | Yes | Stored as `VARCHAR(36)` provisionally |
| `char`, `enum` | `Not started` | — | P1 | Yes | |
| JSON column type | `Not started` | — | P1 | Yes | |
| Spatial types (NetTopologySuite) | `Deferred` | — | After P6 | No | Not required by the specification suite core cases; revisit after the first release |
| Store type to CLR reverse lookup | `Not started` | — | P1 | Yes | Needed by scaffolding |
| Concurrency token (`rowversion`) | `Not started` | — | P3 | Yes | |
| Type mapping clone semantics | `Not applicable` | — | P1 | Yes | The provider builds a mapping per facet set instead of cloning a base mapping, so no clone override is needed |

### C. Query

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| `ToQueryString()` for a simple projection | `Implemented` | `DmQuerySqlGenerationTests` | P0a | Yes | Works with no database involved |
| `Where`, `OrderBy`, `Select` | `Implemented` | `DmQuerySqlGenerationTests` | P2 | Yes | |
| `Join`, `GroupBy`, aggregates | `Partial` | `DmQuerySqlGenerationTests` | P2 | Yes | `GroupBy` with `COUNT(*)` covered; joins not yet |
| `Include`, split and single queries | `Not started` | — | P2 | Yes | |
| Paging (`Skip`, `Take`) | `Partial` | `DmQuerySqlGenerationTests` | P2 | Yes | Emits ANSI `OFFSET ... FETCH NEXT`; Dameng support unverified |
| String methods and members | `Implemented` | `DmStringTranslationTests` | P2 | Yes | `Contains`, `StartsWith`, `EndsWith`, `ToUpper`, `ToLower`, `Trim`, `TrimStart`, `TrimEnd`, `Replace`, `Substring`, `Length`. Function names follow Oracle-compatible mode and are unverified against a server. |
| String concatenation operator | `Implemented` | `DmStringTranslationTests` | P2 | Yes | Emits the double pipe operator; the base generator emits `+`, which is arithmetic for strings |
| Date and time date parts (`Year`, `Month`, `Day`, `Hour`, `Minute`, `Second`) | `Implemented` | `DmDateTimeTranslationTests` | P2 | Yes | Rendered as `EXTRACT(PART FROM value)`; unverified against a server |
| Date and time methods (`AddDays`, `Now`, `UtcNow`, `Today`, `Date`) | `Not started` | — | P2 | Yes | The reference emitted SQL Server functions (`GETDATE`, `DATEPART`) for these |
| Math functions | `Not started` | — | P2 | Yes | |
| `Convert`, `ToString`, `new Guid()` | `Not started` | — | P2 | Yes | |
| `LIKE` with `ESCAPE` | `Implemented` | `DmStringTranslationTests` | P2 | Yes | Wildcards in the search value are escaped with a backslash escape character |
| Null semantics processor | `Not started` | — | P2 | Yes | Override only where Dameng differs |
| Parameter-based SQL processor | `Partial` | `DmQuerySqlGenerationTests` | P2 | Yes | Base behaviour; parameters render with a colon prefix |
| Compiled query cache key | `Not started` | — | P2 | Yes | |
| Collection parameter translation | `Not started` | — | P2 | No | EF Core 8+ contract |
| `FromSql` and composable raw SQL | `Not started` | — | P2 | Yes | |
| `ExecuteUpdate`, `ExecuteDelete` | `Not started` | — | P3 | No | EF Core 7+ contract |
| Inheritance mapping (TPH, TPT, TPC) | `Not started` | — | P2 | Yes | |
| JSON querying | `Deferred` | — | After P6 | No | The store type exists in the dialect; query translation is not required for the first release |
| Query filters and global filters | `Not started` | — | P2 | Yes | |

### D. Update

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| `INSERT`, `UPDATE`, `DELETE` | `Implemented` | `DmUpdateSqlTests` | P3 | Yes | Generated without a `RETURNING` clause, which Dameng only accepts as `RETURNING ... INTO` |
| Multiple statements per batch | `Deferred` | `DmUpdateSqlTests` | P3 | Yes | One statement per round trip; the affected row count comes from the driver return value and could not be attributed to individual commands in a shared batch |
| Store-generated value readback | `Not started` | `DmUpdateSqlTests` | P3 | Yes | Needs `RETURNING ... INTO` with output parameters; such an operation is rejected instead of leaving the property stale |
| Concurrency conflict detection | `Implemented` | `DmUpdateSqlTests` | P3 | Yes | A statement affecting a row count other than one raises `DbUpdateConcurrencyException` |
| Modification command factory | `Not started` | — | P3 | No | EF Core 9+ contract |
| `SaveChanges` transaction wrapping | `In progress` | `DmUpdateSqlTests` | P3 | Yes | The transaction is opened by the relational layer; not covered by a test |

### E. Migrations

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| Migrations SQL generator (DDL) | `Implemented` | `DmMigrationsSqlTests` | P4 | Yes | Table, index, constraint, sequence and data shapes; alter and rename operations are provider-specific and implemented here |
| History repository | `Not started` | — | P4 | Yes | |
| Migration lock | `Not started` | — | P4 | Yes | Lock table, per D7 |
| Schema operations | `Partial` | `DmMigrationsSqlTests` | P4 | Yes | `CREATE SCHEMA` is rejected: Dameng has no schema DDL separate from users |
| User operations | `Not started` | — | P4 | Yes | |
| Migrations annotation provider | `Not started` | — | P4 | Yes | |
| `MigrationBuilder` extensions | `Not started` | — | P4 | Yes | |
| Idempotent script generation | `Not started` | — | P4 | Yes | |
| Model differ integration | `Not started` | — | P4 | Yes | |

### F. Scaffolding and design time

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| Database model factory (reverse engineering) | `Not started` | — | P4 | Yes | Catalog queries come from `Dialect/` |
| Code generator | `Not started` | — | P4 | Yes | |
| Annotation code generator | `Not started` | — | P4 | Yes | |
| Design-time services | `Not started` | — | P4 | Yes | |
| Provider model validator | `Not started` | — | P1 | Yes | Relational default applies |
| Compiled model support | `Not started` | — | P6 | Yes | |

### G. Value generation

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| Identity columns | `Not started` | — | P5 | Yes | |
| Sequences | `Not started` | — | P5 | Yes | |
| HiLo over a sequence | `Not started` | — | P5 | Yes | |
| Value generator selector and cache | `Not started` | — | P5 | Yes | |
| Store generation convention | `Not started` | — | P5 | Yes | |
| Client-side value generation | `Not started` | — | P5 | Yes | |

### H. Conventions and metadata

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| Provider convention set builder | `Implemented` | `DmProviderRegistrationTests` | P0a | Yes | Required for the relational model to be built |
| Value generation strategy convention | `Not started` | — | P5 | Yes | |
| Fluent API: property extensions | `Not started` | — | P5 | Yes | Compatibility surface |
| Fluent API: model builder extensions | `Not started` | — | P5 | Yes | Compatibility surface |
| Fluent API: model and entity type extensions | `Not started` | — | P5 | Yes | Compatibility surface |
| `DmValueGenerationStrategy` annotation | `Not started` | — | P5 | Yes | Compatibility surface |
| Database facade extensions | `Not started` | — | P1 | Yes | Compatibility surface |
| `DbFunctions` extensions | `Not started` | — | P2 | Yes | Compatibility surface |

### I. Diagnostics

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| Provider logging definitions | `Implemented` | `DmProviderRegistrationTests` | P0a | Yes | |
| Provider event ids | `Not started` | — | P6 | Yes | Own id range, never reusing EF Core ids |
| Command cancellation interceptor | `Not started` | — | P1 | Yes | |
| Sensitive data logging compliance | `Not started` | — | P6 | Yes | |

### J. Testing and tooling

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| T1 unit tests for `Dialect/` | `Implemented` | `DmIdentifierTests`, `DmParametersTests`, `DmDialectConstantsTests` | P0a | Yes | |
| Dialect isolation guard | `Implemented` | `DialectLayerIsolationTests` | P0a | No | This project's rule |
| T2 SQL shape tests | `Implemented` | `DmQuerySqlGenerationTests` | P0a | Yes | Complete statement asserted, expression placed where server execution is required |
| T3 specification suite | `Not started` | — | P0b | **No** | The reference never had one |
| T4 differential comparison | `Not started` | — | P6 | No | Diagnostic only |
| Live validation console | `Not started` | — | P6 | Yes | |
| Package consumer smoke test | `Not started` | — | P6 | Yes | |

## 6. Current position

Rows by status, updated when the matrix changes:

| Status | Count |
|---|---|
| `Implemented` | 25 |
| `In progress` | 2 |
| `Partial` | 7 |
| `Blocked` | 2 |
| `Deferred` | 3 |
| `Not started` | 57 |
| `Not applicable` | 1 |
| **Total rows** | **97** |

The count is a rough indicator of distance, not a progress bar. The progress bar is the [skip ledger](testing.md).

## 7. Updating this document

1. Change a row's status in the same commit that changes the behaviour.
2. Name the test that proves it. If there is no test, the status is `Partial` or `In progress`, never `Implemented`.
3. Add rows when a new EF Core contract surface is discovered. Record the discovery in the row's Note.
4. Recompute the counts in section 6.
5. When a capability is judged unnecessary, mark it `Not applicable` and state the reason. Do not delete the row.
