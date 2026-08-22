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
