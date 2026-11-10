# Cocosip.DM.EntityFrameworkCore

Entity Framework Core provider for Dameng (DM). Original implementation, MIT licensed.

Status: implementation in progress. Work proceeds per the [Roadmap](docs/roadmap.md) and what is implemented is recorded in the [Capability Matrix](docs/capability-matrix.md).

## Supported versions

| Branch | EF Core | Target framework | Package line | Role |
|---|---|---|---|---|
| `dev` | 10 | `net10.0` | `10.0.x` | Primary development |
| `efcore9` | 9 | `net8.0` | `9.0.x` | Maintenance |
| `efcore8` | 8 | `net8.0` | `8.0.x` | Maintenance |

EF Core 8 and 9 reached end of official support on 2026-11-10. Their branches serve existing deployments: they receive defect fixes and capability backports on demand, and do not track new features.

## Usage

```csharp
services.AddDbContext<MyContext>(options =>
    options.UseDm(connectionString));
```

The root namespace `Microsoft.EntityFrameworkCore.Dm` and the `UseDm()` entry point are unchanged from the previous provider, so migrating requires updating one `PackageReference`. No application code changes.

The assembly is `DM.EntityFrameworkCore`, the same identity the EF Core providers use, and the package id is `Cocosip.DM.EntityFrameworkCore`: the publisher prefix plus that identity. The package is not binary compatible with the previous provider, so consumers recompile, which a package change requires anyway. Consumers using a compiled model (`dotnet ef dbcontext optimize`) regenerate it.

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
- [Roadmap](docs/roadmap.md)
- [Provenance](docs/provenance.md)

## Licence

[MIT](LICENSE). MIT covers original code in this repository only. Third-party fragments are registered separately in [Provenance](docs/provenance.md).
