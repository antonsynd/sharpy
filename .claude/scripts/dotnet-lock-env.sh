#!/usr/bin/env bash
# Single source of truth for the dotnet serialization lock layout (#1566).
# Sourced by dotnet-serialized, test-memory-harness, and test-memory-profile.

export LOCK_DIR="${HOME}/.claude/locks/dotnet.lock"
export LOCK_PID_FILE="${LOCK_DIR}/pid"
export LOCK_CHILD_FILE="${LOCK_DIR}/child"
