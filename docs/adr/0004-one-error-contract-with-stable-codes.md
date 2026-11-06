# 0004. One error contract: `application/problem+json` with a stable `code`

- Status: Accepted
- Date: 2026-11-06 (records a decision made while building error handling)

## Context

Errors originate in four places: the application (typed exceptions), validation, ASP.NET Core itself (a missing route, a
wrong method, a body that cannot be bound, a bare `401`), and Kestrel (an oversized or slow body). Left alone these
answer in four shapes, and a frontend would need a parser for each. A message string is a poor thing to branch on,
because wording is changed for clarity.

## Decision

Every error is `application/problem+json` (RFC 9457) with `code` and `traceId` as extension members. Clients branch on
`code` (`User.DuplicateEmail`, `Validation.Failed`, `RateLimit.Exceeded`), never on `title`. One `ApiProblem` writes
every error, whichever layer produced it, and a status-code handler gives framework responses the same body.

Application errors implement `IAppException` (an `ErrorKind`, a message, a code and an optional title). The kind says
what went wrong without naming HTTP, and the host maps it to a status, so the Domain stays free of the web and the
middleware handles a new typed exception without a new branch. Validation failures are `422` with `errors` keyed by the JSON field name; a body that
cannot be read is `400` with the key the binder reports. The full table is in the README.

Expected failures log at `Information` without a stack trace; only unexpected exceptions log at `Error`. A client
disconnect returns `499` quietly.

## Consequences

- A client needs one error parser, and a new endpoint inherits the contract by throwing a typed exception.
- The codes and statuses are an API contract: renaming one is a breaking change. The README table is the reference and
  tests pin it.
- `400` for a missing property and `422` for a present but empty one follows how ASP.NET Core model binding treats
  non-nullable properties, not a rule in the HTTP standards. Many APIs use one status for both.
- `traceId` is the full W3C string (`00-<32 hex>-<16 hex>-01`) while logs carry the 32-hex part. Searching the middle
  part finds both. Making them identical would change the contract (open in `TODO.md`).
- Error keys are camel-cased by a fixed rule in `ErrorKeys`, not read from the JSON options (open in `TODO.md`).
