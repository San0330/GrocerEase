# Architecture decision records

A short record of each decision that had real trade-offs: what forced it, what was chosen, and what it costs. They are
written so a new reader can tell why the code is shaped as it is, and whether a reason still holds. Copy
[`0000-template.md`](0000-template.md) for a new one. A decision that is replaced is marked *Superseded*, not deleted.

| # | Decision | Status |
|---|---|---|
| [0001](0001-clean-architecture-enforced-by-tests.md) | Clean Architecture, with the dependency rule enforced by tests | Accepted |
| [0002](0002-rotating-refresh-tokens-with-reuse-detection.md) | Refresh tokens rotate, are stored hashed, and a reused one revokes the login | Accepted |
| [0003](0003-rate-limit-auth-endpoints-per-client-address.md) | Rate-limit the authentication endpoints per client address, in memory | Accepted |
| [0004](0004-one-error-contract-with-stable-codes.md) | One error contract: `application/problem+json` with a stable `code` | Accepted |
| [0005](0005-telemetry-never-carries-credentials.md) | Logs, audit events and metrics never carry credentials | Accepted |
| [0006](0006-migrate-on-startup-is-opt-in.md) | Applying migrations at startup is opt-in | Accepted |
