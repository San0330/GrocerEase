# 0005. Logs, audit events and metrics never carry credentials

- Status: Accepted
- Date: 2026-11-06 (records a decision made while adding observability)

## Context

An authentication service needs good telemetry: to see who is guessing passwords, to follow one request through the
logs, and to alert on failures. The same telemetry is shipped to other systems and read by more people than the database
is, so a secret that reaches it is exposed far wider. C# records make this easy to get wrong, because a generated
`ToString()` prints every property.

## Decision

- **Never log or tag a secret.** Telemetry carries ids, outcomes and reasons. A password, a token, an `Authorization`
  header, a query string and a request body are never written.
- **Email as a fingerprint.** Where an account must be tracked, the log carries 16 hex characters of the SHA-256 of the
  lower-cased email. Repeated attempts against one account show as one value; the address cannot be read back.
- **Fixed event ids.** Authentication events 1001 to 1009 have stable ids, so a query or an alert can name one. They are
  written through source-generated `[LoggerMessage]` methods.
- **Redact in `ToString()`.** Types that carry a secret override `PrintMembers` to print `[redacted]`. A test finds, by
  reflection, any type in Application or Contracts with a password-like or token-like property that does not, so a future
  `ChangePasswordCommand` is covered without anyone remembering.
- **Route, not path.** The request log records the route pattern (`/products/{id}`), and `unmatched` for no route, so an
  id, a token or a scanner's probe does not reach the log.
- **Metrics do not split failures by reason.** `grocerease.auth.attempts` has `outcome` but no reason tag, so the metric
  cannot be used to learn which emails exist.
- **One test watches the whole flow.** An integration test runs register, login, refresh, reuse and logout and fails if a
  secret appears in anything logged.

## Consequences

- An operator can follow an incident by trace id and account fingerprint without ever holding a credential.
- A new type with a secret must follow the rule, and the reflection test will say so. A secret carried in a type the test
  does not scan (a string in a log message, a header) relies on review.
- The fingerprint is not anonymous against someone who can guess addresses and hash them. It is meant to keep the address
  out of a log, not to defeat a determined attacker who already has candidate emails.
- What the telemetry says to alert on, and who is paged, is not decided yet (open in `TODO.md`).
- CodeQL's `cs/exposure-of-sensitive-information` flags the log reason `AuthenticationLog.Reasons.UnknownEmail` because
  of its name, though its value is the literal `"unknown_email"`. Those two alerts were dismissed as false positives with
  that explanation. A real hit on this rule means a value derived from an email, a password or a token reached a log, and
  should be treated as a bug.
