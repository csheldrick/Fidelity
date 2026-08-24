# CONTINUITY

## Current
- Status: active
- Goal: Implement and verify the required `--offset` fix for Application Insights harvesting (issue #5).
- Active direction: Application Insights harvesting now requires an explicit `--offset`, passed unchanged to `az monitor app-insights query --offset`, and fails before invoking `az` if omitted; Log Analytics argument generation is unchanged. The offset is caller-owned (never parsed/inferred from KQL) and recorded in provenance only for Application Insights.
- Next action: Open a PR against main referencing/closing issue #5, tagging `@project-relay` for the PR-event continuation/review workflow; do not merge.
- Open questions: Whether Log Analytics should later get an equivalent explicit `--timespan` requirement remains open per issue #5's stated scope boundary (do not broaden into a generic time-range abstraction without concrete pressure).
- Recorded: 2026-08-24

## History
- Initial Fidelity bootstrap and first-slice implementation were the earlier candidate state.
- The three file-based replay demos exercising the real Refit pipeline and shared transport/semantic diagnostics remain unchanged and governing; do not broaden that shared surface without another generic example creating pressure.
- The issue-#3 harvester slice was rebased onto post-squash origin/main (0860ffa) after PR #2 merged with squash, since the branch had reused the pre-squash lineage.
- Issue #3's harvester slice (PR #4) merged with `--mode application-insights` invoking `az monitor app-insights query` without a query time range, silently inheriting Azure CLI's 1-hour `--offset` default; issue #5 tracked and this branch fixed that gap.
- `.noet/continuity.json` is left at its origin/main value going forward; per `.noet/OPERATING.md` v6, `.noet/CONTINUITY.md` is the maintained continuity surface and the JSON mirror is an inactive-by-default writer that should not be carried as a parallel edit.
