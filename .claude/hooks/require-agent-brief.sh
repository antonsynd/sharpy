#!/bin/bash
# PreToolUse(Agent|Task) hook: every agent brief carries the shared-tree block of
# docs/design/verification-contract.md §9, which says to paste it verbatim into every agent
# prompt that touches the repository. Denies (exit 2) a spawn whose tool_input.prompt lacks
# either KEY sentence below, compared after dropping each line's leading whitespace and `>`
# quote markers and collapsing whitespace (so a quoted, indented or re-wrapped copy passes; a
# paraphrase does not). The denial message carries the block, read from the contract.
#
# Exempt subagent_type values (pinned by .claude/hooks/test-hooks.sh):
#   fork               inherits the parent conversation, the block included
#   web-fetch, claude-code-guide, statusline-setup   cannot touch the repository
# An absent subagent_type is general-purpose and is not exempt.
#
# Cannot see: a message sent to a running agent (SendMessage), a resumed agent, an agent started
# outside Claude Code's agent tool (create_session, a scheduled task), and whether the agent
# obeys the block. A prompt that is not a string, and unreadable input, are allowed.
#
# Hook input is JSON on stdin (tool_input.prompt, tool_input.subagent_type).
input=$(cat)
read -r -d '' PROG <<'PY'
import json, os, re, sys

KEYS = (
    'REPORT `git status`; do not "make it clean".',
    "Restore a mutation-test from the copy you made (`cp`), never from git.",
)
EXEMPT = {"fork", "web-fetch", "claude-code-guide", "statusline-setup"}

def norm(text):
    lines = (re.sub(r"^[\s>]*", "", line) for line in text.split("\n"))
    return re.sub(r"\s+", " ", " ".join(lines)).strip()

try:
    tool_input = json.loads(os.environ["HOOK_INPUT"]).get("tool_input", {})
    prompt = tool_input.get("prompt")
    kind = tool_input.get("subagent_type") or "general-purpose"
except Exception:
    sys.exit(0)
if not isinstance(prompt, str) or kind in EXEMPT:
    sys.exit(0)
brief = norm(prompt)
missing = [k for k in KEYS if norm(k) not in brief]
if not missing:
    sys.exit(0)

block = ""
try:
    contract = open(os.path.join(os.environ["HOOKS_DIR"], "..", "..", "docs", "design",
                                 "verification-contract.md"), encoding="utf-8").read()
    m = re.search(r"^## 9\.[^\n]*\n(?:(?!> )[^\n]*\n)*?((?:> [^\n]*\n)+)", contract, re.M)
    block = m.group(1) if m else ""
except OSError:
    pass
sys.stderr.write(
    "BLOCKED by .claude/hooks/require-agent-brief.sh: this " + kind + " agent's prompt lacks the "
    "shared-tree block (docs/design/verification-contract.md §9), missing: " + " / ".join(missing)
    + "\nPaste the block verbatim into the prompt and spawn again:\n\n" + block)
sys.exit(2)
PY
HOOK_INPUT="$input" HOOKS_DIR="$(cd "$(dirname "$0")" && pwd)" exec python3 -c "$PROG"
