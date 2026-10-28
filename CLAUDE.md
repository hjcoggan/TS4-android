# Top Spin 4 Recomp

Goals, 1.0 scope, milestones and way of work: `docs/goals.md`. Read it before starting work.

- Never commit game data, extracted executables or generated C++ (ADR-0006).
- Commit verified steps to `main` freely; ask before any push.
- A change is done only after it passes the smoke run and has been compared with the Oracle (Xenia Edge).
- Before committing, run `pwsh scripts/build.ps1 -Action Check`.
- Smoke run or codegen fails (unregistered target, crash, stall): `docs/agents/codegen-debugging.md`.
- Native draws differently from Classic, or adding a D3D hook: `docs/agents/native-debugging.md`.

## Project agents

Delegate to the agents in `.claude/agents/` when the task fits; do small checks inline.

- `implementer`: a fully specified `ready-for-agent` ticket (settings, scripts, tests, launcher). Not for parity bugs, codegen misses, crashes, stalls or RE.
- `qa`: before ticking a ticket's boxes, resolving it, or handing a milestone to the owner. Pass the ticket path, diff range and evidence paths, not your conclusions.
- `re-investigator`: one bounded reverse-engineering question about the TU1 Modules, to keep Ghidra output out of the main session.

## Agent skills

### Issue tracker

Issues live as local markdown files under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Default vocabulary: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix` (recorded as a `Status:` line in each issue file). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
