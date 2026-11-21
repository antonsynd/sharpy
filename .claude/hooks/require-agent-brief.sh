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
# obeys the block.
#
# FAILS CLOSED: unreadable input (not JSON, a prompt that is not a string), an internal error,
# and a python3 that fails to start or dies are denied. The input is read from stdin by python
# (no size limit).
#
# Hook input is JSON on stdin (tool_input.prompt, tool_input.subagent_type).
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

def deny(msg):
    sys.stderr.write("BLOCKED by .claude/hooks/require-agent-brief.sh: " + msg)
    sys.exit(2)

try:
    tool_input = json.loads(sys.stdin.buffer.read())["tool_input"]
    kind = tool_input.get("subagent_type") or "general-purpose"
    if kind in EXEMPT:
        sys.exit(0)
    prompt = tool_input["prompt"]
    assert isinstance(prompt, str)
    missing = [k for k in KEYS if norm(k) not in norm(prompt)]
except Exception as e:
    deny("cannot read the hook input (%s: %s); the hook fails closed.\n" % (type(e).__name__, e))
if not missing:
    sys.exit(0)

block = ""
try:
    contract = open(os.path.join(os.environ["HOOKS_DIR"], "..", "..", "docs", "design",
                                 "verification-contract.md"), encoding="utf-8").read()
    m = re.search(r"^## 9\.[^\n]*\n(?:(?!> )[^\n]*\n)*?((?:> [^\n]*\n)+)", contract, re.M)
    block = m.group(1) if m else ""
except (OSError, KeyError):
    pass
deny("this " + kind + " agent's prompt lacks the shared-tree block "
     "(docs/design/verification-contract.md §9), missing: " + " / ".join(missing)
     + "\nPaste the block verbatim into the prompt and spawn again:\n\n" + block)
PY
HOOKS_DIR="$(cd "$(dirname "$0")" && pwd)" python3 -c "$PROG"
rc=$?
case $rc in
  0 | 2) exit "$rc" ;;
esac
echo "BLOCKED by .claude/hooks/require-agent-brief.sh: the hook could not judge this spawn" \
  "(python3 exit $rc); it fails closed." >&2
exit 2
