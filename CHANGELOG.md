# Changelog

All notable changes to this project are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html) aligned to the EF Core major version.

The capability matrix records what the provider supports today; the skip ledger records the cases that
do not pass and why. This file records what changed, once per release, instead of narrating every run.

## [Unreleased]

This branch is `efcore/8.0`, cut from `efcore/9.0` at `3e937a2` on 2026-11-13. The entries below are inherited
from the 9 line. The 8 line's own gate ran on 2026-11-13 against DM 8.1.4.80: the specification suite
reports 3780 results, 3711 passed, 69 skipped and 0 failed, the acceptance console 32 of 32 steps in both
compatibility modes, the design-time smoke test passed end to end, and the feature probe 44 steps in each
mode. `v8.0.1` is not published yet.

### Changed on `efcore/8.0` (EF Core 8)

- The provider targets `net8.0` and depends on `Microsoft.EntityFrameworkCore` `[8.0.0,9.0.0)`; on this line
  every EF Core package is a range whose floor is 8.0.0.
- `global.json` pins SDK 8.0.404 and the solution is a classic `.sln`: the 8.0 SDK binds C# 12, which the
  EF Core 8 query pipeline expects, and CI installs 8.0.x.
- The migrations lock is removed: EF Core 8's `HistoryRepository` declares no lock members, so no
  `DBMS_LOCK` named lock is taken and the acceptance console declares 32 steps.
- The parameterized-collection translation option is removed with its tests; a collection parameter is
  still rendered as one JSON parameter read through `JSON_TABLE`.
- `Math.Max` and `Math.Min` are translated by the provider's math translator, because EF Core 8 has no
  `GenerateGreatest`/`GenerateLeast` hook.
- The specification suite is rewritten against the 8 base classes, and the skip ledger's rows for cases the
  8 bases do not declare are removed.

### Fixed on `efcore/8.0` (EF Core 8)

- The specification fixtures seed through the synchronous `Seed`, because EF Core 8 never calls the
  asynchronous one; the keyless `CustomerQueries` rows the fixture projects from `Customers` now exist, and
  the two keyless cases that failed pass.
- A column shared with an owned type keeps its `IDENTITY` attribute: the annotation provider asks every
  property mapping of a column instead of the first, whose strategy is `None` for the owned type's key.
- A boolean `&` or `|` is translated to `AND` or `OR`: EF Core 8 renders the operator, which the server
  rejects with `语法分析出错`.
- `IndexOf("")` answers 0 as .NET does, where the server answers `INSTR(x, '')` with null.
- The design-time smoke project targets `net8.0` with the `[8.0.0,9.0.0)` dependency range instead of
  `net10.0` with `10.0.14`, so `dotnet ef` can create the context the provider implements.

### Added
- EF Core 10 provider for Dameng (`net10.0`), package `Sharp.DM.EntityFrameworkCore`.
- Query translation for the relational operators, `DbFunctions` date differences, `ISDATE`, full-text
  `CONTAINS`/`FREETEXT`, and the language and nanosecond forms the reference provider exposes.
- Migrations DDL generation and a migrations specification suite (134 cases).
- Reverse engineering of tables, columns, indexes, foreign keys, views, comments and sequences,
  including the declared sequence start value.
- The specification suite bases: built-in data types, store generated values, `Where`, `Navigations`,
  `Miscellaneous`, `BulkUpdates`, `PrimitiveCollections`, `Join`, `Select`, `GroupBy`,
  `AggregateOperators`, `Include`, `Updates` and `Migrations`.
- Query translation for `Contains` over a mapped primitive collection, expanded through the server's
  `JSON_TABLE` table function by the provider's own subquery visitor.
- Query translation for an `EF.Parameter` collection: the parameter is given the collection type
  mapping, serialized as a JSON array and read through `JSON_TABLE`, so the four specification cases
  that depended on it now run.
- EF Core 10 SQL compatibility surface for existing Dameng consumers.

### Fixed

- The full-text translator no longer claims every method named `Contains` or `FreeText`: an entity
  `Contains` (including `List<T>.Contains` and a navigation collection) was translated as a full-text
  call and threw during translation. It now accepts only methods declared by `DmDbFunctionsExtensions`.
- `uint.ToString()` and the other unsigned integer types are translated to `TO_CHAR`, as the signed
  types already were.
- A culture-sensitive `string.Equals(..., StringComparison)` is left to the client instead of being
  silently compared as an ordinal one.
- A token that is already cancelled throws `OperationCanceledException` before the driver runs the
  command, rather than surfacing the driver's `TaskCanceledException`.
- A primitive collection backed by an array is translated: the element type came from
  `GetGenericArguments()[0]`, which an array has none of, so every array-backed collection threw
  `IndexOutOfRangeException` before the `JSON_TABLE` expansion.
- A nullable primitive-collection element is not nested in `Nullable<T>` twice.
- Raw SQL that carries SQL Server's bracket delimiters is normalized to Dameng's double quotes in the
  specification fixture, so `FromSql`-composed bulk commands reach the table the model maps.
- Identifier limit declared at 128 characters, so generated names no longer fail after their tables exist.
- An unbounded `byte[]` concurrency token is `VARBINARY(900)` rather than `BLOB`, so updates and deletes
  are not rejected by the server.
- The provider's concurrency message now carries the framework's explanatory link.
- A virtual column is written without a data type (`不支持为虚拟列指定的数据类型`).
- A collation value is written quoted (`COLLATE "BINARY"`).
- Column comments are read from `SYS.SYSCOLUMNCOMMENTS`, where they live on 8.1.4.80.
- Indexes the server creates for keys (`INDEX<digits>`) are not scaffolded as model indexes.
- An unbounded string maps to `CLOB`/`NCLOB` instead of a typed column that cannot hold it.
- A foreign key delete rule is read back from the catalog.
- A descending index keeps its direction.
- A comment-only alteration no longer rewrites an identity column.
- `ALTER SEQUENCE ... RENAME TO` is generated for a rename inside the schema.
- The declared sequence start value is read back from the sequence object.
- A self-referencing foreign key is added after the indexes of a script.
- A condition used as an operand of a comparison is rendered as `CASE WHEN ... THEN 1 ELSE 0 END`.
- A nested `Take` composed through `LEAST`, a bare condition in a `CASE` result and an `EXTRACT`
  keyword propagating nullability were corrected.
- A collection join is written as a subquery the server parses, and a tautological join predicate is not
  written as a bare `1`.
- `ExecuteUpdate` and `ExecuteDelete` rewrite a boolean predicate, including a query filter's, the way the
  select pipeline does; the update commands are built outside `VisitSelect`, so the filter used to reach the
  server as a value expression (`查询使用值表达式作为过滤条件`).
- A boolean ordering is written as `CASE WHEN ... THEN 1 ELSE 0 END`: an ordering over a predicate was
  rendered as `COALESCE(<condition>, 0)` and rejected (`无效的ORDER BY子句`).
- A boolean exclusive-or is translated as a comparison of its operands' `CASE` forms, because the server
  has no `^` operator (`语法分析出错`).
- A paged primitive collection orders its `JSON_TABLE` row source by an ordinality column, so the same
  page is asked for each time: the row source declared no ordering and the server answered a different
  page (`FETCH FIRST` over an unordered row source).

### Changed

- The package prefix is `Sharp`; the assembly, namespaces and project directory remain
  `DM.EntityFrameworkCore`.
- CI runs on `master` and the version branches and publishes only from a version tag, using NuGet
  trusted publishing instead of a stored API key.

[Unreleased]: https://github.com/cocosip/efcore.dm/commits/master
