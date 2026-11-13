# Version Matrix

Status: **living document**, maintained on the branch that owns the EF Core line, with `dev` and `master` reserved for the next line (EF Core 11).

Answers one question: **when moving the provider from EF Core version A to version B, what must change?**

Used in two directions: forward when adopting a new EF Core major version, backward when creating a maintenance branch.

## 1. Target frameworks and support

| EF Core | Target framework | Official support until | Branch | Specification tests |
|---|---|---|---|---|
| 8.0 | `net8.0` | 2026-11-10 (expired) | `efcore/8.0` | 8.0.0 (range floor of `[8.0.0,9.0.0)`) |
| 9.0 | `net9.0` | 2026-11-10 (expired) | `efcore/9.0` | 9.0.22 |
| 10.0 | `net10.0` | 2028-11-10 | `efcore/10.0` | 10.0.14 |
| 11.0 | `net11.0` | not released | `efcore/11.0` (not created) | 11.0.0-rc (xUnit v3) |

EF Core 8 and 9 both ship `net8.0` assets, so one package cannot carry distinct dependency ranges for them, and each line targets the framework it is built and tested on: `net8.0` for 8, `net9.0` for 9. EF Core 10 ships `net10.0` only and EF Core 11 ships `net11.0` only, so adoption of each major version is a separate build and, from EF Core 11 onward, a runtime cutover.

EF Core 8 and 9 both reached end of support on 2026-11-10. Their branches exist to serve existing deployments and are not committed to tracking new capabilities.

## 2. EF Core 8 to 9

### Migrations locking became abstract

In `release/9.0`, `HistoryRepository` declares:

```csharp
public abstract LockReleaseBehavior LockReleaseBehavior { get; }
public abstract IMigrationsDatabaseLock AcquireDatabaseLock();
public abstract Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(CancellationToken cancellationToken = default);
```

In `release/8.0` none of these members exist.

| Item | EF Core 8 | EF Core 9 | EF Core 10 |
|---|---|---|---|
| Lock members | absent | must implement both | same contract |
| `LockReleaseBehavior` | not applicable | `Transaction` | `Transaction` |

The provider implements locking with a `DBMS_LOCK` named lock (D7). The implementation is written once for EF Core 9 and reused on 10. It does not exist on the EF Core 8 branch.

### Other provider-relevant changes

| Change | Impact | Source |
|---|---|---|
| Applying migrations throws when the model has pending changes | Migrations and model-snapshot determinism | [EF Core 9 breaking changes](https://learn.microsoft.com/ef/core/what-is-new/ef-core-9.0/breaking-changes) |
| Applying migrations inside an explicit transaction throws | Migration lock and transaction boundaries | same |
| All pending migrations are applied in a single transaction | Transaction suppression semantics in migrations | same |
| `SqlFunctionExpression` validates the arity of `argumentsPropagateNullability` | Every translator constructing this node must pass one flag per argument | same |
| `EF.Constant()` and `EF.Parameter()` no longer work inside compiled queries | Translator assumptions about expression position | same |
| Primitive collection APIs replaced (`ListComparer` becomes `ListOfValueTypesComparer` and related types) | Direct API replacement where used | same |
| Compiled models reference value converter methods directly | Provider-supplied converters must be public or internal; private methods fail to compile | same |
| `ToString()` returns an empty string for null | Translation semantics must match | same |
| Parameterized collection translation becomes user-selectable (`ToConstants` / `ToParameters`) | Both rendering forms must work | [EF Core 9 what's new](https://learn.microsoft.com/ef/core/what-is-new/ef-core-9.0/whatsnew) |
| `PropertyDiscoveryConvention.IsCandidatePrimitiveProperty` is overridable | Available hook for Dameng type discovery | same |

### Reverse adaptation, EF Core 9 to 8, measured on `efcore/8.0` (2026-11-13)

The 8 line was cut from `efcore/9.0` at `3e937a2`, so the code it starts from already carries the three
statement-level fixes the 9 line measured: a boolean `ORDER BY`, a boolean `^`, and the `JSON_TABLE`
ordinality ordering. None of the three is an EF Core API, and all three pass on 8 unchanged.

The direction matters: the maintenance branch is cut from the newer line, so these are the changes the
9 line's code needed to become the 8 line. Anything marked *absent* was removed rather than shimmed.
Measured against the 8.0.0 packages the build restores and the `release/8.0` sources.

| Area | Measured | Action taken |
|---|---|---|
| Migrations lock | `HistoryRepository` declares no `LockReleaseBehavior` / `AcquireDatabaseLock` members before EF Core 9, and `IMigrationsDatabaseLock` / `LockReleaseBehavior` do not exist | Deleted the overrides, `DmMigrationDatabaseLock`, the `DBMS_LOCK` helper in `Dialect/`, its 4 T2 cases and the `migrations: concurrent deployments` acceptance step (33 steps become 32). The idempotent create block keeps its lost-race tolerance and no longer mentions a lock |
| Parameterized collection options | `TranslateParameterizedCollectionsToConstants()` / `...ToParameters()` / `UseParameterizedCollectionMode` are 9/10 API | No plumbing existed outside the tests; the 2 constants-mode T2 cases are deleted. The one-JSON-parameter `JSON_TABLE` form is the only rendering, and its case passes |
| `EF.Parameter()` | Absent on 8.0.34; `EF.Constant()` is the only member of that family that exists (9 sources add `Parameter`) | The T2 case that used it is deleted; the default collection-parameter case pins the same SQL |
| `PropertyDiscoveryConvention.IsCandidatePrimitiveProperty` | Absent on 8; the provider never referenced it | No action; the predicted row was deleted |
| Primitive collection comparer types | 8 has `ListComparer<T>` and no `ListOfValueTypesComparer` / `ListOfNullableValueTypesComparer`; the provider never referenced either | No action; the predicted row was deleted |
| Parameter-based SQL processor | `RelationalParameterBasedSqlProcessorParameters` is 9 API; the 8 processor and factory take `bool useRelationalNulls` | `DmParameterBasedSqlProcessor` and its factory moved to the 8 signatures |
| Value generator selector | 8 has `Select`/`Create` and no `TrySelect`; 9 moved hi-lo selection into `TrySelect` | The hi-lo switch moved into `Create`; an unsupported CLR type now throws instead of returning false |
| `Math.Max` / `Math.Min` | 8 has no `GenerateGreatest` / `GenerateLeast` hook on the relational visitor, and no `EF.Functions.Greatest` / `Least` | Translation moved into `DmMathMethodTranslator`; the `GREATEST`/`LEAST` baselines pass unchanged |
| `SelectExpression` and friends | 8 has no `SqlAliasManager`, `ColumnExpression` is abstract, the primitive-collection root is built with the `(tableExpression, columnName, columnType, mapping, isColumnNullable, identifier…)` constructor, `Update` lists projections first and limit before offset, and `DeleteExpression.Update` takes only the select | The `JSON_TABLE` row source is built with the 8 constructor and an ordinality identifier column (`rn`, `int`); the three `Update` call sites and the delete rewrite follow the 8 order |
| `HistoryRepository.GetCreateCommands()` | 9 API; 8 publishes `GetCreateScript()` only | The idempotent wrapper splits the script on the batch terminator and refuses more than one statement |
| Test store | 8's `TestStore` has abstract `Clean(DbContext)`, no `InitializeAsync`, and `RelationalTestStore` takes `(name, shared)` with the connection as a property | `Clean` awaits the store's async clean; the connection and its string are set in the constructor |
| SQL shape baselines | 8 aliases an anonymous projection as `"t"` where 9 uses `"s0"`/`"i0"`, and wraps the `SUBSTR` parts of `string.Remove`/`Insert` in `COALESCE(..., '')` | The four affected T2 baselines carry the 8 shapes; the other 549 cases pass unchanged |
| Specification base classes | `NorthwindBulkUpdatesRelationalTestBase`/`…RelationalFixture` and `ITestSqlLoggerFactory` are 9 API; `Average_over_max_subquery` / `…nested_subquery` are named `…_is_client_eval`; 8 declares no lock, `TrySelect`, `value_index_Column`, `JsonScalarExpression` inference or JSON-column migration cases; `Can_perform_query_with_max_length` and `Swap_filtered_unique_index_values` are `void` | The bulk-updates class derives from the 8 base and fixture, the fixture keeps its `TestSqlLoggerFactory` property without the interface, the two `void` cases return nothing, 8 overrides and 8 ledger rows leave, and 2 average cases stop overriding behaviour the 8 base does not assert |
| JSON column store type | `JsonTypePlaceholder` is absent on 8; the mapping is resolved through `FindMapping(typeof(JsonElement))`, which the code already does | The mapping compiles and the shape tests pass; the live reader path stays the open item |
| Specification package | 8.0.x ships `net8.0` assets only and pins `xunit.core`/`xunit.assert` 2.4.2, `Microsoft.CodeAnalysis.CSharp` 4.5.0 and `NetTopologySuite` 2.5.0 | The project inherits `DmTargetFramework` = `net8.0` and references the package as the range `[8.0.0,9.0.0)`. It keeps `xunit` 2.9.3 rather than the pinned floor: the 2.4.2 analyzer reports `xUnit1013` on the EF base classes, which leave case methods public without an attribute |
| Build tooling | The 8.0 SDK cannot read `.slnx` (`MSB4068`), and C# 14 binds `T[].Contains` to `MemoryExtensions` instead of `Enumerable.Contains`, which the 8 query pipeline does not translate | The solution is a classic `.sln` on this branch, `global.json` pins 8.0.404, and CI installs 8.0.x, so the branch compiles with C# 12 as the framework expects |
| Dependency ranges | The line's EF Core packages were pinned to one patch or floated on `9.0.*` | Every EF Core package on this line is a range with an 8.0.0 floor: `[8.0.0,9.0.0)` for the provider dependency, `Design`, `UserSecrets` and the specification package |
| Driver assets | `DM.DmProvider` 8.3.1.47463 ships `net8.0` assets | No packaging change; the package smoke test restores and runs the consumer on `net8.0` |

## 3. EF Core 9 to 10

| Change | Impact | Source |
|---|---|---|
| Parameterized collections default to multiple scalar parameters, with padding | `ValuesExpression` expands to N parameters; EF Core supplies padding, the provider renders them | [EF Core 10 breaking changes](https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/breaking-changes) |
| `IRelationalCommandDiagnosticsLogger` methods gain a `logCommandText` parameter | Custom diagnostic loggers and interceptors change signature | same |
| SQL parameter names are simplified (`@__city_0` becomes `@city`) | Test baselines and any logic depending on parameter names | same |
| Complex type column names are uniquified; nested complex type properties use the full path | Migrations and reverse engineering must reproduce the naming | same |
| `ExecuteUpdateAsync` accepts a regular, non-expression lambda | Update pipeline entry shape | same |
| `IDiscriminatorPropertySetConvention` takes `IConventionTypeBaseBuilder` | Custom convention implementations | same |
| `RelationalMethodCallTranslatorProvider` and `RelationalMemberTranslatorProvider` no longer call `AddTranslators` from their constructors; `AddTranslators` is only defined, never invoked | A derived provider that relied on overriding `AddTranslators` silently loses its translators. Call `AddTranslators` in the derived constructor instead. Measured: overriding it compiles and does nothing. | `release/10.0` sources of both classes |
| `IMethodCallTranslator.Translate` and `IMemberTranslator.Translate` take `IDiagnosticsLogger<DbLoggerCategory.Query>` instead of a type mapping | Measured on 9.0.22: the logger-based signature is already the one EF Core 9 declares, so the 14 translators compile on both lines without a change. The row is not a 9→10 task | `release/9.0` and `release/10.0` sources; provider build |
| `net10.0` only | Target framework switch | package metadata |

### Reverse adaptation, EF Core 10 to 9, measured on `efcore/9.0` (2026-11-13)

The direction matters: the maintenance branch is cut from the EF Core 10 line, so these are the changes the newer line's code needed to become the 9 line. Anything marked *absent* was removed rather than shimmed.

| Area | Measured | Action taken |
|---|---|---|
| `RightJoinExpression` | Absent in 9; the `RightJoin` and `LeftJoin` LINQ operators are EF Core 10 additions | `VisitRightJoin` deleted, `right join` acceptance step deleted, pipeline case deleted. `LEFT JOIN` is expressed as `GroupJoin` + `DefaultIfEmpty` and keeps its shape |
| JSON column mapping | `JsonTypePlaceholder` is an EF Core 10 type. In 9, `JsonColumnBase.GetDefaultStoreTypeMapping()` resolves the column through `FindMapping(typeof(JsonElement))` | The mapping is registered for `JsonElement`; JSON column shape tests pass. The live suite still has to confirm the reader path |
| Specification test package | 9.0.22 ships `net9.0` assets only (10.0.14 ships `net10.0`) | The provider targets `net9.0`, so the specification project inherits it; the 10 line inherits `net10.0` the same way |
| Specification base classes | 19 overrides named cases that exist only in the 10 bases | Deleted. The skip ledger guard then reported 2 names that no longer exist and both rows left the ledger |
| SQL parameter names | 9 writes `:__value_0`, 10 writes `:value` | Every T2 baseline on this branch carries the 9 form |
| Parameterized collections | 9 default: one JSON parameter expanded by the provider through `JSON_TABLE`. 10 default: one scalar parameter per element, with padding. The 9 option names are `TranslateParameterizedCollectionsToConstants()` / `TranslateParameterizedCollectionsToParameters()`; `UseParameterizedCollectionMode` is 10 API | Constants mode renders `IN (1, 2)`; the default renders `IN (SELECT ... FROM JSON_TABLE(:__ids_0, ...))` |
| Nested `Take` | 9 keeps a subquery with two `FETCH FIRST` clauses; 10 folds them into `FETCH FIRST LEAST(:p, :p1) ROWS ONLY` | The pipeline case pins the 9 shape |
| `ExecuteUpdate` setter constant | 9 inlines it (`SET "Name" = 'y'`); 10 sends a parameter | Baseline updated; the provider renders what the framework passes |
| Entity list `Contains` | 9 inlines the single element's key as a constant | Baseline updated |
| Migrations lock | The abstract members exist on 9 and 10 both | `DBMS_LOCK` implementation carried unchanged |
| Boolean in `ORDER BY` | 9 composes an ordering over a predicate as `COALESCE(<condition>, 0)`, which the server rejects with `无效的ORDER BY子句`. 10 composes the ordering itself, so the 10 line never needed the rewrite | `VisitOrdering` writes `CASE WHEN ... THEN 1 ELSE 0 END` for a boolean-typed ordering; measured by five `Include`/`Miscellaneous` cases |
| Boolean `^` | 9 keeps the framework's `^`, which the server rejects with `语法分析出错`. The 10 line's tests pass without a translation | A boolean exclusive-or becomes a comparison of the two `CASE` forms; measured by `Where_bitwise_xor` |
| Specification case set | 9's bases name cases differently (for example an `_3` suffix on `GroupBy_aggregate_from_multiple_query_in_same_projection` and a misspelled primitive-collection migrations case) and skip 12 cases itself, so an override list copied from 10 silently stops applying | Skip overrides rewritten against the 9 signatures (`bool async`); 5 rows the 10 ledger held now pass and left the 9 ledger |

## 4. EF Core 10 to 11

EF Core 11 is the first version with an official provider-facing changelog, so this section is sourced from primary documentation rather than inferred.

### Provider changes

| Change | Impact | Source |
|---|---|---|
| Collation names are quoted in SQL like identifiers | Override `QuerySqlGenerator.VisitSql()` and `MigrationsSqlGenerator.ColumnDefinition()` if the database does not support quoting, or apply restricted-character validation | [EF Core 11 provider-facing changes](https://learn.microsoft.com/ef/core/what-is-new/ef-core-11.0/provider-facing-changes) |
| Type mappings become generic (`CoreTypeMapping<T>`, `RelationalTypeMapping<T>`); `Clone(...)` loses its `clrType` argument | Custom mappings must derive from the new base types and update `Clone` overrides | same |
| `JsonPath` on `IColumnModification` and `ColumnModificationParameters` changes from `string?` to a structured type with `Segments`, `Ordinals`, `IsRoot` and `AppendTo()` | Overrides of `UpdateSqlGenerator.AppendUpdateColumnValue()` must switch to the new type; the null or `"$"` check becomes `JsonPath is not { IsRoot: false }` | same |
| No-op SQL `CAST` nodes are stripped when store types match | Imprecise store types assigned by translations now propagate; each `Convert` operand must carry a precise store type | same |
| Keys and indexes can traverse complex-type properties; index and key metadata broadened | Code that consumes index or key metadata, generates index code, or performs custom validation | same |

### Test infrastructure changes

| Change | Impact | Source |
|---|---|---|
| Specification test assemblies move from xUnit v2 to xUnit v3 | Provider test project must adopt `xunit.v3` packages, `Microsoft.Testing.Platform`, and the v3 runner; `[ConditionalFact]` becomes `[Fact]`, `[ConditionalTheory]` becomes `[Theory]` | same |
| Query fixtures extend a new `QueryFixtureBase`; `NonSharedPrimitiveCollectionsQueryTestBase` merges into `PrimitiveCollectionsQueryTestBase` | Test project structure | same |
| Inheritance specification tests move to their own folder | Test project structure | same |

### Behaviour changes

| Change | Impact | Source |
|---|---|---|
| `Migrate()` throws when no migrations exist (`MigrationsNotFound`) | Migration paths and CI | [EF Core 11 breaking changes](https://learn.microsoft.com/ef/core/what-is-new/ef-core-11.0/breaking-changes) |
| `Property(...)` no longer promotes a collection member to a primitive collection | Model building conventions and tests | same |
| Owned JSON collections without an explicit key are deprecated; complex collections are the recommended path | JSON mapping must support complex types and complex collections | same |
| Split queries throw when concurrent modifications are detected | Correct ordering and key propagation in the query pipeline | same |
| `net11.0` only | Target framework switch and runtime cutover | package metadata |

## 5. Measured volatility

Reference measurement: the EF Core 9 to EF Core 10 migration in the reference implementation changed 24 of 114 source files (+1132 / -310 lines).

| Layer | Files changed | Examples |
|---|---|---|
| Query | 9 | nullability processor, SQL translating visitor, string method translator, query SQL generator, search condition visitor |
| Migrations | 3 | migrations SQL generator, history repository, migration database lock |
| Storage | 2 | type mapping source, JSON type mapping |
| Update | 1 | update SQL generator |
| Value generation | 1 | value generator selector |
| Other | 8 | service registration, utilities, resources, project file |

### Collections are a strategy change, not a new node type

`ValuesExpression` exists in EF Core 8, 9, 10 and `main`. What changes across versions is how parameterized collections are translated:

- EF Core 9 exposes `TranslateParameterizedCollectionsToConstants` and `TranslateParameterizedCollectionsToParameters`.
- EF Core 10 defaults to `ParameterTranslationMode.MultipleParameters` and adds padding. The option is set through `UseParameterizedCollectionMode`; the two older method names are obsolete.
- Measured: two contexts that share a model and differ only in the collection mode reuse the first translation, because the translation is cached against the compiled query. A test that exercises both modes gives each its own context type.

Consequence: every branch must handle `ValuesExpression`. The branches differ in default strategy, not in whether the node is supported.

## 6. Volatile surface checklist

Checked for every version transition.

| Layer | What to re-examine |
|---|---|
| L0 `Dialect/` | Nothing. Version-independent; synchronize as-is. |
| L1 Storage | Type mapping base types (generic from EF Core 11), `RelationalTypeMapping` constructor parameters, connection and transaction base types, execution strategy interface |
| L2 Query | `QuerySqlGenerator` virtual method set, nullability processor override points, translator provider construction dependencies, SQL translating visitor, parameter-based SQL processor, search condition visitor, collection parameter rendering |
| L3 Update | Modification command batch base type, update SQL generator virtual methods (`JsonPath` from EF Core 11), store-generated value readback |
| L4 Migrations and scaffolding | Migrations SQL generator virtual methods (collation quoting from EF Core 11), history repository (abstract lock members from EF Core 9), migrations database lock lifecycle, database model factory, design-time registration |
| L5 Value generation | Value generator selector, sequence and HiLo interfaces |
| L6 Public API | Service registration order, diagnostic logger interface (`logCommandText` from EF Core 10), event id allocation |

## 7. Reverse adaptation procedure

Applied when creating a maintenance branch, newest to oldest. The branch is created from the **adjacent newer**
line's branch at the moment this procedure starts (`efcore/10.0` → `efcore/9.0` → `efcore/8.0`); no other
line's release tag is a precondition, and the branch's own `v` tags are placed on it when it releases.

Cutting from the newest line instead replays every already measured delta through two steps at once, and each
step then has to be re-fixed without a measurement that separates them. The adjacent delta is the one that
was measured, so it is the one to inherit.

1. Set the EF Core dependency range, the specification test package version, and the target framework.
2. Handle compile errors first: APIs that do not exist on the older version fail the build.
3. Distinguish *absent* from *different*:
   - Types and members that do not exist on the older version are removed. Do not keep compatibility code for them.
   - Capabilities that exist but behave or are signed differently are reworked, not deleted. Collections are the reference case: the node exists on every version, so removing the handling is wrong.
4. Capability is never downgraded. A maintenance branch is complete when it supports everything its own EF Core version can express.
5. EF Core 8 branch: the migrations lock API does not exist, so the lock implementation from EF Core 9 is not carried over.
6. Regenerate the specification test skip ledger. Executable test sets differ between versions, so the ledger is never inherited.
7. Record any bidirectional divergence in this document with the version pair and the reason.

## 8. Version-independent content

The following are properties of Dameng, identical across EF Core versions. Their only version-dependent aspect is how they are registered with EF Core.

- Store type names, sizes, precision, and CLR type correspondence. A `TIME` column names a fractional second precision because Dameng defaults one that names none to 0
- Identifier delimiting and escaping
- Parameter prefix `:` and batch terminator `/` followed by a line feed, which belongs to the terminator because a script writer appends the next statement directly after it. A generated script opens no transaction, because Dameng starts one with the first statement it runs and defines no `START TRANSACTION`
- DDL syntax: identity columns, sequences, tables, indexes, and `COMMENT ON TABLE` and `COMMENT ON COLUMN` for a comment an object carries
- Catalog queries against `SYSOBJECTS` and related system tables
- Dameng error classification
- SQL dialect translation rules: `||` concatenation, `TOP` and `OFFSET`/`FETCH`, `CAST` wrapping of aggregates, `GREATEST` and `LEAST`, `DATEDIFF(datepart, date1, date2)` for date differences, `TIMESTAMPADD(MONTH, n, x)` for month and year arithmetic on a timestamp — not `ADD_MONTHS`, which answers at day precision on a default-mode instance — and `ADD_MONTHS` on a date, `ISDATE(value) = 1` for a validity check, `CAST(... AS TIME)` for a `TimeOnly` conversion, `SYS_EXTRACT_UTC(value)` for a UTC conversion, `UPPER` on both sides of an ignore-case comparison, `LPAD`/`RPAD` for padding, `LTRIM`/`RTRIM` with a character set, `INSTR` shifted to a zero-based index, `ATAN2`, and `INTERVAL '...' DAY TO SECOND` rather than a bare time string for an interval literal

A date difference is the exception: subtracting two dates answers a number of days rather than an interval, and the conversion to an interval is not used, so that shape stays untranslated instead of declaring a `TimeSpan` the server would not send back. See §9 for the source.

A date part is read with `EXTRACT(PART FROM value)` for a timestamp, a date and a time of day alike; `DayOfYear` and `Millisecond` use `DATEPART(part, value)`, because `EXTRACT` has no such part. The reference provider reads every member with `DATEPART`; the two agree on the parts this dialect translates, and the live matrix decides which one the server accepts on a `TIME` column.

`Guid.NewGuid()` renders `NEWID()`, the function the reference provider uses; Dameng's `GUID()` answers a 32-character string without the hyphens and is not used.

This is the reason the `Dialect/` layer exists: these are written once rather than once per version.

The concrete values in that layer — store type names, maximum lengths, default precision and scale — are confirmed against a live server catalog in roadmap phase P1. A correction is made in `Dialect/` and propagates to type mapping and DDL generation without touching pipeline code.

## 9. Sources

- [EF Core 8 breaking changes](https://learn.microsoft.com/ef/core/what-is-new/ef-core-8.0/breaking-changes)
- [EF Core 9 breaking changes](https://learn.microsoft.com/ef/core/what-is-new/ef-core-9.0/breaking-changes)
- [EF Core 10 breaking changes](https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/breaking-changes)
- [EF Core 11 breaking changes](https://learn.microsoft.com/ef/core/what-is-new/ef-core-11.0/breaking-changes)
- [EF Core 11 provider-facing changes](https://learn.microsoft.com/ef/core/what-is-new/ef-core-11.0/provider-facing-changes)
- [EF Core releases and planning](https://learn.microsoft.com/ef/core/what-is-new/)
- [.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)
- [DM SQL 开发指南: 日期运算](https://eco.dameng.com/document/dm/zh-cn/sql-dev/practice-date.html) — two dates subtract to a number of days, which is why a date difference is not rendered as an interval
- [Oracle interval conversion functions under DM8](https://tidba.com/oracle-to-dm/interval-conversion-funcs/) and [a `NUMTODSINTERVAL` report](https://eco.dameng.com/community/question/f9f8c1e1e53e18977f5d541f9a2a900b) — the conversion exists but is behind, or fails under, Oracle compatible mode
- [Empty string and NULL after a migration from Oracle](https://eco.dameng.com/community/article/20260807112730LSO2E8Z3PLEEGLVZF7) — the two are the same value only when `COMPATIBLE_MODE` is 2, so a translator cannot decide it
- [`TO_DAYS` returns a count that disagrees with the difference to year zero](https://eco.dameng.com/community/question/84b0feac5606c18936a18edcbab44cde)
- [`GUID()` returns a 32-character string, reported as 31 in one version](https://eco.dameng.com/community/question/fcc917faa4035262f9c9915cf929fe2b) — which is why a `Guid` stays client generated

Source verification performed directly against the `dotnet/efcore` repository (`release/8.0`, `release/9.0`, `release/10.0`, `main`) and against published package metadata for EF Core 8.0.34, 9.0.22, 10.0.14 and 11.0.0-rc.3. The 8.0.34 patch was confirmed from the nuget.org package index on 2026-11-13.
