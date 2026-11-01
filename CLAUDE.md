# Development Guide

GrocerEase is a learning project built on Clean Architecture (.NET 10, ASP.NET Core, PostgreSQL). Every step is documented in Obsidian notes, so the notes are as much a deliverable as the code.

Read these instead of asking: `README.md` (layout, running, tests, error contract) and `TODO.md` (what is done and open).

## Workflow

1. Do the task in the prompt. Add a unit test or integration test whenever the change can be tested.
2. Run only the new test while iterating: `dotnet test --filter "FullyQualifiedName~<TestName>"`. Run the full suite once before the final commit. Integration tests need Docker running.
3. Document the change in a new Obsidian note (see Notes).
4. Make small, testable commits. One behaviour per commit, so each commit passes its own tests.

## Commits

Match the existing history: a lowercase, imperative subject with no prefix, saying what the change makes true (`reject a duplicate email before hashing or inserting`). Put the reason in the body when the subject cannot carry it.

## Notes

Location: `~/Documents/Guides to Great SW/Better NCC DEV/Applications/Grocery Project`

- Notes are numbered files named `<N>. <Title>.md`. Create the next number after the highest existing one.
- Older notes were written incrementally and can contradict each other or the code. The newest note wins, so state in a new note what it supersedes and why.
- End every note with a **Missing / Can be improved** list.
- Reflect completed work in `TODO.md`, and in `README.md` when the API contract changes.
