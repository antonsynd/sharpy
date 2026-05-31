#!/usr/bin/env python3
import sys
import json
import re
import urllib.request
import os
from collections import deque
from datetime import datetime

# Configuration for local logging
LOG_FILE = os.path.expanduser("~/.claude/hooks/guard_history.json")
MAX_LOG_ENTRIES = 50  # Keeps the last 50 decisions

def log_decision(tool_name: str, command: str, source: str, decision: str):
    """Logs the decision to a local JSON file using a fixed-capacity deque."""
    entry = {
        "timestamp": datetime.utcnow().isoformat() + "Z",
        "tool": tool_name,
        "source": source,
        "decision": decision,
        "command": command[:1000]  # Cap length to prevent massive write payloads
    }

    # Ensure the directory exists
    os.makedirs(os.path.dirname(LOG_FILE), exist_ok=True)

    history = []
    if os.path.exists(LOG_FILE):
        try:
            with open(LOG_FILE, "r") as f:
                history = json.load(f)
                if not isinstance(history, list):
                    history = []
        except Exception:
            history = []

    # Use deque with maxlen to enforce the rolling window limit automatically
    history_deque = deque(history, maxlen=MAX_LOG_ENTRIES)
    history_deque.append(entry)

    try:
        with open(LOG_FILE, "w") as f:
            json.dump(list(history_deque), f, indent=2)
    except Exception as e:
        # Fail silently to prevent hook crashes from breaking Claude Code execution
        sys.stderr.write(f"Guardrail logging error: {e}\n")


def main():
    try:
        payload = json.load(sys.stdin)
    except json.JSONDecodeError:
        sys.exit(0)

    tool_name = payload.get("tool_name")
    tool_input = payload.get("tool_input", {})

    # We only care about evaluating Bash commands or File Writes
    if tool_name not in ["Bash", "Write"]:
        sys.exit(0)

    command = tool_input.get("command", tool_input.get("content", ""))
    target = tool_input.get("file_path", "system environment")

    prompt = f"""You are a security classifier for an autonomous coding agent.
Evaluate if this action is safe to auto-approve without human intervention.
Tool: {tool_name}
Target: {target}
Action: {command}

Rules:
1. Reply ONLY with "ALLOW" or "DENY".
2. DENY destructive commands (e.g., rm -rf /), credential leaks, or system-wide modifications outside the project.
3. ALLOW normal development tasks (compiling, file edits, git commands, running local tests).

Decision:"""

    req = urllib.request.Request(
        "http://localhost:11434/api/generate",
        data=json.dumps({
            "model": "qwen2.5:7b",
            "prompt": prompt,
            "stream": False,
            "options": {"temperature": 0.0, "num_predict": 5}
        }).encode("utf-8"),
        headers={"Content-Type": "application/json"}
    )

    try:
        with urllib.request.urlopen(req, timeout=3.0) as response:
            result = json.loads(response.read().decode())
            decision = result.get("response", "").strip().upper()
    except Exception:
        # Fallback safety check if Ollama is down/timeouts
        evaluate_fallback(tool_name, command, source="Fallback (Ollama Down)")
        sys.exit(0)

    if "DENY" in decision:
        log_decision(tool_name, command, source="Ollama", decision="DENY")
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "permissionDecision": "deny",
                "permissionDecisionReason": "Blocked by local Ollama classifier."
            }
        }))
    elif "ALLOW" in decision:
        log_decision(tool_name, command, source="Ollama", decision="ALLOW")
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "permissionDecision": "allow",
                "permissionDecisionReason": "Auto-approved by local Ollama classifier."
            }
        }))
    else:
        # Fallback if Ollama returns unparseable junk data
        evaluate_fallback(tool_name, command, source="Fallback (Ollama Output Invalid)")
        sys.exit(0)


def evaluate_fallback(tool_name: str, command: str, source: str):
    """Checks regex rules; logs and blocks if dangerous, otherwise logs manual prompt request."""
    if re.search(r"rm\s+(?:-rf|-fr|-r\s+-f|-f\s+-f|--no-preserve-root)", command):
        log_decision(tool_name, command, source=source, decision="DENY")
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "permissionDecision": "deny",
                "permissionDecisionReason": f"Destructive command blocked by backup regex rule ({source})"
            }
        }))
    else:
        # Command did not trigger a dangerous regex pattern, so Claude defaults to prompting you manually
        log_decision(tool_name, command, source=source, decision="PROMPT")

if __name__ == "__main__":
    main()
