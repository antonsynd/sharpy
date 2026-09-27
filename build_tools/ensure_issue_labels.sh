#!/usr/bin/env bash
# Create (idempotently) the lane / reach / tracker labels the round skills apply at filing
# (docs/design/verification-contract.md §10). Safe to re-run: --force updates color/description.
# Needs gh authenticated; run outside the sandbox (TLS).
set -euo pipefail
REPO="${REPO:-antonsynd/sharpy}"
while IFS='|' read -r name color desc; do
  [ -z "$name" ] && continue
  gh label create "$name" --repo "$REPO" --color "$color" --description "$desc" --force >/dev/null && echo "ok  $name"
done <<'EOF'
lane:silent-wrong|B60205|Compiles and runs with wrong output or semantics (rank first)
lane:ice|D93F0B|Internal compiler error: SPY0908/SPY0909, a CSxxxx leak, a crash
lane:false-refusal|FBCA04|A valid program (spec or python) is refused
lane:missing-refusal|E4E669|An invalid program is accepted
lane:runtime-throw|F9D0C4|Throws at runtime where python does not, or the wrong exception type
lane:message|C5DEF5|Only the wording, location, steer or code of a diagnostic or exception
lane:tooling|BFD4F2|Docs, formatter, LSP, build tools, tests, CI
reach:program|0E8A16|An ordinary program a Python programmer would write hits it
reach:probe|C2E0C6|Needs an escaped spelling, a wrong-annotation probe or a python3 message diff
class-tracker|5319E7|The one issue per class: carries the contract and the cell table
EOF
