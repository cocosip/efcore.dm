# Sharp.DM.EntityFrameworkCore

[![CI/CD](https://github.com/cocosip/efcore.dm/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/cocosip/efcore.dm/actions/workflows/ci.yml)

Entity Framework Core provider for Dameng (DM). Original implementation, MIT licensed.

Status: EF Core 10 release candidate. The provider is feature-complete within the server limits recorded in the [Capability Matrix](docs/capability-matrix.md); the `v10.0.1` tag is pending. Changes are recorded in the [Changelog](CHANGELOG.md).

## Supported versions

| Branch | EF Core | Target framework | Package line | Role |
|---|---|---|---|---|
| `efcore/10.0` | 10 | `net10.0` | `10.0.x` | Development for the EF Core 10 line |
| `dev` | 10 | `net10.0` | `10.0.x` | Integration; carries the latest `efcore/10.0` code |
| `master` | as merged | as merged | as merged | Packaging; receives `dev` when a version is complete |
| `efcore/9.0` | 9 | `net8.0` | `9.0.x` | Created when EF Core 9 support starts |
| `efcore/8.0` | 8 | `net8.0` | `8.0.x` | Created when EF Core 8 support starts |

Changes are committed on the version branch, merged into `dev`, and reach `master` only when development for a version is complete. EF Core 8 and 9 reached end of official support on 2026-11-10, so those lines serve existing deployments: they receive defect fixes and capability backports on demand and do not track new features.

## Usage

A context configures the provider the way it configures any other. The model is built with the ordinary EF Core API; only the parts that depend on Dameng spelling need this provider.

```csharp
public sealed class MyContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDm("Server=localhost;Port=5236;User Id=SYSDBA;Password=SYSDBA");

    public DbSet<Order> Orders => Set<Order>();
}
```

An application that builds its services up front makes the same call in the registration. There is also an overload that takes a `DbConnection` the caller owns, and each overload accepts an options action that reaches the relational settings through `DmDbContextOptionsBuilder`: command timeout, batch size, migrations assembly and the execution strategy.

```csharp
services.AddDbContext<MyContext>(options =>
    options.UseDm(connectionString));
```

Design time needs nothing extra: `dotnet ef` finds the context the way it finds any other, so a context that reads its connection string in `OnConfiguring` or from the environment is enough. The DDL a migration generates is recorded row by row in the [Capability Matrix](docs/capability-matrix.md).

The consumer-facing types keep the namespaces the previous provider declares — `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Metadata`, `.Infrastructure`, `.Migrations`, `.Diagnostics` — and the entry point is still `UseDm()`, so migrating requires updating one `PackageReference`. Application code that uses the provider through those types needs no change.

The previous provider also put its internals under `Microsoft.EntityFrameworkCore.Dm`. Those are not part of the public surface and this provider keeps its internals under `DM.EntityFrameworkCore`. The one public type a consumer calls from that tree is `DmDbFunctionsExtensions` (`EF.Functions`), which this provider ships under `Microsoft.EntityFrameworkCore.Dm.Extensions`: it carries the date difference members and `IsDate`, and the [Capability Matrix](docs/capability-matrix.md) records what each one translates to.

The assembly is `DM.EntityFrameworkCore`, the same identity the EF Core providers use, and the package id is `Sharp.DM.EntityFrameworkCore`: the publisher prefix plus that identity. The package is not binary compatible with the previous provider, so consumers recompile, which a package change requires anyway. Consumers using a compiled model (`dotnet ef dbcontext optimize`) regenerate it.

## Building and verifying

Four commands, each proving something the previous one cannot:

```pwsh
dotnet build DM.EntityFrameworkCore.slnx                 # zero warnings, zero errors
dotnet test DM.EntityFrameworkCore.slnx                  # the unit and SQL shape suite, no server needed
dotnet run --project samples/DmProviderValidation -- --dry-run   # the acceptance model, its migration and its idempotent script
pwsh -File scripts/package-smoke.ps1                     # packs, restores in an isolated cache and runs a consumer
```

Nothing above needs a Dameng instance. The live acceptance run does, and it takes the connection string from user secrets, the `ConnectionStrings__Dm` environment variable, or its first argument — in that order of specificity — so no credential has to sit in the repository:

```pwsh
dotnet user-secrets set "ConnectionStrings:Dm" "<connection string>" --project samples/DmProviderValidation
dotnet run --project samples/DmProviderValidation -- --secrets=Dm      # the secret, under a name
dotnet run --project samples/DmProviderValidation -- --secrets=DmFull  # a second instance
pwsh -File scripts/design-time-smoke.ps1 -Secrets Dm   # the `dotnet ef` tooling: migrations and scaffolding
```

It exits non-zero when any step fails and never prints the connection string. [Testing](docs/testing.md) records what a run covers, what it does not, and what stays marked as awaiting verification until an instance is reachable.

The functional probe is the second live program and is not part of CI: `dotnet run --project samples/DmProviderFeatureProbe -- "<connection string>"` exercises the EF Core feature surface — types, keys, relationships, inheritance, owned and complex types, raw SQL and the update pipeline — and reports each step separately.

## Design principles

- **Dialect separated from version.** Dameng dialect knowledge lives in an EF Core-free `Dialect/` layer that is shared across branches. Pipeline classes that inherit EF Core base types are implemented per version, with no cross-version abstraction.
- **Specification suite as the gate.** Acceptance is defined by the EF Core specification test suite. Self-authored SQL assertions never stand alone as proof.
- **Live database required.** A capability not verified against a real Dameng instance is reported as awaiting verification, not as complete.
- **Clean licensing.** Original code under MIT. No decompiled source is used. Every file's origin is traceable.

## Documentation

- [Documentation index](docs/README.md)
- [Architecture](docs/architecture.md)
- [Branching and Release](docs/branching-and-release.md)
- [Version Matrix](docs/version-matrix.md)
- [Testing](docs/testing.md)
- [Changelog](CHANGELOG.md)
- [Roadmap](docs/roadmap.md)
- [Provenance](docs/provenance.md)

## Licence

[MIT](LICENSE). MIT covers original code in this repository only. Third-party fragments are registered separately in [Provenance](docs/provenance.md).



