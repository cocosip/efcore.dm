# Documentation

| Document | Purpose |
|---|---|
| [Architecture](architecture.md) | Layering, shared versus version-specific code, public surface, namespace rule, repository layout, cross-cutting rules, code conventions, decisions |
| [Branching and Release](branching-and-release.md) | Branch model, branch creation and synchronization, capability tiers, versioning, tags, release process, CI matrix, commit format |
| [Version Matrix](version-matrix.md) | What a provider must change between EF Core 8, 9, 10 and 11; volatile surface checklist; reverse adaptation procedure |
| [Testing](testing.md) | Acceptance layers, specification suite wiring, skip ledger rules, differential comparison, live database matrix, definition of done |
| [Changelog](../CHANGELOG.md) | What changed, once per release; the suites and the matrix carry the evidence |
| [Roadmap](roadmap.md) | Phases, exit criteria, ordering, status, prerequisites |
| [Capability Matrix](capability-matrix.md) | Every capability the EF Core provider contract requires, with status, evidence, phase and whether the reference implementation had it |
| [Provenance](provenance.md) | Licence scope, permissible use of reference material, third-party code register |

## Reading order

- Why the design is what it is: [Architecture](architecture.md), then [Version Matrix](version-matrix.md)
- How branches and releases work: [Branching and Release](branching-and-release.md)
- What counts as finished: [Testing](testing.md), then [Roadmap](roadmap.md)
- What is actually supported today, and what is missing: [Capability Matrix](capability-matrix.md)
- Before committing code: [Provenance](provenance.md)

## Maintenance rules

1. Documents are maintained on the version branch and reach `dev` and `master` by merge. An older version branch reads them from the newest one.
2. Decisions are updated in place with a date. No supplementary or superseding documents are created.
5. Progress is not narrated in the documents that define behaviour: a change is recorded once in the [Changelog](../CHANGELOG.md), and the matrix or the ledger gains a row only when a capability or a case changes.
3. [Version Matrix](version-matrix.md) and [Capability Matrix](capability-matrix.md) are living documents. Every newly discovered version difference or capability gap adds a row, which keeps them usable as checklists.
4. When a document and the implementation disagree, both are corrected in the same commit. Drift is not carried forward.

