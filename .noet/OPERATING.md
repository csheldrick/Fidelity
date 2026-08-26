<!-- noet:operating-contract-version: 8 -->
# Noet Operating Contract

This repository uses Noet's repository-native protocol to preserve engineering
judgment across context replacement. Plain Markdown under `.noet/` is the
source of truth; Git is authority and rollback.

## Operating loop

1. **Read before choosing a direction.** Read the relevant `.noet/` Markdown
   records before planning or editing. Treat decisions, failures, and
   constraints as accumulated project judgment. Treat continuity and attention
   as provisional state that must be revalidated against current repository
   truth.
2. **Work normally on a branch.** Noet does not own Git workflow or commit
   boundaries.
3. **Record judgment in the same diff as the work that creates it.** For
   `.noet/DECISIONS.md`, prefer `noet record decision` when the CLI is
   available: ordinary create for new judgment, `--amend` for a correction
   that leaves the decision governing, and `--supersede` for a governing
   transition. Direct Markdown remains a supported fallback. Maintain
   failures, constraints, continuity, and attention directly in their protocol
   Markdown surfaces unless a separate evidence-backed decision restores a
   writer for them.
4. **Supersede, don't drift.** If current work contradicts governing judgment,
   either stay inside its bounds or make the lifecycle transition explicit in
   the same reviewed diff. Preserve history rather than silently rewriting it.
5. **Reconcile continuity before claiming completion.** If follow-up remains,
   restate the current snapshot; if nothing should carry forward, close it; if
   it is still accurate, leave it unchanged.
6. **Verify honestly.** Prefer `noet verify run` to execute the commands
   declared in `.noet/VERIFICATION.md` (it executes the declared checks; it
   does not prove them), or run them directly when the CLI is unavailable.
   Report results faithfully. CI and ordinary PR review remain completion
   authority for the pushed candidate.
7. **Merge is confirmation.** Merge confirms candidate durable judgment and
   adopts provisional working state. There is no separate Noet approval ritual.

## Optional executable helpers

- `noet record decision` is the preferred optional lifecycle-safe decision
  writer when the CLI is available. It is not a required write gate.
- `noet verify run` is the active ephemeral declared-check runner: it
  executes `.noet/VERIFICATION.md`'s declared commands and artifact checks
  and reports the results honestly. It writes no `.noet/verification`
  evidence, inspects no Git identity, and is not a required write gate;
  direct execution of the declared commands remains a supported fallback.
- `noet sync --check` is a read-only freshness check for Noet-owned/versioned
  managed instruction surfaces.
- `noet sync` explicitly refreshes only surfaces Noet can prove it owns: a
  versioned Noet operating contract and marked managed sections in host agent
  files. An existing unversioned `.noet/OPERATING.md` is treated
  conservatively as repository-owned and is preserved.
- `apply --task` is retained only as an explicit repository-local opt-in for
  the bounded Strata consumer. The former `brief`, `verify list`/
  `record`/`check`/`branch`, JSON continuity/attention writers, and
  proof/provenance stores were removed from current main; use direct Markdown
  and direct verification commands for those responsibilities.
- Synchronization never stages, commits, pushes, or writes from a read-only
  command. Include any sync-created managed-file changes in the ordinary branch
  diff and review them like any other change.

## Managed artifact ownership

- **Versioned Noet operating contract:** `.noet/OPERATING.md` is replaceable
  only when it carries a recognized Noet operating-contract version marker. An
  existing unversioned file is repository-owned and must be preserved.
- **Shared managed sections:** content between `<!-- noet:begin -->` and
  `<!-- noet:end -->` in `CLAUDE.md` and `AGENTS.md`. Sync replaces only
  those sections and preserves all surrounding user-authored bytes.
- **Durable project records:** decisions, constraints, failures, verification
  declarations, continuity, attention, and other project-authored records are
  never generic sync targets.
- **User-owned:** every other repository file.

## Attached Noet checkout

When invoking a Noet CLI from a separate checkout, pass the target repository
explicitly with `--cwd <absolute-path>` for `record decision` or `sync` and
inspect the printed target before accepting any write.
