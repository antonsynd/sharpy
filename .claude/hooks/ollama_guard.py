#!/usr/bin/env python3
import sys
import json
import re
import urllib.request

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
            "model": "qwen2.5:7b", # Change to your preferred 7B/8B model
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
        # Fallback safety check if Ollama is down
        fallback(command)
        sys.exit(0)

    if "DENY" in decision:
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "permissionDecision": "deny",
                "permissionDecisionReason": "Blocked by local Ollama classifier."
            }
        }))
    elif "ALLOW" in decision:
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "permissionDecision": "allow",
                "permissionDecisionReason": "Auto-approved by local Ollama classifier."
            }
        }))
    else:
        fallback(command)
        sys.exit(0)


def fallback(command: str):
    if re.search(r"rm\s+(?:-rf|-fr|-r\s+-f|-f\s+-f|--no-preserve-root)", command):
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "permissionDecision": "deny",
                "permissionDecisionReason": "Destructive command blocked by fallback hook (Ollama down)"
            }
        }))

if __name__ == "__main__":
    main()
