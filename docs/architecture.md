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
| G6 | Source-compatible with existing consumers: the consumer-facing types keep the namespaces the official provider declares and the entry point stays `UseDm()`, so migration requires a package reference change but no code changes |

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
| Store type names | `NVARCHAR2`, `VARCHAR2`, `TIMESTAMP`, `INTERVAL DAY TO SECOND`, `BLOB`, `ROWID` |
| Size and precision rules | the length a bounded string may name, the length a key takes when none is named, default `DECIMAL(29,4)`, the precision a `TIME` column names |
| Identifier rules | double-quote delimiting, `"` escaped as `""` |
| Parameter rules | `:` prefix, parameter name generation |
| Batch terminator | `/` |
| DDL fragments | `IDENTITY(1,1)`, table, index and sequence templates |
| Catalog queries | statements against `SYSOBJECTS` and related system tables |
| Error classification | Dameng error number to retriable, constraint violation, or syntax error |

**Enforced rule.** A source file under `Dialect/` may not depend on EF Core. The rule is enforced by `DialectLayerIsolationTests` in the unit test project, which scans the directory and fails when a non-comment line names an EF Core type. Comments are ignored, because a comment creates no dependency. The check is a plain file scan; no architecture test framework is used.

### L1–L6

Organized to mirror EF Core's own source layout: `Storage/`, `Query/`, `Update/`, `Migrations/`, `Metadata/`, `ValueGeneration/`, `Infrastructure/`, `Diagnostics/`, `Design/`. The types consumers compile against sit apart in `Compatibility/`, so no directory mixes provider internals with those namespaces; see the namespace rule in section 7. `Scaffolding/` joins the list when reverse engineering is implemented.

Each EF Core major version implements these layers independently. There is no cross-version abstraction over them, for three reasons:

1. Base type signatures change between majors, so a cross-version interface becomes a second synchronization burden.
2. Established providers (Npgsql, Pomelo, Oracle, SQL Server) maintain one branch per major version with no cross-version abstraction.
3. An abstraction can only unify concepts the provider itself invents. It cannot absorb changes in EF Core.

## 6. Shared versus version-specific

| Content | Owner | Cross-branch flow |
|---|---|---|
| `Dialect/` | Same path on every branch | Allowed, one way: newest version branch to older ones |
| L1–L6 pipeline code | Each branch | Forbidden |
| EF Core dependency range, target framework | Each branch | Forbidden |
| `docs/` | Version branch | Travelled by merge to `dev` and `master`; an older version branch reads the newest |
| Specification test skip ledger | Each branch, separate file | Forbidden |
| CI configuration | Each branch | As needed |

## 7. Public surface

| Item | Value | Rationale |
|---|---|---|
| Package id | `Sharp.DM.EntityFrameworkCore` | The configured prefix `$(DmPackagePrefix)` plus the package's own identity, here the assembly name. Only the prefix is a configured value, so every package in the repository derives its id from it. `Sharp` is product-neutral rather than a personal identity; the id was verified free on nuget.org on 2026-11-12 (`https://api.nuget.org/v3-flatcontainer/sharp.dm.entityframeworkcore/index.json` answered 404). |
| Assembly name | `DM.EntityFrameworkCore` | Equal to the root namespace and the project directory, which is how EF Core providers name their assemblies. Deliberately not named after the official provider: a Microsoft-prefixed binary under an MIT package carries trademark exposure and no functional benefit. |
| Project directory | `src/DM.EntityFrameworkCore/` | Same as the assembly, the root namespace and the solution. |
| Root namespace | `DM.EntityFrameworkCore` for internals; the namespaces the Dameng provider declares for the compatibility surface | Preserves source compatibility with existing consumer code, while keeping provider internals under the product's own namespace. See the namespace rule below. |
| Entry point | `optionsBuilder.UseDm(connectionString)` | Preserves source compatibility with existing consumer code |
| Target framework | EF Core 8/9 branches: `net8.0`; EF Core 10: `net10.0`; EF Core 11: `net11.0` | Follows EF Core requirements |
| Version | Aligned to the EF Core major version: `8.0.x`, `9.0.x`, `10.0.x` | See branching and release |
| EF Core dependency | `[8.0.0,9.0.0)`, `[9.0.0,10.0.0)`, `[10.0.0,11.0.0)` | Upper bound is mandatory (D5). The floor is the first release of the major version, so a consumer on any patch of that major can install the package, and the provider is compiled against that floor. |
| Driver dependency | `DM.DmProvider` >= 8.3.1.47463 | Lower bound advances with verification |
| Authors | `cocosip` by default | The repository owner's account. It is configured once for the repository, not per project |
| Repository and project URL | `https://github.com/cocosip/efcore.dm` | Where the source lives, so a consumer reaches it from the package listing. It is a default a satellite package can override, like the author |
| Tags | `efcore`, `entity-framework-core`, `dameng`, `dm`, `database`, `provider` | The terms a search on the feed matches. They name what the package is for, not what it happens to contain |
| Licence in the package | `MIT` as the licence expression, plus the `LICENSE` file at the package root | The expression is what the feed displays; the file is the text that travels with the copy, which MIT requires. Both are packed, so a consumer that unpacks the package has the terms without a network round trip |
| Documentation file | `lib/net10.0/DM.EntityFrameworkCore.xml`, beside the assembly | A package that ships its summaries is the difference between IntelliSense and none. The project builds with the file on and treats warnings as errors, so an undocumented public member fails the build instead of leaving a blank tooltip |
| Public plumbing | `DmOptionsExtension`, public under `DM.EntityFrameworkCore.Infrastructure` | It is the type argument of `DmDbContextOptionsBuilder`'s base class, and a public base type cannot be built over an internal one. The EF Core providers declare their own options extensions the same way. Nothing a consumer calls names it |

### Naming rules

- The assembly name, root namespace and project directory are one identity: `<database>.EntityFrameworkCore`, here `DM.EntityFrameworkCore`. This is the identity consumers see in a stack trace or an assembly reference.
- The package id is `<prefix>.` plus the package's identity, here `Sharp.DM.EntityFrameworkCore`. Only `$(DmPackagePrefix)` is configured; `$(DmPackageIdBase)` is that prefix joined to the assembly name.
- The prefix `Sharp` is a product-neutral word chosen so the package does not carry an organization's brand. NuGet prefix reservation is only granted for a prefix the publisher demonstrates ownership of, so a generic word is not reserved: the exact id is checked against nuget.org instead, and a future package with the same prefix checks its own id. Anyone can claim an unreserved id, so the name is a convenience rather than protection.
- Publishing several packages is expected, as Npgsql does: the provider package plus optional feature packages. A satellite package is a new project with the same prefix and its own suffix, for example `$(DmPackageIdBase).NetTopologySuite` for spatial types. It adds no configuration outside its own project.
- Changing the NuGet prefix changes `$(DmPackagePrefix)` and nothing else: every package id follows, and no directory, assembly, namespace or test project is renamed.
- The assembly is not named after the official provider. Shipping a Microsoft-prefixed binary under an MIT package carries trademark exposure and buys nothing.
- No package has been published yet, so there is no previous id to deprecate. The first tag publishes `Sharp.DM.EntityFrameworkCore`; a later rename deprecates the old id with a pointer to the new one rather than unpublishing it, because unpublished ids cannot be restored by existing builds.

### Namespace rule

Each directory declares exactly one namespace. The path under `Compatibility/` is that namespace without its `Microsoft.` prefix. There are two trees:

- `Compatibility/` holds the types consumers compile against. Their namespaces are the ones the official provider declares, so consumer source keeps working after the package reference changes.
- Everything else is provider internals under the `DM.EntityFrameworkCore` root namespace, laid out the way EF Core lays out its own code.

| Namespace | Directory | Types |
|---|---|---|
| `Microsoft.EntityFrameworkCore` | `Compatibility/EntityFrameworkCore/` | `UseDm`; `DmPropertyExtensions`, `DmPropertyBuilderExtensions`; `DmModelExtensions`, `DmModelBuilderExtensions`; `DmDatabaseFacadeExtensions` |
| `Microsoft.EntityFrameworkCore.Diagnostics` | `Compatibility/EntityFrameworkCore/Diagnostics/` | `DmEventId` |
| `Microsoft.EntityFrameworkCore.Infrastructure` | `Compatibility/EntityFrameworkCore/Infrastructure/` | `DmDbContextOptionsBuilder` |
| `Microsoft.EntityFrameworkCore.Metadata` | `Compatibility/EntityFrameworkCore/Metadata/` | `DmValueGenerationStrategy` |
| `Microsoft.EntityFrameworkCore.Migrations` | `Compatibility/EntityFrameworkCore/Migrations/` | `DmMigrationBuilderExtensions` |
| `Microsoft.EntityFrameworkCore.Migrations.Operations` | `Compatibility/EntityFrameworkCore/Migrations/Operations/` | `DmCreateSchemaOperation`, `DmDropSchemaOperation`, `DmCreateUserOperation`, `DmDropUserOperation` |
| `Microsoft.Extensions.DependencyInjection` | `Compatibility/Extensions/DependencyInjection/` | `AddEntityFrameworkDm` |
| `Microsoft.EntityFrameworkCore.Dm.Extensions` | `Compatibility/EntityFrameworkCore/Dm/Extensions/` | `DmDbFunctionsExtensions` |

Provider internals are ours and stay under `DM.EntityFrameworkCore.*`, which also keeps the `Dialect/` isolation rule satisfiable.

The official provider declares its own internals under `Microsoft.EntityFrameworkCore.Dm.*`. Consumer code does not reference those, so this provider does not reproduce them. The single public type in that tree a consumer can call is `DmDbFunctionsExtensions`, which extends `EF.Functions`; it carries the date difference methods, and what it does and does not cover is recorded in the capability matrix.

## 8. Repository layout

```
.github/workflows/ci.yml                                  build, tests and the acceptance dry run; pack on a tag
DM.EntityFrameworkCore.slnx
Directory.Build.props
Directory.Packages.props
src/DM.EntityFrameworkCore/                                provider
  Compatibility/                                           public API in the namespaces the official provider declares
  Dialect/                                                 EF Core free dialect knowledge, shared across branches
  Design/  Diagnostics/  Infrastructure/  Metadata/        provider internals
  Migrations/  Query/  Storage/  Update/  ValueGeneration/
tests/DM.EntityFrameworkCore.Tests/                        unit and SQL-shape tests
tests/DM.EntityFrameworkCore.PackageConsumer/              package smoke test: consumes the packed provider
tests/DM.EntityFrameworkCore.SpecificationTests/           EF Core specification suite (planned)
scripts/package-smoke.ps1                                  packs, restores the consumer in an isolated cache, runs it
scripts/design-time-smoke.ps1                              runs the `dotnet ef` tooling against a live instance
samples/DmProviderValidation/                              live-database acceptance console (--dry-run builds the model and generates the schema without an instance)
docs/
```

Everything listed exists except the entries marked planned, which are created when their phase starts.
`RepositoryLayoutTests` holds both directions: a listed entry that is missing fails, and a directory at
the top level of the provider that is not listed fails.

## 9. Cross-cutting rules

| Concern | Rule |
|---|---|
| Credentials | Connection strings never appear in logs, exception messages, or diagnostic events. Options debug views mask the password. |
| Cancellation | Every asynchronous command and reader path honours `CancellationToken`. Where the driver does not honour the token itself, the provider supplies an interceptor that binds the token to `DbCommand.Cancel()`. Cancellation is a provider responsibility, not a driver assumption. |
| Retry | A unit of work runs once unless the consumer asks otherwise, because a retry would replay a statement whose failure may already have changed the database. `EnableRetryOnFailure` installs a strategy that retries the failures the provider classifies as transient, from the recorded Dameng error numbers, and closes a connection the failure broke before the retry. |
| Logging | Provider event ids occupy their own range and never reuse EF Core's. |
| Design time | `DesignTimeProviderServices` points to this project's `DmDesignTimeServices`, which is what lets `dotnet ef migrations` build and read a snapshot. Reverse engineering joins the same registration when the model factory exists. |
| Reverse engineering | `IDatabaseModelFactory` issues the L0 catalog queries. No ad-hoc SQL in the factory. |
| Performance | Type mapping lookup and SQL generation avoid reflection on hot paths. A save changes round trip carries one statement, so there is no batch to size against a parameter limit; the row for multiple statements per batch records what it would take to put more than one in a round trip. |

## 10. Code conventions

| Rule | Value |
|---|---|
| Language version | `latest`, set once in `Directory.Build.props` |
| Nullable reference types | Enabled; annotations are accurate, no blanket `!` |
| Implicit usings | Enabled |
| Warnings | Treated as errors. A build with warnings is a failed build. |
| Target framework | Set once as `$(DmTargetFramework)` in `Directory.Build.props`; retargeting a branch is a one-line change |
| Package versions | Central management through `Directory.Packages.props`. The specification test project is exempt, because the EF Core test package pins its own dependency versions. |
| File layout | One public type per file, file name equal to the type name. Each directory declares one namespace: provider internals mirror the EF Core area, and the consumer-facing types sit under `Compatibility/` with their path naming their namespace. `NamespaceLayoutTests` fails the build when a directory declares two namespaces or a compatibility type's namespace disagrees with its path. |
| Test naming | `Method_Scenario_ExpectedOutcome`; test fixtures end in `Tests` |
| Async | `ConfigureAwait(false)` is not used; EF Core and the provider run without a synchronization context |
| Public API | Provider internals are `internal` unless a consumer must name the type. Internals are visible to the test assembly only. |

## 11. Decisions

| ID | Decision | Reason |
|---|---|---|
| D1 | Layers L0–L6; only L0 is shared across versions | Measured 80% stability; an abstraction over the volatile 20% becomes a second synchronization burden |
| D2 | One branch per EF Core major version; no multi-targeting with `#if` | EF Core 8 and 9 both target `net8.0`, so one package cannot select dependency ranges by TFM. Conditional compilation would make the query pipeline unreadable. |
| D3 | The assembly, root namespace and project directory are one identity, `DM.EntityFrameworkCore`; every package id is the configured `Sharp` prefix plus its own identity, so several packages share one prefix; the compatibility surface stays in the namespaces the Dameng provider declares | The prefix is a product-neutral word, chosen over a personal identity so the package carries no organization's brand. NuGet grants prefix reservation only to a prefix the publisher can show is theirs, so `Sharp` is not reserved: each exact id is checked against nuget.org before the first publish, and the check is recorded with the decision. Only the prefix is configured, so a satellite package adds a project with no naming work and changing the prefix renames every package at once. The compatibility namespace is kept so that consumer source compiles unchanged; only the package reference changes. |
| D4 | Do not branch the dialect on `CompatibleMode` | Switching SQL dialect per connection string couples type mapping and SQL generation implicitly. The provider targets default (Oracle-compatible) mode. PostgreSQL mode is a verification target, not an implementation branch. |
| D5 | EF Core dependency always carries an upper bound | The provider uses internal API shapes; an unbounded dependency leads to `MissingMethodException` and `TypeLoadException` on the next major version. |
| D6 | The EF Core specification suite is the primary gate; self-authored SQL assertions are secondary | A large suite of self-authored SQL assertions can pass while the provider remains unusable. |
| D7 | Migrations locking uses a `DBMS_LOCK` named lock owned by the session rather than a lock table | A table lock sounded right and guarded nothing: measured on 8.1.4.80, Dameng released it at the first DDL statement of a migration, because the server commits around DDL, and a lock taken without a transaction was released immediately, so a second session took it in 2 milliseconds. A `DBMS_LOCK` named lock survived the same DDL and still blocked a second session. The id must stay below 2^30 and `DBMS_LOCK.ALLOCATE_UNIQUE` does not exist, so the id is a hash of the database name, which is the same in every process. |
| D8 | A store-generated value is read back by a second statement after the insert, not by `RETURNING` | Dameng accepts `RETURNING` only as `RETURNING ... INTO <variable>`, which binds to a PL/SQL variable instead of producing a result set, so the relational layer's result-set contract cannot be met that way. A `SELECT` matched by `sql%ROWCOUNT = 1` and `scope_identity()` reuses the reader path EF Core already provides: the row count predicate turns a lost update into a concurrency failure, and no output parameter or hand-built command is needed. This shape is taken from the behaviour of the official provider. It was executed against a server on 2026-11-11 in both compatibility modes, where the identity value reached the entity. |
| D9 | A hi-lo block is reserved by advancing a model sequence whose increment is the block size | The block state lives in the singleton value generator cache, so the reservation has to be recorded where a second process can see it, and a sequence is the only such record. Making the increment the block size means one fetch reserves exactly one block, and a model that declares a different increment gets blocks of that size without any extra configuration. |
| D10 | A `string` property maps to the unicode store type unless the model asks for the ansi one | The reference provider and the framework's own providers for the engines Dameng imitates all default a string to the national type, so a model written against them keeps the columns it declared instead of showing every string column as changed. A .NET string is UTF-16 while the ansi type is written in the database character set, which is the second reason, though a weaker one than it looks: Dameng has a single database character set, so the national type does not carry the Oracle dual charset guarantee, and whether a character outside the database set can be stored at all is a property of the database, not of the type. `IsUnicode(false)` is the opt-in to the ansi type, which is the shape the reference provider's own migrations show for such a column. The names are the reference provider's, `VARCHAR2` and `NVARCHAR2`, which the manual says are used the same way as `VARCHAR` and which its catalog reports, so a migration written by either provider reads the same in the other. Recorded 2026-11-11. |
| D11 | A JSON column maps through a `JsonTypeMapping` and a member is written with `JSON_SET` | EF Core 10 looks the container column's store type up by `JsonTypePlaceholder` and refuses the model when the provider answers nothing; the same mapping supplies the reader path, which is the string one turned into the UTF-8 stream the framework parses, because the driver hands a `JSON` column over as text. A change inside a document reaches the update pipeline as a value for a path rather than as the document, so the column is written with `JSON_SET`, measured on 8.1.4.80 to answer `{"a":2}` for `JSON_SET('{"a":1}', '$.a', 2)`; writing that value plainly would replace the document with it, which is data loss rather than an error. The root path is the exception: `JSON_SET(doc, '$', '{"b":2}')` answers the document as a quoted string, so it is written as the column value. Recorded 2026-11-11. |


