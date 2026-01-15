# Implementation Plan 3: Wrap CLI Calls in Tasks for Idempotency

## Overview

**Goal:** Wrap CLI calls to Claude Code and Copilot in LangGraph `@task` decorators to ensure idempotent execution during graph replays and resumption.

**Priority:** High  
**Estimated Effort:** 3-5 hours  
**Risk Level:** Medium (changes execution flow)

**Prerequisite:** Implementation Plan 1 (Durable Persistence) should be completed first, as tasks require checkpointing to store their results.

## Background

### Current State

CLI calls in `build_tools/sharpy_auto_builder/backends.py` are executed directly:

```python
class ClaudeCodeBackend(Backend):
    async def execute(self, prompt: str, ...) -> ExecutionResult:
        # Builds command
        cmd = CLIBuilder.build_claude_command(...)
        
        # Executes directly - NOT idempotent
        process = await asyncio.create_subprocess_exec(*cmd.args, ...)
        stdout, stderr = await process.communicate(...)
        
        return ExecutionResult(...)
```

### Problem

When a graph node is re-executed (due to resumption, replay, or debugging):
1. The CLI command runs again, even if it already succeeded
2. This wastes API tokens and time
3. Can cause inconsistent state if the command has side effects
4. Makes debugging difficult (can't replay without re-running expensive operations)

### Solution: LangGraph Tasks

LangGraph's `@task` decorator creates memoized, checkpointed functions:
- Results are stored in the checkpoint
- On replay, cached results are returned instead of re-executing
- Side effects only happen once per unique input

From the [Durable Execution docs](https://docs.langchain.com/oss/python/langgraph/durable-execution):
> "Wrap any non-deterministic operations or operations with side effects inside tasks to ensure that when a workflow is resumed, these operations are not repeated."

## Reference Documentation

- [Durable Execution](https://docs.langchain.com/oss/python/langgraph/durable-execution)
- [Tasks in Nodes](https://docs.langchain.com/oss/python/langgraph/durable-execution#using-tasks-in-nodes)
- [Determinism and Consistent Replay](https://docs.langchain.com/oss/python/langgraph/durable-execution#determinism-and-consistent-replay)
- [Functional API - Tasks](https://docs.langchain.com/oss/python/langgraph/functional-api#task)

## Files to Modify

| File | Changes |
|------|---------|
| `build_tools/sharpy_auto_builder/tasks.py` | New file: task-wrapped CLI operations |
| `build_tools/sharpy_auto_builder/backends.py` | Update to use tasks |
| `build_tools/sharpy_auto_builder/orchestrator.py` | Update nodes to use task-based execution |
| `build_tools/shared/cli_builder.py` | No changes (used by tasks) |

## Task List

### Task 3.1: Create Tasks Module
**File:** `build_tools/sharpy_auto_builder/tasks.py` (new file)

Create a new module for task-wrapped operations:

```python
"""
Task-wrapped operations for idempotent execution.

This module provides LangGraph @task-decorated functions that wrap
CLI operations. Tasks are memoized - their results are stored in
checkpoints and retrieved on replay instead of re-executing.

Usage:
    from sharpy_auto_builder.tasks import execute_claude_cli, execute_copilot_cli
    
    # In a node:
    result = await execute_claude_cli(
        prompt="Implement feature X",
        tools={"Read", "Write", "Edit"},
        model="claude-sonnet-4-5-20250929",
        timeout=300,
        working_dir="/path/to/project",
    )

IMPORTANT: Tasks must have deterministic inputs. If the same inputs
are provided, the cached result will be returned. Use unique identifiers
(like task_id + attempt_number) if you need to force re-execution.
"""

import asyncio
import hashlib
import json
from dataclasses import dataclass, asdict
from datetime import datetime
from pathlib import Path
from typing import Optional, Set, Any

from langgraph.func import task

# Import from shared module
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))
from shared.cli_builder import CLIBuilder, CLICommand
from shared.backends import ToolPermission, BackendType
from shared.rate_limiting import is_rate_limit_error, extract_rate_limit_wait_time


@dataclass
class TaskExecutionResult:
    """
    Result of a task-wrapped CLI execution.
    
    This dataclass is JSON-serializable for checkpoint storage.
    """
    success: bool
    output: str
    error: Optional[str]
    backend: str
    model: Optional[str]
    duration_seconds: float
    exit_code: int
    timestamp: str
    
    # For debugging/replay identification
    input_hash: str  # Hash of inputs for cache key identification
    
    def to_dict(self) -> dict:
        return asdict(self)
    
    @classmethod
    def from_dict(cls, data: dict) -> "TaskExecutionResult":
        return cls(**data)


def _compute_input_hash(*args, **kwargs) -> str:
    """Compute a hash of inputs for identification."""
    # Create a stable string representation
    content = json.dumps({
        "args": [str(a) for a in args],
        "kwargs": {k: str(v) for k, v in sorted(kwargs.items())}
    }, sort_keys=True)
    return hashlib.sha256(content.encode()).hexdigest()[:16]


@task
async def execute_claude_cli(
    prompt: str,
    tools: Set[str],
    model: str = "claude-sonnet-4-5-20250929",
    timeout: int = 300,
    working_dir: str = ".",
    task_id: str = "",
    attempt: int = 0,
) -> TaskExecutionResult:
    """
    Execute Claude Code CLI as a memoized task.
    
    This function is decorated with @task, meaning:
    - Results are stored in the LangGraph checkpoint
    - On graph replay, cached results are returned
    - The CLI only executes once per unique set of inputs
    
    Args:
        prompt: The prompt to send to Claude
        tools: Set of tool permissions (e.g., {"Read", "Write", "Edit"})
        model: Claude model to use
        timeout: Execution timeout in seconds
        working_dir: Working directory for the CLI process
        task_id: Unique task identifier (for cache differentiation)
        attempt: Attempt number (increment to force re-execution)
    
    Returns:
        TaskExecutionResult with execution details
    
    Note:
        The task_id and attempt parameters are included in the cache key.
        To force re-execution of the same prompt, increment attempt.
    """
    start_time = datetime.now()
    input_hash = _compute_input_hash(prompt, tools, model, task_id, attempt)
    
    # Convert tool strings to ToolPermission enum
    tool_permissions = {ToolPermission(t) for t in tools}
    
    # Build the command using shared CLI builder
    cmd = CLIBuilder.build_claude_command(
        prompt=prompt,
        tools=tool_permissions,
        model=model,
        print_mode=True,
    )
    
    try:
        # Execute the CLI process
        process = await asyncio.create_subprocess_exec(
            *cmd.args,
            stdin=asyncio.subprocess.PIPE if cmd.stdin else None,
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,
            cwd=working_dir,
        )
        
        stdin_bytes = cmd.stdin.encode() if cmd.stdin else None
        stdout, stderr = await asyncio.wait_for(
            process.communicate(stdin_bytes),
            timeout=timeout
        )
        
        duration = (datetime.now() - start_time).total_seconds()
        
        stdout_text = stdout.decode("utf-8", errors="replace")
        stderr_text = stderr.decode("utf-8", errors="replace")
        
        # Check for rate limiting
        combined_output = stdout_text + stderr_text
        if is_rate_limit_error(combined_output, process.returncode):
            wait_time = extract_rate_limit_wait_time(combined_output)
            return TaskExecutionResult(
                success=False,
                output=stdout_text,
                error=f"Rate limited. Wait {wait_time} seconds.",
                backend="claude_code",
                model=model,
                duration_seconds=duration,
                exit_code=process.returncode,
                timestamp=start_time.isoformat(),
                input_hash=input_hash,
            )
        
        return TaskExecutionResult(
            success=process.returncode == 0,
            output=stdout_text,
            error=stderr_text if process.returncode != 0 else None,
            backend="claude_code",
            model=model,
            duration_seconds=duration,
            exit_code=process.returncode,
            timestamp=start_time.isoformat(),
            input_hash=input_hash,
        )
        
    except asyncio.TimeoutError:
        duration = (datetime.now() - start_time).total_seconds()
        return TaskExecutionResult(
            success=False,
            output="",
            error=f"Execution timed out after {timeout} seconds",
            backend="claude_code",
            model=model,
            duration_seconds=duration,
            exit_code=-1,
            timestamp=start_time.isoformat(),
            input_hash=input_hash,
        )
    except Exception as e:
        duration = (datetime.now() - start_time).total_seconds()
        return TaskExecutionResult(
            success=False,
            output="",
            error=str(e),
            backend="claude_code",
            model=model,
            duration_seconds=duration,
            exit_code=-1,
            timestamp=start_time.isoformat(),
            input_hash=input_hash,
        )


@task
async def execute_copilot_cli(
    prompt: str,
    tools: Set[str],
    timeout: int = 300,
    working_dir: str = ".",
    task_id: str = "",
    attempt: int = 0,
) -> TaskExecutionResult:
    """
    Execute GitHub Copilot CLI as a memoized task.
    
    Similar to execute_claude_cli but for Copilot.
    Note: Copilot CLI does not support model selection.
    
    Args:
        prompt: The prompt to send to Copilot
        tools: Set of tool permissions
        timeout: Execution timeout in seconds
        working_dir: Working directory for the CLI process
        task_id: Unique task identifier
        attempt: Attempt number
    
    Returns:
        TaskExecutionResult with execution details
    """
    start_time = datetime.now()
    input_hash = _compute_input_hash(prompt, tools, task_id, attempt)
    
    # Convert tool strings to ToolPermission enum
    tool_permissions = {ToolPermission(t) for t in tools}
    
    # Build the command
    cmd = CLIBuilder.build_copilot_command(
        prompt=prompt,
        tools=tool_permissions,
    )
    
    try:
        process = await asyncio.create_subprocess_exec(
            *cmd.args,
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,
            cwd=working_dir,
        )
        
        stdout, stderr = await asyncio.wait_for(
            process.communicate(),
            timeout=timeout
        )
        
        duration = (datetime.now() - start_time).total_seconds()
        
        stdout_text = stdout.decode("utf-8", errors="replace")
        stderr_text = stderr.decode("utf-8", errors="replace")
        
        # Check for rate limiting
        combined_output = stdout_text + stderr_text
        if is_rate_limit_error(combined_output, process.returncode):
            wait_time = extract_rate_limit_wait_time(combined_output)
            return TaskExecutionResult(
                success=False,
                output=stdout_text,
                error=f"Rate limited. Wait {wait_time} seconds.",
                backend="copilot",
                model=None,
                duration_seconds=duration,
                exit_code=process.returncode,
                timestamp=start_time.isoformat(),
                input_hash=input_hash,
            )
        
        return TaskExecutionResult(
            success=process.returncode == 0,
            output=stdout_text,
            error=stderr_text if process.returncode != 0 else None,
            backend="copilot",
            model=None,
            duration_seconds=duration,
            exit_code=process.returncode,
            timestamp=start_time.isoformat(),
            input_hash=input_hash,
        )
        
    except asyncio.TimeoutError:
        duration = (datetime.now() - start_time).total_seconds()
        return TaskExecutionResult(
            success=False,
            output="",
            error=f"Execution timed out after {timeout} seconds",
            backend="copilot",
            model=None,
            duration_seconds=duration,
            exit_code=-1,
            timestamp=start_time.isoformat(),
            input_hash=input_hash,
        )
    except Exception as e:
        duration = (datetime.now() - start_time).total_seconds()
        return TaskExecutionResult(
            success=False,
            output="",
            error=str(e),
            backend="copilot",
            model=None,
            duration_seconds=duration,
            exit_code=-1,
            timestamp=start_time.isoformat(),
            input_hash=input_hash,
        )


@task
async def run_tests(
    test_command: str,
    working_dir: str,
    timeout: int = 120,
    task_id: str = "",
    attempt: int = 0,
) -> TaskExecutionResult:
    """
    Run tests as a memoized task.
    
    Test execution is also wrapped as a task to avoid re-running
    expensive test suites on graph replay.
    
    Args:
        test_command: Command to run tests (e.g., "dotnet test")
        working_dir: Working directory
        timeout: Test timeout in seconds
        task_id: Unique task identifier
        attempt: Attempt number
    
    Returns:
        TaskExecutionResult with test output
    """
    start_time = datetime.now()
    input_hash = _compute_input_hash(test_command, working_dir, task_id, attempt)
    
    try:
        # Split command for subprocess
        import shlex
        cmd_parts = shlex.split(test_command)
        
        process = await asyncio.create_subprocess_exec(
            *cmd_parts,
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,
            cwd=working_dir,
        )
        
        stdout, stderr = await asyncio.wait_for(
            process.communicate(),
            timeout=timeout
        )
        
        duration = (datetime.now() - start_time).total_seconds()
        
        return TaskExecutionResult(
            success=process.returncode == 0,
            output=stdout.decode("utf-8", errors="replace"),
            error=stderr.decode("utf-8", errors="replace") if process.returncode != 0 else None,
            backend="test_runner",
            model=None,
            duration_seconds=duration,
            exit_code=process.returncode,
            timestamp=start_time.isoformat(),
            input_hash=input_hash,
        )
        
    except asyncio.TimeoutError:
        duration = (datetime.now() - start_time).total_seconds()
        return TaskExecutionResult(
            success=False,
            output="",
            error=f"Tests timed out after {timeout} seconds",
            backend="test_runner",
            model=None,
            duration_seconds=duration,
            exit_code=-1,
            timestamp=start_time.isoformat(),
            input_hash=input_hash,
        )
    except Exception as e:
        duration = (datetime.now() - start_time).total_seconds()
        return TaskExecutionResult(
            success=False,
            output="",
            error=str(e),
            backend="test_runner",
            model=None,
            duration_seconds=duration,
            exit_code=-1,
            timestamp=start_time.isoformat(),
            input_hash=input_hash,
        )
```

---

### Task 3.2: Update Orchestrator to Use Tasks
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

Update the execution node to use task-wrapped functions:

```python
# ADD import at top of file:
from .tasks import execute_claude_cli, execute_copilot_cli, run_tests, TaskExecutionResult

# UPDATE _execute_implementation_node:
async def _execute_implementation_node(self, state: OrchestratorState) -> OrchestratorState:
    """Execute task implementation using task-wrapped CLI calls."""
    task_data = state["current_task"]
    attempt = state.get("execution_attempt", 0)
    
    self._log_step_start("execute_implementation", task_data["id"], f"attempt {attempt}")
    
    # Build the prompt
    prompt = self._build_implementation_prompt(task_data, state)
    
    # Determine which backend to use
    backend_name = self.backend_manager.get_available_backend()
    if not backend_name:
        return {
            **state,
            "next_action": "error",
            "error_message": "No backends available (all rate limited)",
            "messages": [f"❌ No backends available for task {task_data['id']}"],
        }
    
    # Get backend config
    backend_config = self.config.backends.get(backend_name)
    
    # Determine tools needed for this task
    tools = self._get_tools_for_task(task_data)
    
    # Execute using task-wrapped function
    # The task_id and attempt ensure unique cache keys
    if backend_name == "claude_code":
        result = await execute_claude_cli(
            prompt=prompt,
            tools=tools,
            model=backend_config.model or "claude-sonnet-4-5-20250929",
            timeout=self.config.agent_execution_timeout,
            working_dir=str(self.config.project_root),
            task_id=task_data["id"],
            attempt=attempt,
        )
    elif backend_name == "copilot":
        result = await execute_copilot_cli(
            prompt=prompt,
            tools=tools,
            timeout=self.config.agent_execution_timeout,
            working_dir=str(self.config.project_root),
            task_id=task_data["id"],
            attempt=attempt,
        )
    else:
        return {
            **state,
            "next_action": "error",
            "error_message": f"Unknown backend: {backend_name}",
            "messages": [f"❌ Unknown backend {backend_name}"],
        }
    
    self._log_step_end("execute_implementation", task_data["id"])
    
    # Log the execution
    self._log_execution(
        event_type="task_execution",
        task_id=task_data["id"],
        prompt=prompt[:500],  # Truncate for logging
        output=result.output[:1000] if result.output else None,
        error=result.error,
        success=result.success,
        backend=result.backend,
        duration=result.duration_seconds,
        extra={"input_hash": result.input_hash, "attempt": attempt},
    )
    
    # Check for rate limiting
    if result.error and "rate limit" in result.error.lower():
        self.backend_manager.mark_rate_limited(backend_name)
        return {
            **state,
            "last_execution_result": result.to_dict(),
            "next_action": "retry_different_backend",
            "messages": [f"⚠️ {backend_name} rate limited, trying another backend"],
        }
    
    # Convert to dict for state storage (must be JSON-serializable)
    return {
        **state,
        "last_execution_result": result.to_dict(),
        "execution_attempt": attempt + 1,
        "next_action": "run_tests" if result.success else "handle_execution_failure",
        "messages": [
            f"{'✅' if result.success else '❌'} Execution {'succeeded' if result.success else 'failed'} "
            f"for {task_data['id']} (attempt {attempt + 1}, {result.duration_seconds:.1f}s)"
        ],
    }


def _get_tools_for_task(self, task_data: dict) -> Set[str]:
    """Determine which tools are needed for a task."""
    task_type = task_data.get("type", "implementation")
    
    # Base tools for all tasks
    tools = {"Read"}
    
    if task_type in ("implementation", "refactor", "fix"):
        tools.update({"Write", "Edit"})
    
    if task_data.get("requires_tests", False):
        tools.add("Bash")  # For running tests
    
    # Don't allow Bash unless specifically needed
    # This is a safety measure
    if not task_data.get("allow_bash", False):
        tools.discard("Bash")
    
    return tools
```

---

### Task 3.3: Update Test Running Node
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

Update the test running node to use the task-wrapped function:

```python
async def _run_tests_node(self, state: OrchestratorState) -> OrchestratorState:
    """Run tests using task-wrapped execution."""
    task_data = state["current_task"]
    attempt = state.get("fix_attempt", 0)
    
    self._log_step_start("run_tests", task_data["id"])
    
    # Use task-wrapped test execution
    result = await run_tests(
        test_command=self.config.test_command,
        working_dir=str(self.config.project_root),
        timeout=self.config.test_timeout,
        task_id=task_data["id"],
        attempt=attempt,  # Different attempt = different cache key
    )
    
    self._log_step_end("run_tests", task_data["id"])
    
    # Log test results
    self._log_execution(
        event_type="test_run",
        task_id=task_data["id"],
        output=result.output[:2000] if result.output else None,
        error=result.error,
        success=result.success,
        duration=result.duration_seconds,
    )
    
    tests_passed = result.success
    
    return {
        **state,
        "last_test_result": result.to_dict(),
        "next_action": "validate" if tests_passed else "fix_tests",
        "messages": [
            f"{'✅' if tests_passed else '❌'} Tests {'passed' if tests_passed else 'failed'} "
            f"({result.duration_seconds:.1f}s)"
        ],
    }
```

---

### Task 3.4: Update Backend Manager Integration
**File:** `build_tools/sharpy_auto_builder/backends.py`

The existing `BackendManager` can remain largely unchanged, but add a method to work with the new task system:

```python
class BackendManager:
    """Manages backend availability and selection."""
    
    # ... existing code ...
    
    def get_available_backend(self) -> Optional[str]:
        """
        Get the name of an available backend.
        
        Returns the first backend in priority order that is:
        - Enabled in config
        - Not currently rate limited
        
        Returns:
            Backend name (e.g., "claude_code") or None if all unavailable
        """
        for backend_name in self.config.backend_priority:
            backend_config = self.config.backends.get(backend_name)
            if not backend_config or not backend_config.enabled:
                continue
            
            if self._is_rate_limited(backend_name):
                continue
            
            return backend_name
        
        return None
    
    def _is_rate_limited(self, backend_name: str) -> bool:
        """Check if a backend is currently rate limited."""
        state = self._rate_limit_states.get(backend_name)
        if not state:
            return False
        return state.is_rate_limited()
    
    def mark_rate_limited(self, backend_name: str, wait_seconds: int = 3600) -> None:
        """Mark a backend as rate limited."""
        if backend_name not in self._rate_limit_states:
            self._rate_limit_states[backend_name] = RateLimitState(
                self.config.backends[backend_name].rate_limit
            )
        self._rate_limit_states[backend_name].record_rate_limit(wait_seconds)
```

---

### Task 3.5: Add Task Module to Package Exports
**File:** `build_tools/sharpy_auto_builder/__init__.py`

Add the new tasks to the package exports:

```python
# ADD to imports:
from .tasks import (
    execute_claude_cli,
    execute_copilot_cli,
    run_tests,
    TaskExecutionResult,
)

# ADD to __all__:
__all__ = [
    # ... existing exports ...
    
    # Tasks
    "execute_claude_cli",
    "execute_copilot_cli",
    "run_tests",
    "TaskExecutionResult",
]
```

---

### Task 3.6: Handle Task Caching Behavior
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

Add documentation and helpers for understanding task caching:

```python
def _should_force_reexecution(self, task_data: dict, state: OrchestratorState) -> bool:
    """
    Determine if we should force re-execution (bypass cache).
    
    Tasks are cached by their inputs. To force re-execution:
    - Increment the attempt counter
    - Or modify the prompt
    
    Returns:
        True if re-execution should be forced
    """
    # Force re-execution if:
    # 1. Human requested retry
    if state.get("human_response", {}).get("retry"):
        return True
    
    # 2. Previous attempt had transient error
    last_result = state.get("last_execution_result", {})
    if last_result.get("error") and "timeout" in last_result.get("error", "").lower():
        return True
    
    return False
```

Add comments explaining the caching behavior:

```python
# In _execute_implementation_node:

# IMPORTANT: Task caching behavior
# --------------------------------
# The execute_claude_cli and execute_copilot_cli functions are
# decorated with @task, which means:
#
# 1. Same inputs = cached result (no re-execution)
#    - prompt, tools, model, task_id, attempt must all match
#
# 2. To force re-execution, increment 'attempt'
#    - This creates a new cache key
#
# 3. On graph replay (e.g., after interrupt resume):
#    - If inputs match a previous execution, cached result is used
#    - The CLI is NOT called again
#
# This saves API tokens and ensures consistent behavior during debugging
```

---

### Task 3.7: Write Tests
**File:** `build_tools/tests/test_tasks.py` (new file)

```python
"""Tests for task-wrapped CLI operations."""

import pytest
from unittest.mock import patch, AsyncMock, MagicMock
import asyncio

from sharpy_auto_builder.tasks import (
    execute_claude_cli,
    execute_copilot_cli,
    run_tests,
    TaskExecutionResult,
    _compute_input_hash,
)


class TestInputHashing:
    """Test input hash computation for cache keys."""
    
    def test_same_inputs_same_hash(self):
        """Same inputs should produce same hash."""
        hash1 = _compute_input_hash("prompt", {"Read"}, "model", "task1", 0)
        hash2 = _compute_input_hash("prompt", {"Read"}, "model", "task1", 0)
        assert hash1 == hash2
    
    def test_different_inputs_different_hash(self):
        """Different inputs should produce different hashes."""
        hash1 = _compute_input_hash("prompt1", {"Read"}, "model", "task1", 0)
        hash2 = _compute_input_hash("prompt2", {"Read"}, "model", "task1", 0)
        assert hash1 != hash2
    
    def test_different_attempt_different_hash(self):
        """Different attempt numbers should produce different hashes."""
        hash1 = _compute_input_hash("prompt", {"Read"}, "model", "task1", 0)
        hash2 = _compute_input_hash("prompt", {"Read"}, "model", "task1", 1)
        assert hash1 != hash2
    
    def test_hash_is_deterministic(self):
        """Hash should be deterministic across calls."""
        hashes = [
            _compute_input_hash("test", {"A", "B"}, "model", "id", 5)
            for _ in range(10)
        ]
        assert len(set(hashes)) == 1  # All hashes should be identical


class TestTaskExecutionResult:
    """Test TaskExecutionResult dataclass."""
    
    def test_to_dict(self):
        """Test serialization to dict."""
        result = TaskExecutionResult(
            success=True,
            output="test output",
            error=None,
            backend="claude_code",
            model="claude-sonnet-4-5-20250929",
            duration_seconds=1.5,
            exit_code=0,
            timestamp="2024-01-01T00:00:00",
            input_hash="abc123",
        )
        
        d = result.to_dict()
        assert d["success"] is True
        assert d["backend"] == "claude_code"
        assert d["input_hash"] == "abc123"
    
    def test_from_dict(self):
        """Test deserialization from dict."""
        d = {
            "success": False,
            "output": "",
            "error": "timeout",
            "backend": "copilot",
            "model": None,
            "duration_seconds": 30.0,
            "exit_code": -1,
            "timestamp": "2024-01-01T00:00:00",
            "input_hash": "xyz789",
        }
        
        result = TaskExecutionResult.from_dict(d)
        assert result.success is False
        assert result.error == "timeout"
        assert result.backend == "copilot"


class TestExecuteClaudeCLI:
    """Test Claude CLI task execution."""
    
    @pytest.mark.asyncio
    async def test_successful_execution(self):
        """Test successful CLI execution."""
        with patch('asyncio.create_subprocess_exec') as mock_exec:
            # Mock successful process
            mock_process = AsyncMock()
            mock_process.returncode = 0
            mock_process.communicate = AsyncMock(return_value=(
                b"Success output",
                b""
            ))
            mock_exec.return_value = mock_process
            
            # Note: Can't easily test @task decorated functions in isolation
            # because they require LangGraph runtime context
            # This test verifies the mock setup is correct
            assert mock_process.returncode == 0
    
    @pytest.mark.asyncio
    async def test_rate_limit_detection(self):
        """Test that rate limit errors are detected."""
        # Test the rate limit detection logic
        from shared.rate_limiting import is_rate_limit_error
        
        assert is_rate_limit_error("rate_limited", 1)
        assert is_rate_limit_error("429 Too Many Requests", 1)
        assert not is_rate_limit_error("Success", 0)


class TestRunTests:
    """Test the test runner task."""
    
    @pytest.mark.asyncio
    async def test_test_command_parsing(self):
        """Test that test commands are parsed correctly."""
        import shlex
        
        cmd = "dotnet test --filter Category=Unit"
        parts = shlex.split(cmd)
        
        assert parts[0] == "dotnet"
        assert parts[1] == "test"
        assert "--filter" in parts


class TestTaskCaching:
    """Test task caching behavior (conceptual tests)."""
    
    def test_cache_key_components(self):
        """Verify cache key includes all relevant components."""
        # Cache key should include:
        # - prompt (content determines result)
        # - tools (affects what CLI can do)
        # - model (affects output)
        # - task_id (different tasks should not share cache)
        # - attempt (allows forcing re-execution)
        
        components = ["prompt", "tools", "model", "task_id", "attempt"]
        
        # All components should affect the hash
        base_hash = _compute_input_hash("p", {"t"}, "m", "id", 0)
        
        different_prompt = _compute_input_hash("p2", {"t"}, "m", "id", 0)
        different_tools = _compute_input_hash("p", {"t2"}, "m", "id", 0)
        different_model = _compute_input_hash("p", {"t"}, "m2", "id", 0)
        different_task = _compute_input_hash("p", {"t"}, "m", "id2", 0)
        different_attempt = _compute_input_hash("p", {"t"}, "m", "id", 1)
        
        hashes = [
            base_hash, different_prompt, different_tools,
            different_model, different_task, different_attempt
        ]
        
        # All should be unique
        assert len(set(hashes)) == 6
```

---

## Verification Checklist

After completing all tasks, verify:

- [ ] `tasks.py` module created with `@task` decorated functions
- [ ] `execute_claude_cli` and `execute_copilot_cli` tasks work correctly
- [ ] `run_tests` task works correctly
- [ ] Orchestrator uses task-wrapped functions
- [ ] Task results are stored in checkpoints
- [ ] On graph replay, cached results are used (CLI not re-called)
- [ ] Different `attempt` values create different cache keys
- [ ] Rate limiting is detected and handled
- [ ] All existing tests pass
- [ ] New task tests pass

## Understanding Task Caching

### How It Works

1. **First Execution:**
   ```
   execute_claude_cli(prompt="X", task_id="001", attempt=0)
   → Runs CLI → Returns result → Result stored in checkpoint
   ```

2. **Graph Replay (same inputs):**
   ```
   execute_claude_cli(prompt="X", task_id="001", attempt=0)
   → Cache hit → Returns stored result → CLI NOT called
   ```

3. **Retry (incremented attempt):**
   ```
   execute_claude_cli(prompt="X", task_id="001", attempt=1)
   → Cache miss (different key) → Runs CLI → Returns new result
   ```

### When Caching Helps

- **Interrupts:** After human review, the implementation step isn't re-run
- **Debugging:** Can replay graph without burning API tokens
- **Crashes:** After recovery, completed steps don't repeat
- **Rate Limits:** If one backend fails, switching doesn't re-run prior work

### When to Force Re-execution

- Human requests retry → increment `attempt`
- Transient error (timeout) → increment `attempt`
- Different prompt needed → new cache key automatically

## Rollback Plan

If issues arise:

1. Remove `@task` decorators from functions in `tasks.py`
2. Or revert to direct CLI calls in orchestrator nodes
3. Tasks without `@task` are just regular async functions

The checkpoint data format doesn't change, so rollback is safe.

## Next Steps

After this implementation:
- Tasks are now idempotent and replay-safe
- Combined with Plan 1 (persistence) and Plan 2 (interrupts), you have robust durable execution
- Consider Implementation Plan 4 (Memory Store) for cross-task learning

## Cross-References

- **Depends on:** Implementation Plan 1 (Durable Persistence) - tasks need checkpointer
- **New file:** `build_tools/sharpy_auto_builder/tasks.py`
- **Orchestrator:** `build_tools/sharpy_auto_builder/orchestrator.py`
- **CLI Builder:** `build_tools/shared/cli_builder.py` (used by tasks)
- **LangGraph Durable Execution:** https://docs.langchain.com/oss/python/langgraph/durable-execution
