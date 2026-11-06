# Contributing

GrocerEase is a learning project built on Clean Architecture, and changes are made in small steps that can each be understood and tested on their own. [`README.md`](README.md) covers the layout, how to run it, and the error contract. [`TODO.md`](TODO.md) lists what is done and open. Design decisions are in [`docs/adr`](docs/adr).

## Set up

You need the .NET 10 SDK (see `global.json`) and Docker. The integration tests start a Postgres container, so Docker must be running.

```bash
dotnet restore
dotnet build
dotnet test                                                    # unit and integration tests
dotnet test --filter "FullyQualifiedName!~IntegrationTests"    # unit tests only
```

## Working on a change

1. Add a unit test or an integration test whenever the change can be tested. While iterating, run only that test: `dotnet test --filter "FullyQualifiedName~<TestName>"`.
2. Run the whole suite once before you finish.
3. Keep commits small: **one behaviour per commit**, each passing its own tests.

## Commit messages

A lowercase, imperative subject with no prefix, saying what the change makes true:

```
reject a duplicate email before hashing or inserting
```

Put the reason in the body when the subject cannot carry it.

## What the build enforces

- **Analyzers.** The recommended .NET analyzers run on every build and a warning fails it. A rule that does not fit is switched off in `.editorconfig` with a reason beside it, not suppressed at the call site without explanation.
- **Architecture tests.** `GrocerEase.UnitTests/Architecture` checks the layer rule (Domain depends on nothing, Application only on Domain, Api never touches EF Core) and a few conventions. If one fails, a layer reached somewhere it should not; read its message before changing the test. See ADR 0001.
- **Lock files.** Every project has a `packages.lock.json`, and CI restores in locked mode. After you change `Directory.Packages.props` or a `PackageReference`, run `dotnet restore` and commit the changed lock files.
- **Coverage.** CI fails when merged line coverage drops below 80 percent.
- **Vulnerable packages.** CI fails when a NuGet package has a known advisory.
- **CodeQL.** Security queries run on every pull request. A hit that is a false positive is dismissed on GitHub with the reason written on the alert, and noted in the relevant ADR.
- **Image scan.** A pull request that touches code or the `Dockerfile` builds the image and scans it with Trivy. A waiver, with a reason and an end date, goes in `.trivyignore.yaml`.

## Secrets

Never commit a password, a token, or a signing key, and never write one to a log. Types that carry a secret override `PrintMembers` so `ToString()` prints `[redacted]`; a test finds any new type with a password-like or token-like property that does not. See [`SECURITY.md`](SECURITY.md).

## Decisions

If a change picks between options with real trade-offs, write a short ADR in `docs/adr` (copy `0000-template.md`) and link it from the pull request. An ADR that is replaced is marked superseded, not deleted.

## License

By contributing you agree that your work is released under the [MIT License](LICENSE).
