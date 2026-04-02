---
name: playground
description: Run the Sharpy playground (Blazor WASM) locally with hot reload
---

Run the Sharpy playground locally as a Blazor WebAssembly dev server with hot reload.

**Usage:** `/playground`

**Behavior:**
- Builds and serves `src/Sharpy.Playground` with `dotnet watch run`
- Opens at `http://localhost:5000` or `https://localhost:5001`
- Hot reloads on file changes (Playground, Compiler, Core)
- Ctrl+C to stop

**Log location:** `.claude/tmp/last-playground.log`

## Steps

1. Run `mkdir -p .claude/tmp` to ensure log directory exists
2. Clear the old log with `rm -f .claude/tmp/last-playground.log`
3. Tell the user: "Starting playground at http://localhost:5000 (https://localhost:5001). Press Ctrl+C to stop."
4. Run: `dotnet watch run --project src/Sharpy.Playground > .claude/tmp/last-playground.log 2>&1`
   - **Important:** This is a long-running process. Run it in the background with `run_in_background: true` so the user can continue working.
5. After launching, tell the user the playground is running and they can open `http://localhost:5000` in their browser.
6. If the process fails immediately (exit code non-zero within a few seconds): Print "=== PLAYGROUND FAILED ===" then `tail -50 .claude/tmp/last-playground.log`, then echo "=== Full log: .claude/tmp/last-playground.log ==="
