# VERIFICATION

Declared checks an agent must run before claiming completion.
`noet verify run` executes the declared command and checks the declared artifacts.
It does not write verification evidence or prove a later commit.

## Commands
- `pwsh -NoProfile -File scripts/verify.ps1` — timeout: 300s

Append ` — timeout: 300s` when a command legitimately needs more than
Noet's 120-second default. Example: `- \`npm run e2e\` — timeout: 300s`

## Artifacts
- `src/Fidelity/Replay.cs`
- `examples/HealthyReplay.cs`
- `examples/LossyModelReplay.cs`
- `examples/CorrectedModelReplay.cs`
- `fixtures/healthy.json`
- `fixtures/application-error.json`
- `scripts/verify.ps1`
- `.noet/OPERATING.md`
- `.noet/PROJECT.md`
- `.noet/CONTINUITY.md`
- `.noet/continuity.json` — adopted working continuity
