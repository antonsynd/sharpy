---
name: clean-dotnet
description: Kill zombie dotnet processes and clean build artifacts
disable-model-invocation: true
---

Kill stale/zombie dotnet processes that can cause builds and tests to hang. Also optionally cleans build artifacts.

**Usage:** `/clean-dotnet`

**Warning:** This kills ALL running dotnet processes. Only use when builds or tests are hanging.

## Steps

1. List running dotnet processes: `ps aux | grep '[d]otnet'`
2. If no processes found, print "No dotnet processes running." and stop.
3. Show the process list to the user and kill them: `pkill -f dotnet`
4. Verify cleanup: `ps aux | grep '[d]otnet'`
5. Print "=== CLEANUP COMPLETE ===" with count of processes killed.
