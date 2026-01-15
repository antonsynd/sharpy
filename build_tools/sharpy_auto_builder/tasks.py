"""
Task execution functions with LangGraph @task decorator for idempotent execution.

This module provides task-wrapped functions for CLI execution and test running.
The @task decorator ensures idempotent execution during graph replays and resumption.
"""

import asyncio
import hashlib
import json
import shlex
import sys
import time
from dataclasses import dataclass, field, asdict
from datetime import datetime
from pathlib import Path
from typing import Optional, Any, Dict, List

# Default heartbeat interval for long-running operations
DEFAULT_HEARTBEAT_INTERVAL = 60.0  # Log heartbeat every 60 seconds


async def _communicate_with_heartbeat(
    process: asyncio.subprocess.Process,
    input_data: Optional[bytes],
    start_time: float,
    heartbeat_interval: float = DEFAULT_HEARTBEAT_INTERVAL,
    label: str = "Process",
) -> tuple[bytes, bytes]:
    """
    Communicate with a subprocess while logging periodic heartbeats.

    This helps track long-running operations and provides visibility
    into whether the process is still active.

    Args:
        process: The subprocess to communicate with
        input_data: Data to send to stdin (or None)
        start_time: When the operation started (for elapsed time calculation)
        heartbeat_interval: Seconds between heartbeat messages
        label: Label to use in heartbeat messages (e.g., "Agent", "Tests")

    Returns:
        tuple[bytes, bytes]: (stdout, stderr) from the process
    """

    async def heartbeat_logger():
        """Log periodic heartbeats while waiting for process."""
        while True:
            await asyncio.sleep(heartbeat_interval)
            elapsed = time.time() - start_time
            print(
                f"[heartbeat] {label} still running... ({elapsed:.0f}s elapsed)",
                file=sys.stderr,
            )

    heartbeat_task = asyncio.create_task(heartbeat_logger())
    try:
        if input_data is not None:
            stdout, stderr = await process.communicate(input=input_data)
        else:
            stdout, stderr = await process.communicate()
        return stdout, stderr
    finally:
        heartbeat_task.cancel()
        try:
            await heartbeat_task
        except asyncio.CancelledError:
            pass


try:
    from langgraph.func import task
except ImportError:
    # Fallback if langgraph not available
    def task(func):
        """Fallback decorator if langgraph.func.task is not available."""
        return func


def _compute_input_hash(*args, **kwargs) -> str:
    """
    Compute a stable hash of function inputs for cache key generation.

    Args:
        *args: Positional arguments to hash
        **kwargs: Keyword arguments to hash

    Returns:
        str: SHA256 hex digest of the inputs
    """
    # Create a stable representation of inputs
    input_data = {
        "args": [str(arg) for arg in args],
        "kwargs": {k: str(v) for k, v in sorted(kwargs.items())},
    }

    # Convert to JSON string (sorted keys for stability)
    input_str = json.dumps(input_data, sort_keys=True)

    # Compute hash
    return hashlib.sha256(input_str.encode()).hexdigest()


@dataclass
class TaskExecutionResult:
    """
    Result of executing a task (CLI call, test run, etc.).

    This dataclass is serializable to/from dict for state persistence.
    """

    # Execution status
    success: bool
    output: str
    error: Optional[str] = None

    # Execution context
    backend: str = "unknown"
    model: Optional[str] = None
    duration_seconds: float = 0.0
    exit_code: int = 0

    # Metadata
    timestamp: str = field(default_factory=lambda: datetime.now().isoformat())
    input_hash: str = ""

    # Additional data
    metadata: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        """
        Convert to dictionary for JSON serialization.

        Returns:
            dict: Dictionary representation of the result
        """
        return asdict(self)

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "TaskExecutionResult":
        """
        Create instance from dictionary.

        Args:
            data: Dictionary representation

        Returns:
            TaskExecutionResult: New instance
        """
        # Extract known fields
        known_fields = {
            "success",
            "output",
            "error",
            "backend",
            "model",
            "duration_seconds",
            "exit_code",
            "timestamp",
            "input_hash",
            "metadata",
        }

        # Filter to only include known fields
        filtered_data = {k: v for k, v in data.items() if k in known_fields}

        return cls(**filtered_data)


# ============================================================================
# Task-Wrapped CLI Execution Functions
# ============================================================================


@task
async def execute_claude_cli(
    prompt: str,
    tools: Optional[List[str]] = None,
    model: Optional[str] = None,
    timeout: float = 600.0,
    working_dir: Optional[Path] = None,
    task_id: str = "unknown",
    attempt: int = 1,
    use_fallback_idempotency: bool = True,
) -> TaskExecutionResult:
    """
    Execute Claude Code CLI with idempotent task semantics.

    The @task decorator ensures this function is idempotent - if the graph
    replays or resumes, the same inputs will return cached results instead
    of re-executing the CLI command.

    Args:
        prompt: The prompt to send to Claude Code
        tools: List of allowed tools (default: ["Read", "Write", "Edit", "Bash"])
        model: Model to use (optional, uses CLI default if not specified)
        timeout: Maximum execution time in seconds
        working_dir: Working directory for the command
        task_id: Task identifier for logging/tracking
        attempt: Attempt number (for cache differentiation on retries)
        use_fallback_idempotency: Use file-based cache if @task caching fails

    Returns:
        TaskExecutionResult: Execution result with success status and output
    """
    start_time = time.time()

    # Compute input hash for cache key and fallback idempotency
    input_hash = _compute_input_hash(
        prompt, tools, model, working_dir, task_id, attempt
    )

    # Default tools if not specified
    if tools is None:
        tools = ["Read", "Write", "Edit", "Bash"]

    # Build Claude Code command
    # --print (-p): Non-interactive output mode
    # --allowedTools: Restrict to specific tools for safety
    cmd = ["claude", "--print", "--allowedTools", ",".join(tools)]

    # Add model if specified
    if model:
        cmd.extend(["--model", model])

    try:
        # Execute Claude Code with prompt via stdin
        process = await asyncio.create_subprocess_exec(
            *cmd,
            stdin=asyncio.subprocess.PIPE,
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,
            cwd=working_dir or Path.cwd(),
        )

        # Execute with timeout and heartbeat logging
        try:
            stdout, stderr = await asyncio.wait_for(
                _communicate_with_heartbeat(
                    process,
                    prompt.encode(),
                    start_time,
                    label="Claude Code agent",
                ),
                timeout=timeout,
            )
        except asyncio.TimeoutError:
            # Kill the process on timeout
            try:
                process.kill()
                await process.wait()
            except Exception:
                pass  # Process may have already exited

            duration = time.time() - start_time
            return TaskExecutionResult(
                success=False,
                output="",
                error=f"CLI execution timed out after {duration:.1f}s (limit: {timeout:.0f}s)",
                backend="claude-code",
                model=model or "default",
                duration_seconds=duration,
                exit_code=-1,
                input_hash=input_hash,
                metadata={
                    "task_id": task_id,
                    "attempt": attempt,
                    "timed_out": True,
                },
            )

        duration = time.time() - start_time
        stdout_text = stdout.decode()
        stderr_text = stderr.decode()

        # Check for rate limiting
        rate_limited = _is_rate_limited(stdout_text, stderr_text)

        # Determine success
        success = process.returncode == 0 and not rate_limited

        return TaskExecutionResult(
            success=success,
            output=stdout_text,
            error=stderr_text if not success else None,
            backend="claude-code",
            model=model or "default",
            duration_seconds=duration,
            exit_code=process.returncode or 0,
            input_hash=input_hash,
            metadata={
                "task_id": task_id,
                "attempt": attempt,
                "rate_limited": rate_limited,
            },
        )

    except FileNotFoundError:
        return TaskExecutionResult(
            success=False,
            output="",
            error="Claude Code CLI not found. Ensure 'claude' is installed and in PATH.",
            backend="claude-code",
            model=model or "default",
            duration_seconds=time.time() - start_time,
            exit_code=-1,
            input_hash=input_hash,
            metadata={
                "task_id": task_id,
                "attempt": attempt,
            },
        )
    except Exception as e:
        return TaskExecutionResult(
            success=False,
            output="",
            error=str(e),
            backend="claude-code",
            model=model or "default",
            duration_seconds=time.time() - start_time,
            exit_code=-1,
            input_hash=input_hash,
            metadata={
                "task_id": task_id,
                "attempt": attempt,
            },
        )


@task
async def execute_copilot_cli(
    prompt: str,
    tools: Optional[List[str]] = None,
    timeout: float = 600.0,
    working_dir: Optional[Path] = None,
    task_id: str = "unknown",
    attempt: int = 1,
    use_fallback_idempotency: bool = True,
) -> TaskExecutionResult:
    """
    Execute GitHub Copilot CLI with idempotent task semantics.

    The @task decorator ensures this function is idempotent - if the graph
    replays or resumes, the same inputs will return cached results instead
    of re-executing the CLI command.

    Note: Copilot CLI does not support model selection - it uses GitHub's
    default model.

    Args:
        prompt: The prompt to send to Copilot
        tools: List of allowed tools (default: ["read", "write", "edit", "bash"])
        timeout: Maximum execution time in seconds
        working_dir: Working directory for the command
        task_id: Task identifier for logging/tracking
        attempt: Attempt number (for cache differentiation on retries)
        use_fallback_idempotency: Use file-based cache if @task caching fails

    Returns:
        TaskExecutionResult: Execution result with success status and output
    """
    start_time = time.time()

    # Compute input hash for cache key and fallback idempotency
    input_hash = _compute_input_hash(prompt, tools, working_dir, task_id, attempt)

    # Default tools if not specified (lowercase for Copilot)
    if tools is None:
        tools = ["read", "write", "edit", "bash"]

    # Build Copilot CLI command
    # --prompt: The prompt to execute
    # --allow-tool: Explicitly allow specific tools for safety
    cmd = ["copilot", "--prompt", prompt]
    for tool in tools:
        cmd.extend(["--allow-tool", tool.lower()])

    try:
        # Execute Copilot CLI
        process = await asyncio.create_subprocess_exec(
            *cmd,
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,
            cwd=working_dir or Path.cwd(),
        )

        # Execute with timeout and heartbeat logging
        try:
            stdout, stderr = await asyncio.wait_for(
                _communicate_with_heartbeat(
                    process,
                    None,  # Copilot CLI doesn't use stdin
                    start_time,
                    label="Copilot agent",
                ),
                timeout=timeout,
            )
        except asyncio.TimeoutError:
            # Kill the process on timeout
            try:
                process.kill()
                await process.wait()
            except Exception:
                pass  # Process may have already exited

            duration = time.time() - start_time
            return TaskExecutionResult(
                success=False,
                output="",
                error=f"CLI execution timed out after {duration:.1f}s (limit: {timeout:.0f}s)",
                backend="copilot",
                model="default",
                duration_seconds=duration,
                exit_code=-1,
                input_hash=input_hash,
                metadata={
                    "task_id": task_id,
                    "attempt": attempt,
                    "timed_out": True,
                },
            )

        duration = time.time() - start_time
        stdout_text = stdout.decode()
        stderr_text = stderr.decode()

        # Check for rate limiting
        rate_limited = _is_rate_limited(stdout_text, stderr_text)

        # Copilot CLI often returns exit code 0 but with prompts/errors in output
        # Check if it actually produced useful output
        success = (
            process.returncode == 0
            and not rate_limited
            and stdout_text
            and "?" not in stdout_text[:50]
        )

        return TaskExecutionResult(
            success=success,
            output=stdout_text,
            error=stderr_text if not success else None,
            backend="copilot",
            model="default",
            duration_seconds=duration,
            exit_code=process.returncode or 0,
            input_hash=input_hash,
            metadata={
                "task_id": task_id,
                "attempt": attempt,
                "rate_limited": rate_limited,
            },
        )

    except FileNotFoundError:
        return TaskExecutionResult(
            success=False,
            output="",
            error="Copilot CLI not found. Ensure 'copilot' is installed and in PATH.",
            backend="copilot",
            model="default",
            duration_seconds=time.time() - start_time,
            exit_code=-1,
            input_hash=input_hash,
            metadata={
                "task_id": task_id,
                "attempt": attempt,
            },
        )
    except Exception as e:
        return TaskExecutionResult(
            success=False,
            output="",
            error=str(e),
            backend="copilot",
            model="default",
            duration_seconds=time.time() - start_time,
            exit_code=-1,
            input_hash=input_hash,
            metadata={
                "task_id": task_id,
                "attempt": attempt,
            },
        )


@task
async def run_tests(
    test_command: str,
    working_dir: Optional[Path] = None,
    timeout: float = 300.0,
    task_id: str = "unknown",
    attempt: int = 1,
    use_fallback_idempotency: bool = True,
) -> TaskExecutionResult:
    """
    Run test command with idempotent task semantics.

    The @task decorator ensures this function is idempotent - if the graph
    replays or resumes, the same inputs will return cached results instead
    of re-executing the test command.

    Args:
        test_command: Shell command to run tests (e.g., "pytest tests/")
        working_dir: Working directory for the command
        timeout: Maximum execution time in seconds
        task_id: Task identifier for logging/tracking
        attempt: Attempt number (for cache differentiation on retries)
        use_fallback_idempotency: Use file-based cache if @task caching fails

    Returns:
        TaskExecutionResult: Execution result with success status and output
    """
    start_time = time.time()

    # Compute input hash for cache key and fallback idempotency
    input_hash = _compute_input_hash(test_command, working_dir, task_id, attempt)

    # Split command using shlex for proper shell parsing
    try:
        cmd_parts = shlex.split(test_command)
    except ValueError as e:
        return TaskExecutionResult(
            success=False,
            output="",
            error=f"Failed to parse test command: {e}",
            backend="test-runner",
            duration_seconds=time.time() - start_time,
            exit_code=-1,
            input_hash=input_hash,
            metadata={
                "task_id": task_id,
                "attempt": attempt,
                "command": test_command,
            },
        )

    try:
        # Execute test command
        process = await asyncio.create_subprocess_exec(
            *cmd_parts,
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,
            cwd=working_dir or Path.cwd(),
        )

        # Execute with timeout and heartbeat logging
        try:
            stdout, stderr = await asyncio.wait_for(
                _communicate_with_heartbeat(
                    process,
                    None,  # Test command doesn't use stdin
                    start_time,
                    label="Tests",
                ),
                timeout=timeout,
            )
        except asyncio.TimeoutError:
            # Kill the process on timeout
            try:
                process.kill()
                await process.wait()
            except Exception:
                pass  # Process may have already exited

            duration = time.time() - start_time
            return TaskExecutionResult(
                success=False,
                output="",
                error=f"Test execution timed out after {duration:.1f}s (limit: {timeout:.0f}s)",
                backend="test-runner",
                duration_seconds=duration,
                exit_code=-1,
                input_hash=input_hash,
                metadata={
                    "task_id": task_id,
                    "attempt": attempt,
                    "timed_out": True,
                    "command": test_command,
                },
            )

        duration = time.time() - start_time
        stdout_text = stdout.decode()
        stderr_text = stderr.decode()

        # Success is determined by exit code
        # Exit code 0 = all tests passed
        # Non-zero = tests failed or error occurred
        success = process.returncode == 0

        return TaskExecutionResult(
            success=success,
            output=stdout_text,
            error=stderr_text if not success else None,
            backend="test-runner",
            duration_seconds=duration,
            exit_code=process.returncode or 0,
            input_hash=input_hash,
            metadata={
                "task_id": task_id,
                "attempt": attempt,
                "command": test_command,
            },
        )

    except FileNotFoundError:
        # Command not found (e.g., pytest not installed)
        cmd_name = cmd_parts[0] if cmd_parts else test_command
        return TaskExecutionResult(
            success=False,
            output="",
            error=f"Test command not found: {cmd_name}. Ensure it is installed and in PATH.",
            backend="test-runner",
            duration_seconds=time.time() - start_time,
            exit_code=-1,
            input_hash=input_hash,
            metadata={
                "task_id": task_id,
                "attempt": attempt,
                "command": test_command,
            },
        )
    except Exception as e:
        return TaskExecutionResult(
            success=False,
            output="",
            error=str(e),
            backend="test-runner",
            duration_seconds=time.time() - start_time,
            exit_code=-1,
            input_hash=input_hash,
            metadata={
                "task_id": task_id,
                "attempt": attempt,
                "command": test_command,
            },
        )


# ============================================================================
# Fallback Idempotency Tracker
# ============================================================================


class TaskIdempotencyFallback:
    """
    File-based fallback idempotency tracker for task execution.

    This provides a fallback mechanism when LangGraph's @task caching doesn't
    work (e.g., in certain deployment environments). It stores task results
    on disk using the input hash as a cache key.

    The fallback cache is independent of the @task decorator and works across
    process restarts.
    """

    def __init__(self, cache_dir: Optional[Path] = None):
        """
        Initialize the fallback tracker.

        Args:
            cache_dir: Directory to store cache files (default: .task_cache)
        """
        if cache_dir is None:
            cache_dir = Path.cwd() / ".task_cache"
        self.cache_dir = cache_dir
        self.cache_dir.mkdir(parents=True, exist_ok=True)

    def _marker_path(self, input_hash: str) -> Path:
        """
        Get the cache file path for a given input hash.

        Args:
            input_hash: SHA256 hash of task inputs

        Returns:
            Path: Path to the cache file
        """
        return self.cache_dir / f"{input_hash}.json"

    def get_cached(self, input_hash: str) -> Optional[TaskExecutionResult]:
        """
        Get cached result for a given input hash.

        Args:
            input_hash: SHA256 hash of task inputs

        Returns:
            TaskExecutionResult if cached, None otherwise
        """
        marker_path = self._marker_path(input_hash)
        if not marker_path.exists():
            return None

        try:
            with open(marker_path, "r") as f:
                data = json.load(f)
            return TaskExecutionResult.from_dict(data)
        except Exception:
            # If cache file is corrupted, ignore it
            return None

    def cache_result(self, input_hash: str, result: TaskExecutionResult) -> None:
        """
        Cache a task execution result.

        Args:
            input_hash: SHA256 hash of task inputs
            result: Task execution result to cache
        """
        marker_path = self._marker_path(input_hash)
        try:
            with open(marker_path, "w") as f:
                json.dump(result.to_dict(), f, indent=2)
        except Exception:
            # If caching fails, don't crash - just skip caching
            pass


# Global fallback tracker instance
_fallback_tracker: Optional[TaskIdempotencyFallback] = None


def _get_fallback_tracker() -> TaskIdempotencyFallback:
    """
    Get or create the global fallback tracker instance.

    Returns:
        TaskIdempotencyFallback: Global tracker instance
    """
    global _fallback_tracker
    if _fallback_tracker is None:
        _fallback_tracker = TaskIdempotencyFallback()
    return _fallback_tracker


# NOTE: Fallback idempotency integration
# ========================================
# The fallback tracker is available for use when @task caching doesn't work.
# To use it in orchestrator code:
#
# 1. Before calling a task function:
#    tracker = _get_fallback_tracker()
#    cached = tracker.get_cached(input_hash)
#    if cached:
#        return cached
#
# 2. After task execution:
#    tracker.cache_result(input_hash, result)
#
# The use_fallback_idempotency parameter in task functions signals that
# fallback tracking should be used by the caller (orchestrator), not within
# the task function itself (which is decorated by @task).


# ============================================================================
# Helper Functions
# ============================================================================


def _is_rate_limited(stdout: str, stderr: str) -> bool:
    """
    Check if output indicates rate limiting.

    Args:
        stdout: Standard output text
        stderr: Standard error text

    Returns:
        bool: True if rate limited
    """
    combined = (stdout + stderr).lower()
    rate_limit_indicators = [
        "rate limit",
        "rate-limit",
        "too many requests",
        "429",
        "quota exceeded",
        "overloaded_error",
    ]
    return any(indicator in combined for indicator in rate_limit_indicators)
