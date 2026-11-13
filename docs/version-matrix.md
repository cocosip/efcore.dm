# Version Matrix

Status: **living document**, maintained on the branch that owns the EF Core line, with `dev` and `master` reserved for the next line (EF Core 11).

Answers one question: **when moving the provider from EF Core version A to version B, what must change?**

Used in two directions: forward when adopting a new EF Core major version, backward when creating a maintenance branch.

## 1. Target frameworks and support

| EF Core | Target framework | Official support until | Branch | Specification tests |
|---|---|---|---|---|
| 8.0 | `net8.0` | 2026-11-10 (expired) | `efcore/8.0` | 8.0.33 |
| 9.0 | `net8.0` | 2026-11-10 (expired) | `efcore/9.0` | 9.0.22 |
| 10.0 | `net10.0` | 2028-11-10 | `efcore/10.0` | 10.0.14 |
| 11.0 | `net11.0` | not released | `efcore/11.0` (not created) | 11.0.0-rc (xUnit v3) |

EF Core 8 and 9 both ship `net8.0` assets only, so one package cannot carry distinct dependency ranges for them. EF Core 10 ships `net10.0` only and EF Core 11 ships `net11.0` only, so adoption of each major version is a separate build and, from EF Core 11 onward, a runtime cutover.

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
| `IMethodCallTranslator.Translate` and `IMemberTranslator.Translate` take `IDiagnosticsLogger<DbLoggerCategory.Query>` instead of a type mapping | Every translator signature changes | same |
| `net10.0` only | Target framework switch | package metadata |

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

Applied when creating a maintenance branch, newest to oldest.

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

Source verification performed directly against the `dotnet/efcore` repository (`release/8.0`, `release/9.0`, `release/10.0`, `main`) and against published package metadata for EF Core 8.0.33, 9.0.22, 10.0.14 and 11.0.0-rc.3.
