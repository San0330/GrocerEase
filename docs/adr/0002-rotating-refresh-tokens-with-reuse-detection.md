# 0002. Refresh tokens rotate, are stored hashed, and a reused one revokes the login

- Status: Accepted
- Date: 2026-11-06 (records a decision made when refresh tokens were added)

## Context

A JWT access token lasts 60 minutes and cannot be revoked, so a session needs a second, longer-lived credential. That
credential is the most valuable thing a thief can copy: it lasts 30 days and mints new access tokens. The API cannot see
whether a request comes from the owner or from someone holding a copy.

## Decision

- **256 random bits, hash only.** The token is random, not a signed structure. Only its SHA-256 hash is stored
  (`RefreshTokens`, unique on the hash), so a leaked table cannot be replayed. A fast hash is enough because the token
  is high-entropy; BCrypt's cost is for guessable passwords.
- **Rotation.** Every successful `POST /auth/refresh` revokes the presented token and returns a new one. Tokens from one
  login share a `FamilyId`.
- **Reuse detection.** Presenting a token that is already revoked means a copy exists. The owner and the holder cannot be
  told apart, so every token in the family is revoked and the next call gets `401 Auth.InvalidRefreshToken`. That
  revocation runs with `CancellationToken.None`, so a client that disconnects cannot stop it.
- **One message for every failure.** An unknown, expired or reused token gets the same `401`, so the response says
  nothing about which it was. The log records the reason.
- **No double spend.** `xmin` is the row version, so two simultaneous refreshes with the same token cannot both win: the
  second gets `409 Database.ConcurrentUpdate`.
- **Sessions are independent.** Each login is its own family, and `POST /auth/logout` ends one family only.

## Consequences

- A stolen refresh token is useful until the owner next refreshes, then both sides are signed out. The owner has to log
  in again, which is the intended failure.
- A client that retries a refresh after a network error can present a token the server already replaced, and loses the
  session. Clients must make one refresh call at a time and keep the newest token.
- Access tokens still cannot be revoked; logout ends the refresh family but a JWT stays valid until it expires. Shortening
  the lifetime or adding a deny list is open in `TODO.md`.
- Rows are never deleted, and a session can be extended forever by using it. Both are open in `TODO.md`.
- Rejected: a signed (JWT) refresh token. It needs no table, but cannot be revoked or checked for reuse without one.
