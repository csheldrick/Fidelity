# Noet dogfood observations

This repository was initialized with the current local Noet checkout before implementation work began.

- Useful: `noet init` printed the resolved target and installed the protocol surfaces in one operation. `noet verify run` provides a compact way to execute the declared repository check and verify declared artifact existence.
- Awkward: `noet init --help` performed initialization instead of showing help. The current CLI's top-level `noet --help` is the safe help surface; the implementation/docs should be inspected before probing subcommand help.
- Awkward: initializing an existing protocol set with `--name` and `--goal` preserved the seeded `PROJECT.md` text while carrying the goal into continuity state. The project mission was therefore reconciled directly in `.noet/PROJECT.md` rather than relying on a misleadingly successful metadata update.
- Useful: the protocol makes the distinction between durable judgment (`DECISIONS.md`) and provisional working state (`CONTINUITY.md`) explicit. This slice records only the decision earned by the examples and keeps the remaining design questions open.
