# Security policy

## Reporting a vulnerability

Please do not open a public issue for a security problem.

Report it privately through GitHub: on the repository, open **Security → Report a vulnerability**. Include what you found, how to reproduce it, and the commit you tested. If that option is not available to you, open an issue that says only that you have a security report and asks for a private channel, without any details.

This is a learning project maintained by one person. There is no service-level promise, but a report is read, acknowledged, and fixed on `main` as soon as it can be confirmed.

## Supported versions

Only the latest commit on `main` is supported. Nothing is released in numbered versions yet.

## What is in scope

The API in this repository: authentication and session handling (`/auth/*`, `/profile`), error handling, logging, and the container image built from the `Dockerfile`.

Out of scope: findings that need a changed configuration the documentation warns against (for example running the telemetry dashboard unsecured on a public address), and denial of service by sheer traffic volume.

## How the code treats secrets

These are design rules a report can be measured against; they are explained in [`docs/adr`](docs/adr):

- Passwords are hashed with BCrypt and never logged or returned. A command or response that carries one prints `[redacted]` from `ToString()`.
- Refresh tokens are 256 random bits; only a SHA-256 hash is stored. A refresh token that is presented twice revokes the whole login.
- Logs, audit events and metrics carry ids, outcomes and reasons, never a password, a token or an email address.
- The JWT signing secret is never committed. The host refuses to start without it.
