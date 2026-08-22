# FAILURES

Record prior failures that constrain future work. Keep the description concrete
enough for an agent to recognize when it applies.

| Failure | Trigger | Symptoms | Mitigation | Status | Evidence |
| --- | --- | --- | --- | --- | --- |
| `noet init --help` is not side-effect free | Probing init subcommand help on a fresh repository | The repository was initialized instead of help being displayed | Use top-level `noet --help` or inspect the current CLI before probing init options | observed | 2026-08-22 local Fidelity bootstrap |
