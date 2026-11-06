# 0001. Clean Architecture, with the dependency rule enforced by tests

- Status: Accepted
- Date: 2026-11-06 (records a decision made at the start of the project)

## Context

The system will grow from authentication into products, carts, orders and payments. The first feature is too small to
show whether a layering holds, and a layering that is only a convention erodes: one controller that reads the database
directly is enough to start a pattern.

## Decision

Five source projects with one-way dependencies:

- `GrocerEase.Domain` depends on nothing.
- `GrocerEase.Application` depends only on Domain. It holds one handler per use case, commands, validators, and the
  interfaces that the outer layers implement.
- `GrocerEase.Infrastructure` implements those interfaces (EF Core and Npgsql, BCrypt, JWT).
- `GrocerEase.Contracts` holds HTTP request and response shapes and depends on nothing in the solution.
- `GrocerEase.Api` is the host: controllers, the pipeline, startup. It never touches EF Core types.

The rule is enforced, not described: `GrocerEase.UnitTests/Architecture` fails the build when Domain or Application
references a web or database framework, when Api uses the database directly, or when a controller reaches past a
handler. Handlers must be sealed and registered with their validator in front.

## Consequences

- A use case can be unit tested with no database and no running host, and Infrastructure can change without touching
  the rules.
- Adding a feature costs more files than a controller with inline queries would. That is accepted: the cost is paid
  while the project is small, and the tests fail on a shortcut instead of a review having to catch it.
- The Domain model is still thin (`User` is mostly data). Whether richer aggregates are worth it for `Order` and `Cart`
  is open and will get its own record when it is decided.
- Rejected: a single project with folders. It is quicker today, and nothing would stop a layer from reaching where it
  should not.
