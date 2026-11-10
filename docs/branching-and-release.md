# Branching and Release

Status: **decided**. Changes to this document require review.

Governing rule:

> A branch exists for as long as it still receives new capabilities. A version that only receives defect fixes is a tag plus a temporary hotfix branch, not a long-lived branch.

## 1. Branches

| Branch | Role | EF Core | Target framework | Package line | EF Core dependency |
|---|---|---|---|---|---|
| `dev` | Primary development. New capabilities land only here. | 10 | `net10.0` | `10.0.x` | `[10.0.0,11.0.0)` |
| `efcore9` | Maintenance: capability backport on demand, defect fixes | 9 | `net8.0` | `9.0.x` | `[9.0.0,10.0.0)` |
| `efcore8` | Maintenance: baseline capabilities, defect fixes | 8 | `net8.0` | `8.0.x` | `[8.0.0,9.0.0)` |
| `hotfix/X.Y.Z` | Temporary, created from a release tag | as source tag | as source tag | as source tag | as source tag |

`dev` always targets the newest EF Core major version this project supports. When a newer major version is adopted, `dev` moves to it and the previous major version receives a maintenance branch, created from the last release tag of that line.

Branch names use the EF Core major version (`efcore9`, `efcore8`), matching the package version line one to one and leaving no ambiguity about which EF version a branch serves.

Reserved names that are not used: `master` or `main` (duplicate of `dev`), `develop` (meaningless prefix), a `release/*` branch per version where the version only receives fixes.

## 2. Creating a branch

**Create from a release tag, not from mid-development; chain the branches.**

```
dev --*--*--*--*-------------->  10.0.x continues
      \
       v10.0.x   (tag, acceptance passed)
            \
             +--> efcore9 -->  9.0.x
                       \
                        v9.0.x (tag)
                             \
                              +--> efcore8 -->  8.0.x
```

1. The fork point is a commit that passed acceptance and carries a tag. The maintenance branch therefore starts from a verified state rather than from unfinished work.
2. Branches chain: `dev` to `efcore9` to `efcore8`. Each step absorbs exactly one generation of difference. Forking `efcore8` directly from `dev` would consume two generations at once, making it impossible to attribute a change to EF Core 9 or EF Core 8.
3. Maintenance branches never merge back into `dev`. A defect found on a maintenance branch that also affects newer versions is fixed independently on each affected branch.

## 3. Synchronization

| Content | Direction | Mechanism |
|---|---|---|
| `Dialect/` | `dev` to maintenance branches only | `git checkout dev -- src/*/Dialect/` or cherry-pick |
| New dialect capabilities | `dev` to maintenance branches, on demand | See capability tiers |
| L1–L6 pipeline code | Never | Each branch maintains its own |
| Defect fixes | Fixed where the defect exists | Pipeline code is never cherry-picked |
| `docs/` | Maintained on `dev` only | Maintenance branches read `dev` |
| Specification test skip ledger | Separate per branch | Never synchronized; baselines differ |

`Dialect/` can move between branches precisely because the L0 rule forbids EF Core references there.

## 4. Capability tiers

Without tiers, three branches mean three regression costs and two of them inevitably decay.

| Branch | Scope | New dialect capabilities | Defect fixes |
|---|---|---|---|
| `dev` (10) | Full. All Dameng dialect capabilities and new features. | Originated here only | Fixed |
| `efcore9` (9) | Full behavioural baseline, plus backports | Backported when a consumer needs them | Fixed |
| `efcore8` (8) | Behavioural baseline | Not backported | Fixed |

The behavioural baseline is the set of specification tests that can execute on that EF Core version. Baselines are not nested. A maintenance branch is not deficient because a newer branch has capabilities it lacks; it is complete when it supports everything its own EF Core version can express.

If a capability is reachable only through an API that exists on an older version and was removed in a newer one, the branches diverge in both directions. In that case neither branch is a subset of the other, and the difference is recorded in [Version Matrix](version-matrix.md).

## 5. Versioning

```
<EF major>.<EF minor or provider increment>.<patch>

10.0.3   third release on the EF Core 10 line
9.0.1    first release on the EF Core 9 line
```

- The minor position increments when the provider is republished for a new EF Core minor release.
- The patch position increments for provider defect fixes.
- Assembly version equals the package version with `.0` appended (`10.0.3` becomes `10.0.3.0`). The project fails the build if the two disagree.
- The first release on the `10.0.x` line is `10.0.1`. If that version already exists on the target feed, increment the patch position.
- The EF Core dependency range always carries an upper bound (D5).

## 6. Tags

1. Tag name is `v` followed by the package version (`v10.0.3`). Annotated tags only.
2. A tag is placed only on a commit that passed full acceptance: specification suite plus live-database matrix.
3. Tag name, package version and assembly version always agree. CI enforces this.
4. Published tags are never moved or deleted.

## 7. Release process

```
feature branch -> pull request (CI: build, unit tests, specification tests)
               -> dev
               -> release decision (version number, live-database matrix passed)
               -> tag vX.Y.Z
               -> pack
               -> package consumer smoke test in an isolated NuGet cache
               -> publish
```

Rules:

- The consumer smoke test restores into an isolated NuGet package cache and compares the SHA-256 of the produced `.nupkg` against the package the consumer resolved. A package cache shared with the build output can silently validate an older package.
- Full acceptance runs against a live Dameng instance before publish.
- Branches release independently and never block one another.

## 8. Adopting a new EF Core major version

The trigger is the decision to support that version, not its release date. Steps:

1. Retarget `dev` to the new EF Core major version and its required target framework.
2. Create a maintenance branch for the previous major version from its last release tag, if that line will receive new capabilities. If it will only receive defect fixes, no branch is created; use `hotfix/X.Y.Z` from the tag.
3. Update the branch table in this document and the support table in `README.md`.
4. Work through [Version Matrix](version-matrix.md) for the transition.

### EF Core 11

`Microsoft.EntityFrameworkCore` 11.x ships `net11.0` assets only and cannot run on .NET 10. Adopting it is therefore a runtime cutover: consumers on .NET 10 remain on the `10.0.x` line. EF Core 11 also changes the provider surface more than previous majors:

- type mappings become generic (`CoreTypeMapping<T>`, `RelationalTypeMapping<T>`)
- `JsonPath` becomes a structured type on column modifications
- collation names are quoted in generated SQL
- the specification test suite moves to xUnit v3

These are tracked in [Version Matrix](version-matrix.md).

## 9. CI matrix

| Branch | Build | Unit tests | Specification tests (offline) | Specification tests (live) | Pack |
|---|---|---|---|---|---|
| `dev` | every pull request | every pull request | every pull request | daily, and before release | on tag |
| `efcore9` | every pull request | every pull request | every pull request | before release | on tag |
| `efcore8` | every pull request | every pull request | every pull request | before release | on tag |

Live-database jobs use protected variables and never run on pull requests from forks. A failing live job fails the pipeline; it is never marked optional.

## 10. Commit messages

Format: `type(scope): summary`, where type is one of `feat`, `fix`, `refactor`, `test`, `docs`, `build`, `ci`, `chore`.

Scope is the layer or area touched: `query`, `storage`, `update`, `migrations`, `scaffolding`, `value-generation`, `dialect`, `docs`, `ci`.

A commit that adds a skip-ledger entry includes it in the same commit as the code, so it is visible in review.
