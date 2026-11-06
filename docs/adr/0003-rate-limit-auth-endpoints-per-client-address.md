# 0003. Rate-limit the authentication endpoints per client address, in memory

- Status: Accepted
- Date: 2026-11-06 (records a decision made when rate limiting was added)

## Context

Each register or login attempt costs a BCrypt hash at cost 11, which is deliberately slow. Without a cap, the endpoints
invite password guessing and let a modest request rate exhaust the CPU.

## Decision

`/auth/register`, `/auth/login`, `/auth/refresh` and `/auth/logout` share one fixed-window budget per client address:
10 requests per 60 seconds by default (`RateLimiting:Auth:PermitLimit`, `WindowSeconds`), validated at startup. A
rejected request gets `429 RateLimit.Exceeded` with `Retry-After`, no queueing, and an audit event with the path and the
address. The limiter is ASP.NET Core's built-in one, held in the process.

The address is the socket's `RemoteIpAddress`. Requests with none (the in-memory test host) share one bucket.

## Consequences

- It is cheap and has no new infrastructure.
- **Behind a reverse proxy, every client looks like the proxy** and shares one budget until `UseForwardedHeaders` is
  configured with the proxy's address (open in `TODO.md`). Trusting `X-Forwarded-For` without that would let a client
  choose its own bucket, so it is not read.
- **The budget is per instance.** Two replicas allow twice the limit. A shared store, or enforcement at a gateway, is
  needed before scaling out (open in `TODO.md`).
- The limit is per address, not per account, so a distributed attack on one email is not slowed. Per-account lockout is
  part of the open account-lifecycle item.
- A fixed window allows a burst at the boundary (up to twice the limit across two adjacent windows). Accepted for now.
