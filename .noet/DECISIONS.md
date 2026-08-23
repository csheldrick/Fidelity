# DECISIONS

Record a row when a project judgment should travel with future work.

An empty Status cell means the decision currently governs. When project
judgment changes, the old row is kept and its Status becomes
`superseded <date> by: <exact replacement decision text>` (written by
`noet record decision --supersede`); only the replacement renders as in
force.

| Date | Decision | Reason | Current consequence | Reversal condition | Action when reversal condition is met | Status |
| --- | --- | --- | --- | --- | --- | --- |
| 2026-08-22 | The first Fidelity slice uses a replay HttpMessageHandler plus a small semantic assertion runner included into standalone file-based apps; the demos configure Refit as the real typed-client pipeline. | The healthy, incomplete, and corrected demos all needed the same transport substitution and diagnostics, while the examples themselves define the model and client boundary. | Keep shared mechanics limited to replaying caller-supplied HTTP responses, loading fixtures, and reporting transport, typed-result, and semantic expectation outcomes; keep client interfaces and models in each example. | A later generic example demonstrates a different repeated concern that cannot be expressed by these shared mechanics without adding client-specific abstraction. | Record a superseding decision based on that example before broadening the shared surface. |  |
| 2026-08-23 | The Application Insights/Log Analytics fixture harvester (tools/HarvestApplicationInsights.cs, src/Fidelity/Harvest.cs) uses the locally authenticated Azure CLI behind a small AzCliRunner delegate seam, keeping envelope parsing and field extraction pure/testable without live Azure access, and writes the harvested response body as an ordinary Fidelity fixture with no telemetry-specific replay type. | Azure CLI avoids an Azure SDK dependency while az login handles authentication; isolating process execution behind a delegate lets zero-row, ambiguous-row, invalid-row-index, missing-field, empty-field, and malformed-envelope failures be tested deterministically; writing the extracted body directly (no reserialize) and keeping provenance in a separate sidecar keeps telemetry acquisition out of replay semantics. | Fidelity core remains usable with zero Azure dependency; harvesting is opt-in, ambiguity fails closed rather than guessing a row, and the harvested output is consumed unmodified by ReplayHttpMessageHandler/ReplayFixture. | A second telemetry backend or acquisition mechanism creates concrete pressure for a generalized source/plugin framework that the current AzCliRunner seam cannot express. | Record a superseding decision based on that concrete second example before generalizing the harvester surface. |  |
