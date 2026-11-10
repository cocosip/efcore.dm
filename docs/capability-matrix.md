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
| Command timeout | `Implemented` | `DmInheritedBehaviorTests` | P1 | Yes | The value the context sets reaches the command it executes |
| Max batch size | `Partial` | — | P3 | Yes | Read by the batch factory, not honoured yet |
| Open, close, reopen, async | `Implemented` | `DmConnectionTests` | P1 | Yes | Opens are counted, so a nested open needs its own close. Async members are the driver fallbacks, so they run synchronously underneath |
| Transactions: begin, commit, rollback | `Implemented` | `DmConnectionTests`, `DmTransactionTests` | P1 | Yes | Commit and rollback reach the driver transaction; disposing without committing disposes it; the isolation level the caller asks for reaches it; a unit of work inside a transaction runs on it rather than opening a second one |
| Savepoints and nested transactions | `Partial` | `DmConnectionTests`, `DmTransactionTests` | P1 | Yes | The relational layer executes `SAVEPOINT x`, `ROLLBACK TO x` and `RELEASE SAVEPOINT x` as statements rather than through the driver transaction, so the driver methods for it are unused; a unit of work inside a transaction is bracketed by one. A second transaction is rejected while one is open. Dameng support for `RELEASE SAVEPOINT` is unverified |
| Ambient transaction enlistment | `Blocked` | — | P1 | Yes | Requires observing driver behaviour |
| Non-retrying execution strategy | `Implemented` | `DmInheritedBehaviorTests` | P1 | Yes | A unit of work runs once, because a retry would replay a statement whose failure may already have changed the database |
| Retrying execution strategy | `Not started` | — | P1 | Yes | Needs Dameng error codes |
| Transient error classification | `Blocked` | — | P1 | Yes | Error codes must be observed, not guessed |
| Cancellation token honoured on every async path | `Partial` | `DmInheritedBehaviorTests` | P1 | Yes | A cancelled token stops an async query before it starts. A statement already running cannot be interrupted, because the driver exposes no asynchronous cancellation |
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
| `string` with size, unicode and fixed length | `Implemented` | `DmTypeMappingTests` | P1 | Yes | `VARCHAR`, `NVARCHAR`, `CHAR` and `NCHAR`, sized when configured |
| `byte[]` with size | `Implemented` | `DmTypeMappingTests` | P1 | Yes | `BLOB` without a size, `VARBINARY(n)` with one |
| `DateTime` with fractional seconds | `Implemented` | `DmTypeMappingTests` | P1 | Yes | Precision suffix only when configured |
| `DateTimeOffset` | `Implemented` | `DmParameterBindingTests` | P1 | Yes | Stored as `DATETIME WITH TIME ZONE`, bound as `DbType.DateTimeOffset` with the offset intact |
| `TimeSpan` | `Implemented` | `DmParameterBindingTests` | P1 | Yes | Stored as `INTERVAL DAY TO SECOND`, bound as `DbType.Time` |
| `DateOnly`, `TimeOnly` | `Implemented` | `DmTypeMappingTests` | P1 | Yes | |
| `Guid` | `Implemented` | `DmTypeMappingTests` | P1 | Yes | Stored as `VARCHAR(36)` provisionally |
| `char`, `enum` | `Implemented` | `DmParameterBindingTests` | P1 | Yes | A character is stored as `VARCHAR(1)` and bound as a one character string; an enum is stored as `INT` and bound as its underlying integer, including the nullable form |
| JSON column type | `Not started` | — | P1 | Yes | A store type alone is not enough in EF Core 10: with one in place the model builds, the DDL and the writes are correct, and materialization then fails because the container column also needs a JSON reader/writer, which `JsonTypeMapping` exposes no hook for |
| Spatial types (NetTopologySuite) | `Deferred` | — | After P6 | No | Not required by the specification suite core cases; revisit after the first release |
| Store type to CLR reverse lookup | `Implemented` | `DmReverseTypeMappingTests` | P1 | Yes | Recovers the CLR type and the size, precision and scale written in the name. `DATE` maps to `DateTime` because Dameng keeps a time part in it, while the forward direction maps `DateOnly` to `DATE`; a GUID column reads back as a string because the database has no GUID type |
| Concurrency token (`rowversion`) | `Not started` | — | P3 | Yes | |
| Type mapping clone semantics | `Not applicable` | — | P1 | Yes | The provider builds a mapping per facet set instead of cloning a base mapping, so no clone override is needed |

### C. Query

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| `ToQueryString()` for a simple projection | `Implemented` | `DmQuerySqlGenerationTests` | P0a | Yes | Works with no database involved |
| `Where`, `OrderBy`, `Select` | `Implemented` | `DmQuerySqlGenerationTests` | P2 | Yes | |
| `Join`, `GroupBy`, aggregates | `Implemented` | `DmQueryPipelineTests` | P2 | Yes | `INNER JOIN`, `LEFT JOIN`, `GROUP BY`, `COUNT`, `SUM`, `MIN` and `MAX` |
| `Include`, split and single queries | `Implemented` | `DmQueryPipelineTests` | P2 | Yes | Collection loading joins and orders by the root key; the split query keeps the root statement separate |
| Paging (`Skip`, `Take`) | `Implemented` | `DmQueryPipelineTests` | P2 | Yes | Emits `OFFSET ... ROWS FETCH NEXT ... ROWS ONLY`, which the DM SQL manual documents as its `ROW_LIMIT` clause in [数据的插入、删除和修改](https://eco.dameng.com/document/dm/zh-cn/pm/insertion-deletion-modification.html). `LIMIT n OFFSET m` is the alternative form and is not used |
| String methods and members | `Implemented` | `DmStringTranslationTests` | P2 | Yes | `Contains`, `StartsWith`, `EndsWith`, `ToUpper`, `ToLower`, `Trim`, `TrimStart`, `TrimEnd`, `Replace`, `Substring`, `Length`. Function names follow Oracle-compatible mode and are unverified against a server. |
| String concatenation operator | `Implemented` | `DmStringTranslationTests` | P2 | Yes | Emits the double pipe operator; the base generator emits `+`, which is arithmetic for strings |
| Date and time date parts (`Year`, `Month`, `Day`, `Hour`, `Minute`, `Second`) | `Implemented` | `DmDateTimeTranslationTests` | P2 | Yes | Rendered as `EXTRACT(PART FROM value)`; unverified against a server |
| Date and time methods (`AddDays`, `Now`, `UtcNow`, `Today`, `Date`) | `Implemented` | `DmDateTimeMethodTranslationTests` | P2 | Yes | Day counts are added directly and sub-day units are scaled into a fraction of a day; months use `ADD_MONTHS`; `Date` uses `TRUNC`. The reference emitted SQL Server functions (`GETDATE`, `DATEPART`) for these. `CURRENT_TIMESTAMP` and `SYS_EXTRACT_UTC(SYSTIMESTAMP)` are unverified against a server |
| Math functions | `Implemented` | `DmMathTranslationTests` | P2 | Yes | `ABS`, `ACOS`, `ASIN`, `ATAN`, `CEIL`, `COS`, `EXP`, `FLOOR`, `SIGN`, `SIN`, `SQRT`, `TAN`, `TRUNC`, `POWER`, `ROUND`, `LN`, `LOG`. `Math.Max` and `Math.Min` resolve to `GREATEST` and `LEAST` through the translating visitor, which also covers `EF.Functions.Greatest` and `Least`. Unverified against a server |
| `Convert` and `ToString` | `Implemented` | `DmConversionTranslationTests` | P2 | Yes | `Convert` becomes `CAST(value AS type)` using the dialect type names; `ToString` becomes `TO_CHAR`, which needs no declared width. Unverified against a server |
| `new Guid()` and `Guid.NewGuid()` | `Not started` | — | P2 | Yes | Needs confirmation that Dameng exposes a GUID generator such as `SYS_GUID()` |
| `LIKE` with `ESCAPE` | `Implemented` | `DmStringTranslationTests` | P2 | Yes | Wildcards in the search value are escaped with a backslash escape character |
| Null semantics processor | `Not started` | — | P2 | Yes | Override only where Dameng differs |
| Parameter-based SQL processor | `Partial` | `DmQuerySqlGenerationTests` | P2 | Yes | Base behaviour; parameters render with a colon prefix |
| Compiled query cache key | `Implemented` | `DmInheritedBehaviorTests` | P2 | Yes | The default key already covers everything the translation depends on, because the translation is a function of the model and the query expression |
| Collection parameter translation | `Not started` | — | P2 | No | EF Core 8+ contract |
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
| Modification command factory | `Not started` | — | P3 | No | EF Core 9+ contract |
| `SaveChanges` transaction wrapping | `Implemented` | `DmConnectionTests` | P3 | Yes | A unit of work that builds more than one command is wrapped and committed; a single command runs without a transaction |

### E. Migrations

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| Migrations SQL generator (DDL) | `Implemented` | `DmMigrationsSqlTests` | P4 | Yes | Table, index, constraint, sequence and data shapes. A sequence takes no type clause and spells its options as one word, an alteration repeats every option, and an alter, rename or schema operation is implemented here rather than by the base class |
| History repository | `Implemented` | `DmHistoryRepositoryTests` | P4 | Yes | Catalog existence check against `SYS.SYSOBJECTS`, unverified against a server |
| Migration lock | `Implemented` | `DmHistoryRepositoryTests` | P4 | Yes | Exclusive table lock released by the transaction; no lock row, so a failed run cannot strand one |
| Schema operations | `Implemented` | `DmMigrationsSqlTests` | P4 | Yes | `CREATE SCHEMA` runs inside a block that asks `SYS.SYSOBJECTS` first, so a schema the database administrator created does not fail the migration. The catalog query is not yet confirmed against a server |
| User operations | `Implemented` | `DmMigrationsSqlTests` | P4 | Yes | Creating a user creates the schema it owns in a second statement. Privileges are not granted: the reference grants DBA as part of the operation, which a migration should not decide |
| Migrations annotation provider | `Implemented` | `DmAnnotationProviderTests` | P4 | Yes | The identity attribute and a non default seed and increment reach the column operation the differ produces, so the generated migration shows them and a replay reads them; the mirrored key of a table per type hierarchy carries none |
| `MigrationBuilder` extensions | `Implemented` | `DmMigrationsSqlTests` | P4 | Yes | `IsDm()` lets one migration assembly guard its provider specific operations |
| Idempotent script generation | `Implemented` | `DmHistoryRepositoryTests` | P4 | Yes | The history repository wraps each migration in a block that reads the history table first and runs the create through `EXECUTE IMMEDIATE`. Whether DDL inside the block is accepted is not yet confirmed against a server |
| Model differ integration | `Implemented` | `DmModelDifferTests` | P4 | Yes | The EF Core differ compares the two relational models and keeps the provider metadata: a new hi-lo property produces the schema and the sequence, a column that appears produces an added column rather than a modified one, and a widened column produces the matching `MODIFY`. Changing the value generation strategy between identity and hi-lo yields an `ALTER` that only repeats the type, because Dameng defines identity at column creation; a migration that switches strategy needs a hand-written operation |

### F. Scaffolding and design time

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| Database model factory (reverse engineering) | `Not started` | — | P4 | Yes | Catalog queries come from `Dialect/` |
| Code generator | `Partial` | `DmProviderConfigurationCodeGeneratorTests` | P4 | Yes | The provider configuration code is generated: `UseDm` with the connection string and no provider options, since the provider declares none. The model it is generated from comes from the database model factory, which is not implemented |
| Annotation code generator | `Implemented` | `DmSnapshotTests` | P4 | Yes | The relational generator is enough: an annotation the generator does not name is written to the snapshot as `HasAnnotation`, which is how the value generation strategy, the identity seed and the hi-lo sequence reach the next migration |
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
| `DbFunctions` extensions | `Not started` | — | P2 | Yes | Compatibility surface |

### I. Diagnostics

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| Provider logging definitions | `Implemented` | `DmProviderRegistrationTests` | P0a | Yes | |
| Provider event ids | `Implemented` | `DmDiagnosticsTests` | P6 | Yes | `DmEventId` declares the provider range from 30000 under the model validation category, starting with the decimal default warning |
| Command cancellation interceptor | `Not started` | — | P1 | Yes | |
| Sensitive data logging compliance | `Implemented` | `DmUpdateSqlTests`, `DmQueryPipelineTests` | P6 | Yes | Every statement the provider builds carries parameters through the relational command builder, so a value never enters the log text, and the provider formats no value of its own |

### J. Testing and tooling

| Capability | Status | Evidence | Phase | Reference | Note |
|---|---|---|---|---|---|
| T1 unit tests for `Dialect/` | `Implemented` | `DmIdentifierTests`, `DmParametersTests`, `DmDialectConstantsTests` | P0a | Yes | |
| Dialect isolation guard | `Implemented` | `DialectLayerIsolationTests` | P0a | No | This project's rule |
| T2 SQL shape tests | `Implemented` | `DmQuerySqlGenerationTests` | P0a | Yes | Complete statement asserted, expression placed where server execution is required |
| T3 specification suite | `Not started` | — | P0b | **No** | The reference never had one |
| T4 differential comparison | `Not started` | — | P6 | No | Diagnostic only |
| Live validation console | `Not started` | — | P6 | Yes | |
| Package consumer smoke test | `Implemented` | `scripts/package-smoke.ps1` | P6 | Yes | Packs the provider, restores a project that references it as a package into its own NuGet cache, compares the SHA-256 of the resolved package with the packed one, and runs the consumer, which builds a model and a create script without connecting to a server |

## 6. Current position

Rows by status, updated when the matrix changes:

| Status | Count |
|---|---|
| `Implemented` | 75 |
| `In progress` | 0 |
| `Partial` | 6 |
| `Blocked` | 2 |
| `Deferred` | 3 |
| `Not started` | 13 |
| `Not applicable` | 2 |
| **Total rows** | **101** |

The count is a rough indicator of distance, not a progress bar. The progress bar is the [skip ledger](testing.md).

## 7. Updating this document

1. Change a row's status in the same commit that changes the behaviour.
2. Name the test that proves it. If there is no test, the status is `Partial` or `In progress`, never `Implemented`.
3. Add rows when a new EF Core contract surface is discovered. Record the discovery in the row's Note.
4. Recompute the counts in section 6. `CapabilityMatrixTests` fails when the summary and the rows disagree, when the rows do not add up to the declared total, or when an implemented row names no test, so the step is enforced rather than remembered.
5. When a capability is judged unnecessary, mark it `Not applicable` and state the reason. Do not delete the row.
