# CONTINUITY

## Current

- Status: stable
- Goal: replay real API responses against typed clients and detect semantic loss.
- Active direction: no feature implementation is active. Application Insights harvesting requires an explicit caller-owned `--offset`, passed unchanged to Azure CLI and recorded in provenance only for that mode; Log Analytics remains unchanged.
- Next action: none. Reopen only when a concrete replay/harvesting need exposes a new boundary.
- Open questions: an equivalent explicit Log Analytics `--timespan` requirement remains intentionally deferred; do not introduce a generic time-range abstraction without concrete pressure.
- Recorded: 2026-10-01.

## History

- 2026-09-30: main added the Aruba Central switches replay example and related fixtures.
- 2026-08-24: issue #5 closed after the Application Insights explicit-offset fix landed.
- Issue #3 added the Application Insights harvesting slice after the initial file-based replay capability.
- `.noet/CONTINUITY.md` remains the maintained continuity surface; legacy JSON state is not a parallel authority.
