#!/usr/bin/env bash
# Single source of truth for the dotnet serialization lock layout (#1566).
# Sourced by dotnet-serialized, test-memory-harness, and test-memory-profile.

export LOCK_DIR="${HOME}/.claude/locks/dotnet.lock"
export LOCK_PID_FILE="${LOCK_DIR}/pid"
export LOCK_CHILD_FILE="${LOCK_DIR}/child"
export LOCK_LOG_FILE="${LOCK_DIR}/log"
export LOCK_CWD_FILE="${LOCK_DIR}/cwd"
export LOCK_STARTED_FILE="${LOCK_DIR}/started"

#: Monotonic count of lock acquisitions, kept OUTSIDE $LOCK_DIR on purpose: cleanup() rm -rf's
#: that directory on every release, so a counter inside it resets and can never be monotonic.
export GENERATION_FILE="${HOME}/.claude/locks/dotnet.generation"
