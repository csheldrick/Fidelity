# CONTINUITY

## Current
- Status: active
- Goal: Complete and verify the Application Insights fixture harvester slice (issue #3).
- Active direction: The harvester is an optional Azure-CLI-backed adapter isolated from replay semantics via the AzCliRunner seam; do not broaden the shared replay surface or add an Azure SDK/plugin framework without concrete pressure.
- Next action: PR #4 is open at the corrected ancestry. A second controller review found no core-boundary blocker and requested four cleanups: drop the redundant `.noet/continuity.json` update (Markdown is the source of truth per OPERATING.md), assert the az CLI argument shape through the `AzCliRunner` seam for both modes, make the unwritable-output test host-independent, and reconcile the README status section. All four are addressed on this branch. Await final controller review before merge; do not merge or close #3.
- Open questions: Whether real Application Insights/Log Analytics output-shape variants beyond the captured envelope fixtures will surface later remains unresolved.
- Recorded: 2026-08-23

## History
- Initial Fidelity bootstrap and first-slice implementation were the earlier candidate state.
- The three file-based replay demos exercising the real Refit pipeline and shared transport/semantic diagnostics remain unchanged and governing; do not broaden that shared surface without another generic example creating pressure.
- The issue-#3 harvester slice was rebased onto post-squash origin/main (0860ffa) after PR #2 merged with squash, since the branch had reused the pre-squash lineage.
- `.noet/continuity.json` is left at its origin/main value going forward; per `.noet/OPERATING.md` v6, `.noet/CONTINUITY.md` is the maintained continuity surface and the JSON mirror is an inactive-by-default writer that should not be carried as a parallel edit.
