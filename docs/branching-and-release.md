# Branching and Release

Status: **decided**. Changes to this document require review.

Governing rule:

> A branch exists for as long as it still receives new capabilities. A version that only receives defect fixes is a tag plus a temporary hotfix branch, not a long-lived branch.

## 1. Branches

| Branch | Role | EF Core | Target framework | Package line | EF Core dependency |
|---|---|---|---|---|---|
| `efcore/10.0` | Development for the EF Core 10 line. Every change lands here first. | 10 | `net10.0` | `10.0.x` | `[10.0.0,11.0.0)` |
| `dev` | Integration. Carries the latest `efcore/10.0` code; a release is cut from it. | 10 | `net10.0` | `10.0.x` | `[10.0.0,11.0.0)` |
| `efcore/9.0` | Development for the EF Core 9 line, created when that line starts. | 9 | `net8.0` | `9.0.x` | `[9.0.0,10.0.0)` |
| `efcore/8.0` | Development for the EF Core 8 line, created when that line starts. | 8 | `net8.0` | `8.0.x` | `[8.0.0,9.0.0)` |
| `master` | Packaging. Receives `dev` once development for a version completes. Tags and packages are made here. | as merged | as merged | as merged | as merged |
| `hotfix/X.Y.Z` | Temporary, created from a release tag | as source tag | as source tag | as source tag | as source tag |

Development happens on the version branch, never directly on `dev` or `master`: `dev` is the integration line that always holds the newest completed version-branch code, and `master` only moves when a release is prepared. This keeps `master` readable as the packaged history, and keeps a half-finished feature out of it by construction.

Branch names carry the EF Core major and minor version (`efcore/10.0`), so a branch states which EF Core release line it serves and which package line it produces.

`efcore/9.0` and `efcore/8.0` are named here and created when that line starts, not before. An empty branch that receives nothing is a placeholder that rots, and the version it would serve is fixed by the EF Core release schedule rather than by this repository.

## 2. Creating a branch

**A version branch starts from `dev` when work on that EF Core line begins; a release tag is placed on `master`.**

```
efcore/10.0 --*--*--*--*-------------->  development continues
              \
               +--> dev -------------->  integration
                       \
                        +--> master -->  packaging
                                \
                                 v10.0.x  (tag, acceptance passed)
```

1. A version branch is created from `dev` at the moment EF Core release line starts, so it inherits every capability the previous line already has. Creating it from an older tag would drop the difference between the lines into a single step, where it could not be attributed to EF Core 9 or EF Core 10.
2. Work is committed on the version branch and merged into `dev` as it lands. `dev` never receives a commit of its own, so it is always reproducible from the version branch.
3. `master` only receives `dev`, and only when development for a version is complete. Nothing is committed to `master` directly; the packaging scripts and the tag are the only things that distinguish it from `dev` at that moment.
4. A branch is closed when its line stops receiving changes. A line that only receives defect fixes is a tag plus a temporary `hotfix/X.Y.Z` branch, not a long-lived branch.

## 3. Synchronization

| Content | Direction | Mechanism |
|---|---|---|
| `Dialect/` | Newest version branch to older ones only | `git checkout efcore/10.0 -- src/*/Dialect/` or cherry-pick |
| New dialect capabilities | Newest version branch to older ones, on demand | See capability tiers |
| L1–L6 pipeline code | Never | Each branch maintains its own |
| Defect fixes | Fixed where the defect exists | Pipeline code is never cherry-picked |
| `docs/` | Maintained on the version branch, travels to `dev` and `master` by merge | An older version branch reads `docs/` from the newest one |
| Specification test skip ledger | Separate per branch | Never synchronized; baselines differ |

`Dialect/` can move between branches precisely because the L0 rule forbids EF Core references there.

## 4. Capability tiers

Without tiers, three branches mean three regression costs and two of them inevitably decay.

| Branch | Scope | New dialect capabilities | Defect fixes |
|---|---|---|---|
| `efcore/10.0` (10) | Full. All Dameng dialect capabilities and new features. | Originated here only | Fixed |
| `efcore/9.0` (9) | Full behavioural baseline, plus backports | Backported when a consumer needs them | Fixed |
| `efcore/8.0` (8) | Behavioural baseline | Not backported | Fixed |

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
feature work on efcore/10.0 -> merge
efcore/10.0 -> merge -> dev
            -> merge -> master (development for the version complete)
                     -> tag vX.Y.Z
                     -> pack
                     -> package consumer smoke test in an isolated NuGet cache
                     -> publish
```

`pwsh scripts/package-smoke.ps1` runs the last two steps: it packs into `artifacts/packages`, restores
`tests/DM.EntityFrameworkCore.PackageConsumer` with its own `NUGET_PACKAGES` folder and a `NuGet.config`
that lists only the produced feed, compares the SHA-256 of the package the consumer resolved against
the packed one, and runs the consumer, which builds a model and a create script without connecting.

The pipeline in `.github/workflows/ci.yml` follows the same convention as the Locus repository:

- A push to `master`, a push to a version branch (`efcore/8.0`, `efcore/9.0`, `efcore/10.0`), or a
  pull request to `master` builds and tests only. Nothing runs on `dev`, which is the integration line.
- A push of a version tag (`v10.0.1` or `10.0.1`) runs the same gate, packs the provider, runs the
  package smoke test, and publishes. Only a tag publishes.
- The tag has to name the version in `Directory.Build.props` (`<DmVersion>`) and has to point at the
  tagged commit's HEAD; the pipeline fails before building when either is not true.
- NuGet publishing uses **trusted publishing**, not a stored API key: `NuGet/login@v1` exchanges the
  workflow's GitHub OIDC token for a one-hour key, which is passed to `dotnet nuget push`. The
  nuget.org trusted publishing policy must name this repository and `.github/workflows/ci.yml`, and
  the public nuget.org profile name is read from the repository variable `NUGET_USER` (`cocosip`
  when it is not set). There is no `NUGET_API_KEY` secret to rotate or leak.

Rules:

- The consumer smoke test restores into an isolated NuGet package cache and compares the SHA-256 of the produced `.nupkg` against the package the consumer resolved. A package cache shared with the build output can silently validate an older package.
- Full acceptance runs against a live Dameng instance before publish.
- Branches release independently and never block one another.

## 8. Adopting a new EF Core major version

The trigger is the decision to support that version, not its release date. Steps:

1. Create `efcore/<new major>.0` from `dev` and retarget it to the new EF Core major version and its required target framework.
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
| `efcore/10.0` | every pull request | every pull request | every pull request | daily, and before release | never |
| `dev` | every merge | every merge | every merge | before release | never |
| `master` | every merge | every merge | every merge | before release | on tag |
| `efcore/9.0` | every pull request | every pull request | every pull request | before release | never |
| `efcore/8.0` | every pull request | every pull request | every pull request | before release | never |

Live-database jobs use protected variables and never run on pull requests from forks. A failing live job fails the pipeline; it is never marked optional.

`.github/workflows/ci.yml` runs the offline rows: build, unit tests, the specification project build with its skip ledger guard, and the acceptance dry run on every push and pull request, and the package smoke test on a `v*` tag. The live rows are not wired yet, because the pipeline has no instance of its own (see [Roadmap](roadmap.md) section 5); until they are, this table describes what the pipeline is meant to run and the release notes state what live verification did not cover. Two live rows are run by hand against an instance someone provides: `samples/DmProviderValidation` and `scripts/design-time-smoke.ps1`. A live job that is added but skipped is worse than an absent one, so nothing here claims to run it. The job runs on Windows, the platform every command in `README.md` was verified on; a Linux job joins with the live matrix, so an unverified native asset does not turn into a red build before anyone can read the failure.

## 10. Commit messages

Format: `type(scope): summary`, where type is one of `feat`, `fix`, `refactor`, `test`, `docs`, `build`, `ci`, `chore`.

Scope is the layer or area touched: `query`, `storage`, `update`, `migrations`, `scaffolding`, `value-generation`, `dialect`, `docs`, `ci`.

A commit that adds a skip-ledger entry includes it in the same commit as the code, so it is visible in review.

