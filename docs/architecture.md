# Architecture

Status: **decided**. Changes to this document require review.

## 1. Scope

This repository provides an Entity Framework Core database provider for Dameng (DM), a Chinese relational database. The provider is written from scratch and released under MIT.

It supports multiple EF Core major versions. Each major version is maintained on its own branch (see [Branching and Release](branching-and-release.md)).

## 2. Goals

| ID | Goal |
|---|---|
| G1 | Original implementation under MIT, with traceable provenance |
| G2 | Simultaneous support for EF Core 8, 9 and 10, each released independently |
| G3 | Acceptance defined by the EF Core specification test suite, not by self-authored SQL assertions |
| G4 | Live Dameng verification is a prerequisite for declaring work complete |
| G5 | Cost of adopting a new EF Core major version stays predictable |
| G6 | Source-compatible with existing consumers: same root namespace and `UseDm()` entry point, so migration requires a package reference change but no code changes |

## 3. Non-goals

- Forking or reimplementing `DM.DmProvider`, the ADO.NET driver.
- Supporting EF Core 7 or earlier.
- Branching the SQL dialect on Dameng `CompatibleMode`. The provider targets Dameng's default (Oracle-compatible) behaviour. See D4.
- Provider-agnostic abstraction layers, plugin systems, or code generators.
- Any ORM surface beyond EF Core.

## 4. Constraints

| Constraint | Consequence |
|---|---|
| EF Core providers inherit from types shaped by internal APIs | The EF Core dependency must carry an upper bound. Any upgrade requires regression testing. |
| EF Core 8 and 9 both ship `net8.0` assets only | A single package cannot carry both dependency ranges. Separate branches per major version. |
| EF Core 10 ships `net10.0` only; EF Core 11 ships `net11.0` only | Each major version is a separate build, not a TFM group in one package |
| Dameng dialect knowledge does not depend on the EF Core version | Dialect data is shareable across branches; API shapes are not |
| `DM.DmProvider` has no `net10.0` asset; EF Core 10 consumes its `lib/net9.0` asset | Cross-TFM asset compatibility must be verified explicitly |

### Measured volatility

A diff of the EF Core 9 to EF Core 10 migration in the reference implementation shows:

```
src:   24 of 114 files changed (21%), +1132 / -310 lines
       concentrated in Query/ (9 files), Migrations/ (3), Storage/ (2), Update/ (1)
```

Roughly 80% of provider code is stable across major versions. The unstable 20% is the set of classes that inherit EF Core base types. The architecture must make the stable majority reusable and isolate the unstable minority, without introducing an abstraction layer over both.

## 5. Layering

```
L6  Public API and diagnostics        UseDm(), property builders, event ids, logging
L5  Value generation                  identity, sequence, HiLo
L4  Migrations and scaffolding        migrations SQL, history, lock, model factory, design time
L3  Update pipeline                   modification batches, update SQL, store-generated readback
L2  Query pipeline                    query SQL, translators, visitors, nullability
L1  Storage                           type mappings, connection, transaction, execution strategy
L0  Dialect                           type names, identifier rules, DDL, catalog SQL, error codes
                                      -- no EF Core types --
```

Dependencies point downward only. No layer references a layer above it.

### L0 — Dialect

Directory: `src/DM.EntityFrameworkCore/Dialect/`

Contains plain data and string logic with no EF Core type references:

| Content | Examples |
|---|---|
| Store type names | `VARCHAR`, `NVARCHAR2`, `TIMESTAMP`, `INTERVAL DAY TO SECOND`, `BLOB`, `ROWID` |
| Size and precision rules | maximum string length, default `DECIMAL(29,4)`, `TIMESTAMP(6)` precision |
| Identifier rules | double-quote delimiting, `"` escaped as `""` |
| Parameter rules | `:` prefix, parameter name generation |
| Batch terminator | `/` |
| DDL fragments | `IDENTITY(1,1)`, table, index and sequence templates |
| Catalog queries | statements against `SYSOBJECTS` and related system tables |
| Error classification | Dameng error number to retriable, constraint violation, or syntax error |

**Enforced rule.** A source file under `Dialect/` may not depend on EF Core. The rule is enforced by `DialectLayerIsolationTests` in the unit test project, which scans the directory and fails when a non-comment line names an EF Core type. Comments are ignored, because a comment creates no dependency. The check is a plain file scan; no architecture test framework is used.

### L1–L6

Organized to mirror EF Core's own source layout: `Storage/`, `Query/`, `Update/`, `Migrations/`, `Scaffolding/`, `Metadata/`, `ValueGeneration/`, `Infrastructure/`, `Diagnostics/`, `Design/`, `Extensions/`.

Each EF Core major version implements these layers independently. There is no cross-version abstraction over them, for three reasons:

1. Base type signatures change between majors, so a cross-version interface becomes a second synchronization burden.
2. Established providers (Npgsql, Pomelo, Oracle, SQL Server) maintain one branch per major version with no cross-version abstraction.
3. An abstraction can only unify concepts the provider itself invents. It cannot absorb changes in EF Core.

## 6. Shared versus version-specific

| Content | Owner | Cross-branch flow |
|---|---|---|
| `Dialect/` | Same path on every branch | Allowed, one way: `dev` to maintenance branches |
| L1–L6 pipeline code | Each branch | Forbidden |
| EF Core dependency range, target framework | Each branch | Forbidden |
| `docs/` | `dev` only | Not copied; maintenance branches read `dev` |
| Specification test skip ledger | Each branch, separate file | Forbidden |
| CI configuration | Each branch | As needed |

## 7. Public surface

| Item | Value | Rationale |
|---|---|---|
| Package id | `Cocosip.DM.EntityFrameworkCore` | Follows the EF provider naming convention `<prefix>.EntityFrameworkCore.<database>`. The prefix is the publisher's own GitHub identity, independent of any employer and demonstrably owned. Defined once as `$(DmPackageId)`. |
| Assembly name | `DM.EntityFrameworkCore` | The product name, deliberately not the publish identity. Naming the assembly after the official provider would ship a Microsoft-prefixed binary under an MIT package, which carries trademark exposure and no functional benefit. |
| Project directory | `src/DM.EntityFrameworkCore/` | Same as the assembly and the solution. Follows the assembly, not the package id. |
| Root namespace | The namespaces the Dameng provider declares for the compatibility surface; `DM.EntityFrameworkCore` for internals | Preserves source compatibility with existing consumer code, while keeping provider internals under the product's own namespace. See the namespace rule below. |
| Entry point | `optionsBuilder.UseDm(connectionString)` | Preserves source compatibility with existing consumer code |
| Target framework | EF Core 8/9 branches: `net8.0`; EF Core 10: `net10.0`; EF Core 11: `net11.0` | Follows EF Core requirements |
| Version | Aligned to the EF Core major version: `8.0.x`, `9.0.x`, `10.0.x` | See branching and release |
| EF Core dependency | `[8.0.x,9.0.0)`, `[9.0.x,10.0.0)`, `[10.0.x,11.0.0)` | Upper bound is mandatory. See D5. |
| Driver dependency | `DM.DmProvider` >= 8.3.1.47463 | Lower bound advances with verification |

### Naming rules

- The package id shape is `<prefix>.EntityFrameworkCore.<database>`, matching the EF Core provider ecosystem.
- The prefix is the publisher's personal open-source identity, not an employer name. It must be one the publisher can demonstrate ownership of, and it is registered with NuGet prefix reservation. A generic English word cannot be reserved and shares the namespace with unrelated publishers, so it is not used.
- The package id is `<prefix>.<product>`; the project directory, assembly name and internal root namespace are the product alone. Changing the NuGet prefix changes one MSBuild property and nothing else: no directory rename, no assembly rename, no namespace rename, no test project rename.
- The assembly is not named after the official provider. Shipping a Microsoft-prefixed binary under an MIT package carries trademark exposure and buys nothing.
- The previous package id is deprecated on NuGet with a pointer to the current one. It is not unpublished, because unpublished ids cannot be restored by existing builds.

### Namespace rule

The project's root namespace is `DM.EntityFrameworkCore`, so a type declared without an explicit namespace block lands there by default. Provider internals live in that namespace tree.

Only the source-compatibility surface is declared under the namespaces the official Dameng provider uses, so that consumer source compiles without a new `using` directive:

| Namespace | Types |
|---|---|
| `Microsoft.EntityFrameworkCore` | `UseDm`, `DmPropertyExtensions`, `DmPropertyBuilderExtensions`, `DmModelBuilderExtensions`, `DmModelExtensions` |
| `Microsoft.EntityFrameworkCore.Infrastructure` | `DmDbContextOptionsBuilder` |
| `Microsoft.EntityFrameworkCore.Metadata` | `DmValueGenerationStrategy` |

Everything else is ours and stays under the `DM.EntityFrameworkCore` root namespace, which also keeps the `Dialect/` isolation rule satisfiable.

## 8. Repository layout

```
DM.EntityFrameworkCore.slnx
Directory.Build.props
Directory.Packages.props
src/DM.EntityFrameworkCore/                                            provider
tests/DM.EntityFrameworkCore.Tests/                                    unit and SQL-shape tests
tests/DM.EntityFrameworkCore.SpecificationTests/                       EF Core specification suite
samples/DmProviderValidation/                             live-database acceptance console
docs/
```

## 9. Cross-cutting rules

| Concern | Rule |
|---|---|
| Credentials | Connection strings never appear in logs, exception messages, or diagnostic events. Options debug views mask the password. |
| Cancellation | Every asynchronous command and reader path honours `CancellationToken`. Where the driver does not honour the token itself, the provider supplies an interceptor that binds the token to `DbCommand.Cancel()`. Cancellation is a provider responsibility, not a driver assumption. |
| Retry | `ExecutionStrategy` decides retryability from the L0 error classification. |
| Logging | Provider event ids occupy their own range and never reuse EF Core's. |
| Design time | `DesignTimeProviderServices` points to this project's `DmDesignTimeServices` so `dotnet ef migrations` and `dotnet ef dbcontext scaffold` work. |
| Reverse engineering | `IDatabaseModelFactory` issues the L0 catalog queries. No ad-hoc SQL in the factory. |
| Performance | Type mapping lookup and SQL generation avoid reflection on hot paths. Batch sizing respects Dameng's parameter limit. |

## 10. Code conventions

| Rule | Value |
|---|---|
| Language version | `latest`, set once in `Directory.Build.props` |
| Nullable reference types | Enabled; annotations are accurate, no blanket `!` |
| Implicit usings | Enabled |
| Warnings | Treated as errors. A build with warnings is a failed build. |
| Target framework | Set once as `$(DmTargetFramework)` in `Directory.Build.props`; retargeting a branch is a one-line change |
| Package versions | Central management through `Directory.Packages.props`. The specification test project is exempt, because the EF Core test package pins its own dependency versions. |
| File layout | One public type per file, file name equal to the type name, directory mirroring the EF Core area |
| Test naming | `Method_Scenario_ExpectedOutcome`; test fixtures end in `Tests` |
| Async | `ConfigureAwait(false)` is not used; EF Core and the provider run without a synchronization context |
| Public API | Provider internals are `internal` unless a consumer must name the type. Internals are visible to the test assembly only. |

## 11. Decisions

| ID | Decision | Reason |
|---|---|---|
| D1 | Layers L0–L6; only L0 is shared across versions | Measured 80% stability; an abstraction over the volatile 20% becomes a second synchronization burden |
| D2 | One branch per EF Core major version; no multi-targeting with `#if` | EF Core 8 and 9 both target `net8.0`, so one package cannot select dependency ranges by TFM. Conditional compilation would make the query pipeline unreadable. |
| D3 | The package id carries the `Cocosip` prefix; the source tree, assembly and internal namespace use the product name `DM.EntityFrameworkCore`; the compatibility surface stays in the namespaces the Dameng provider declares | NuGet only grants prefix protection for a prefix the publisher can show is theirs. `Sharp` is a generic word already shared in the EF provider category with an unrelated publisher, so that namespace can never be reserved. An employer's brand ties the package to that employer. `Cocosip` is the publisher's own open-source identity and is already established on NuGet. Publish identity is separated from source identity, so changing the prefix is a one-line edit rather than a repository-wide rename. The compatibility namespace is kept so that consumer source compiles unchanged; only the package reference changes. |
| D4 | Do not branch the dialect on `CompatibleMode` | Switching SQL dialect per connection string couples type mapping and SQL generation implicitly. The provider targets default (Oracle-compatible) mode. PostgreSQL mode is a verification target, not an implementation branch. |
| D5 | EF Core dependency always carries an upper bound | The provider uses internal API shapes; an unbounded dependency leads to `MissingMethodException` and `TypeLoadException` on the next major version. |
| D6 | The EF Core specification suite is the primary gate; self-authored SQL assertions are secondary | A large suite of self-authored SQL assertions can pass while the provider remains unusable. |
| D7 | Migrations locking uses a lock table rather than a server-level lock primitive | Dameng exposes no session-level advisory lock suitable for `IMigrationsDatabaseLock`. A lock table matches the approach used by providers on similar engines. |
| D8 | A store-generated value is read back by a second statement after the insert, not by `RETURNING` | Dameng accepts `RETURNING` only as `RETURNING ... INTO <variable>`, which binds to a PL/SQL variable instead of producing a result set, so the relational layer's result-set contract cannot be met that way. A `SELECT` matched by `sql%ROWCOUNT = 1` and `scope_identity()` reuses the reader path EF Core already provides: the row count predicate turns a lost update into a concurrency failure, and no output parameter or hand-built command is needed. This shape is taken from the behaviour of the official provider; it has not been run against a server yet. |
| D9 | A hi-lo block is reserved by advancing a model sequence whose increment is the block size | The block state lives in the singleton value generator cache, so the reservation has to be recorded where a second process can see it, and a sequence is the only such record. Making the increment the block size means one fetch reserves exactly one block, and a model that declares a different increment gets blocks of that size without any extra configuration. |
