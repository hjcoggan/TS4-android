# Top Spin 4 Recomp

Goals, 1.0 scope, milestones and way of work: `docs/goals.md`. Read it before starting work.

- Never commit game data, extracted executables or generated C++ (ADR-0006).
- Commit verified steps to `main` freely; ask before any push.
- A change is done only after it passes the smoke run and has been compared with the Oracle (Xenia Edge).

## Agent skills

### Issue tracker

Issues live as local markdown files under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Default vocabulary: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix` (recorded as a `Status:` line in each issue file). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
