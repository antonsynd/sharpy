#!/usr/bin/env bash
# Single source of truth for the dotnet serialization lock layout (#1566).
# Sourced by dotnet-serialized, test-memory-harness, and test-memory-profile.

#: The lock itself: an flock(2) on this file, held by the wrapper for its whole run. The kernel
#: drops it when the holder exits or dies, so it can never be left stale.
export LOCK_FILE="${HOME}/.claude/locks/dotnet.flock"
#: Diagnostics about the current (or last, if it died) holder, written only while holding
#: $LOCK_FILE. Their presence is not the lock.
export LOCK_DIR="${HOME}/.claude/locks/dotnet.lock"
export LOCK_PID_FILE="${LOCK_DIR}/pid"
export LOCK_CHILD_FILE="${LOCK_DIR}/child"
export LOCK_LOG_FILE="${LOCK_DIR}/log"
export LOCK_CWD_FILE="${LOCK_DIR}/cwd"
export LOCK_STARTED_FILE="${LOCK_DIR}/started"

#: Monotonic count of lock acquisitions, kept OUTSIDE $LOCK_DIR on purpose: each release removes
#: that directory, so a counter inside it resets and can never be monotonic.
export GENERATION_FILE="${HOME}/.claude/locks/dotnet.generation"
