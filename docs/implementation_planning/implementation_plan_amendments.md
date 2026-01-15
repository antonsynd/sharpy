# Implementation Plan Amendments: High & Medium Priority Enhancements

This document contains amendments to Implementation Plans 1-4 based on current LangGraph best practices and documentation. Each section specifies where it should be inserted in the original plan.

---

## Amendments to Plan 1: Durable Persistence

### Amendment 1.A: Add Checkpoint Retention Policy

**Insert after Task 1.3 (Update Orchestrator to Use SqliteSaver)**

---

### Task 1.3.1: Add Checkpoint Retention and Cleanup

**File:** `build_tools/sharpy_auto_builder/config.py`

Add checkpoint configuration options:

```python
from dataclasses import dataclass, field
from typing import Literal

@dataclass
class CheckpointConfig:
    """Configuration for checkpoint persistence."""
    
    # Durability mode controls when checkpoints are written
    # - "async": Checkpoints written asynchronously (better performance, tiny crash risk)
    # - "sync": Checkpoints written synchronously before next step (maximum durability)
    durability_mode: Literal["async", "sync"] = "async"
    
    # Maximum checkpoints to retain per thread (prevents DB bloat)
    # Older checkpoints are pruned when this limit is exceeded
    max_checkpoints_per_thread: int = 100
    
    # How often to run cleanup (in terms of checkpoints created)
    cleanup_interval: int = 50
    
    # Retain failed run checkpoints longer for debugging
    retain_failed_checkpoints_days: int = 7


@dataclass
class Config(BaseConfig):
    # ... existing fields ...
    
    # Checkpoint configuration
    checkpoint: CheckpointConfig = field(default_factory=CheckpointConfig)
```

**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

Add checkpoint cleanup method to the `Orchestrator` class:

```python
def _setup_checkpoint_cleanup(self) -> None:
    """Configure automatic checkpoint cleanup."""
    self._checkpoint_count = 0
    self._cleanup_interval = self.config.checkpoint.cleanup_interval

def _maybe_cleanup_checkpoints(self, thread_id: str) -> None:
    """
    Periodically clean up old checkpoints to prevent database bloat.
    
    Called after each checkpoint is created. Only runs actual cleanup
    every `cleanup_interval` checkpoints.
    """
    self._checkpoint_count += 1
    
    if self._checkpoint_count % self._cleanup_interval != 0:
        return
    
    try:
        self._cleanup_thread_checkpoints(thread_id)
    except Exception as e:
        # Don't fail the workflow for cleanup issues
        print(f"Warning: Checkpoint cleanup failed: {e}")

def _cleanup_thread_checkpoints(self, thread_id: str) -> int:
    """
    Remove old checkpoints for a thread, keeping the most recent N.
    
    Returns:
        Number of checkpoints deleted
    """
    max_keep = self.config.checkpoint.max_checkpoints_per_thread
    
    # Get all checkpoints for this thread
    config = {"configurable": {"thread_id": thread_id}}
    
    # Use the checkpointer's list method to get checkpoint history
    checkpoints = list(self.checkpointer.list(config))
    
    if len(checkpoints) <= max_keep:
        return 0
    
    # Sort by timestamp (newest first) and identify old ones
    checkpoints_to_delete = checkpoints[max_keep:]
    deleted_count = 0
    
    for checkpoint in checkpoints_to_delete:
        try:
            # Delete using checkpoint_id
            self._db_connection.execute(
                "DELETE FROM checkpoints WHERE thread_id = ? AND checkpoint_id = ?",
                (thread_id, checkpoint.config["configurable"]["checkpoint_id"])
            )
            deleted_count += 1
        except Exception:
            pass  # Continue with other deletions
    
    self._db_connection.commit()
    
    if deleted_count > 0:
        print(f"Cleaned up {deleted_count} old checkpoints for thread {thread_id}")
    
    return deleted_count

def get_checkpoint_stats(self) -> dict:
    """Get statistics about checkpoint storage."""
    cursor = self._db_connection.cursor()
    
    # Count checkpoints per thread
    cursor.execute("""
        SELECT thread_id, COUNT(*) as count, 
               MIN(checkpoint_id) as oldest,
               MAX(checkpoint_id) as newest
        FROM checkpoints 
        GROUP BY thread_id
    """)
    
    threads = {}
    total = 0
    for row in cursor.fetchall():
        threads[row[0]] = {
            "count": row[1],
            "oldest_checkpoint": row[2],
            "newest_checkpoint": row[3],
        }
        total += row[1]
    
    # Get database file size
    db_size_bytes = self.config.checkpoint_db_path.stat().st_size if self.config.checkpoint_db_path.exists() else 0
    
    return {
        "total_checkpoints": total,
        "threads": threads,
        "database_size_mb": round(db_size_bytes / (1024 * 1024), 2),
    }
```

**Update `__init__` method:**

```python
def __init__(self, config: Config):
    # ... existing initialization ...
    
    # Setup checkpoint cleanup tracking
    self._setup_checkpoint_cleanup()
```

---

### Task 1.3.2: Add CLI Commands for Checkpoint Management

**File:** `build_tools/sharpy_auto_builder/cli.py`

Add checkpoint management commands:

```python
@cli.command()
@click.option('--thread-id', help='Specific thread to inspect')
def checkpoint_stats(thread_id: Optional[str]):
    """Show checkpoint storage statistics."""
    config = Config()
    
    with Orchestrator(config) as orch:
        stats = orch.get_checkpoint_stats()
        
        console.print("\n[bold]Checkpoint Statistics[/bold]")
        console.print(f"Total checkpoints: {stats['total_checkpoints']}")
        console.print(f"Database size: {stats['database_size_mb']} MB")
        console.print(f"Threads: {len(stats['threads'])}")
        
        if thread_id and thread_id in stats['threads']:
            thread_stats = stats['threads'][thread_id]
            console.print(f"\nThread {thread_id}:")
            console.print(f"  Checkpoints: {thread_stats['count']}")
        elif stats['threads']:
            console.print("\n[dim]Recent threads:[/dim]")
            for tid, tstat in list(stats['threads'].items())[:5]:
                console.print(f"  {tid}: {tstat['count']} checkpoints")


@cli.command()
@click.option('--thread-id', required=True, help='Thread to clean up')
@click.option('--keep', default=50, help='Number of checkpoints to keep')
@click.option('--dry-run', is_flag=True, help='Show what would be deleted')
def checkpoint_cleanup(thread_id: str, keep: int, dry_run: bool):
    """Clean up old checkpoints for a thread."""
    config = Config()
    
    with Orchestrator(config) as orch:
        if dry_run:
            # Just show stats
            stats = orch.get_checkpoint_stats()
            if thread_id in stats['threads']:
                current = stats['threads'][thread_id]['count']
                would_delete = max(0, current - keep)
                console.print(f"Would delete {would_delete} checkpoints (keeping {keep})")
            else:
                console.print(f"Thread {thread_id} not found")
        else:
            deleted = orch._cleanup_thread_checkpoints(thread_id)
            console.print(f"Deleted {deleted} checkpoints")
```

---

### Amendment 1.B: Add Verification Checklist Items

**Append to Verification Checklist:**

- [ ] Checkpoint cleanup runs automatically every N checkpoints
- [ ] `checkpoint-stats` CLI command shows storage statistics
- [ ] `checkpoint-cleanup` CLI command removes old checkpoints
- [ ] Database size stays bounded with long-running workflows

---

## Amendments to Plan 2: Native Interrupts

### Amendment 2.A: Pre-Interrupt Idempotency Guidance

**Insert as new section after "Important Rules for Interrupts"**

---

## Critical: Pre-Interrupt Code Idempotency

**This is the most common source of bugs with interrupts.** Code that runs before `interrupt()` will run again when the graph resumes. This can cause:

- Duplicate notifications
- Duplicate API calls
- Inconsistent state
- Wasted resources

### The Problem Illustrated

```python
# ❌ WRONG: Side effects before interrupt
async def _request_human_review_node(self, state: OrchestratorState) -> OrchestratorState:
    # These run AGAIN when human responds!
    await self._send_slack_notification("Review needed")  # Duplicate notification!
    self._log_to_external_service("Review requested")     # Duplicate log!
    tokens_used = await self._call_llm_for_summary()      # Wasted API call!
    
    response = interrupt(payload)  # <-- Execution pauses here
    
    # Only this code runs once (after human responds)
    return self._process_response(response)
```

### Solutions

**Solution 1: Wrap side effects in @task (Recommended)**

```python
from langgraph.func import task

@task
async def send_review_notification(task_id: str, channel: str) -> bool:
    """Send notification - cached, won't re-run on resume."""
    await slack_client.post(channel, f"Review needed for {task_id}")
    return True

async def _request_human_review_node(self, state: OrchestratorState) -> OrchestratorState:
    task_data = state["current_task"]
    
    # ✅ CORRECT: Task is cached, won't re-send on resume
    await send_review_notification(task_data["id"], "#reviews")
    
    response = interrupt(payload)
    return self._process_response(response)
```

**Solution 2: Move side effects after interrupt**

```python
async def _request_human_review_node(self, state: OrchestratorState) -> OrchestratorState:
    # ✅ Only pure/idempotent code before interrupt
    payload = self._build_review_payload(state)  # Pure function
    
    response = interrupt(payload)
    
    # Side effects AFTER interrupt (only runs once)
    await self._send_completion_notification(response)
    return self._process_response(response)
```

**Solution 3: Use flags to skip on resume**

```python
async def _request_human_review_node(self, state: OrchestratorState) -> OrchestratorState:
    # Check if we already sent notification (stored in state)
    if not state.get("review_notification_sent"):
        await self._send_notification("Review needed")
        # Update state to prevent re-send
        # Note: This requires careful state management
    
    response = interrupt(payload)
    return self._process_response(response)
```

### Checklist: Is My Pre-Interrupt Code Safe?

Before every `interrupt()` call, verify the preceding code:

| Code Type | Safe? | Solution |
|-----------|-------|----------|
| Building payload from state | ✅ Yes | Pure computation |
| Logging to internal logger | ✅ Yes | Idempotent |
| Reading from database | ✅ Yes | No side effects |
| Sending notifications | ❌ No | Wrap in @task |
| API calls with side effects | ❌ No | Wrap in @task |
| Incrementing counters | ❌ No | Move after interrupt |
| Writing to external service | ❌ No | Wrap in @task |
| Random number generation | ❌ No | Wrap in @task |
| datetime.now() for logging | ⚠️ Depends | Consider @task if precision matters |

---

### Amendment 2.B: Add Input Validation Loop Pattern

**Insert as Task 2.5 (after Task 2.4)**

---

### Task 2.5: Implement Input Validation Loop

**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

Add a pattern for validating human input and re-prompting if invalid:

```python
from typing import Callable, TypeVar, Generic

T = TypeVar('T')

def interrupt_with_validation(
    payload: dict,
    validator: Callable[[Any], tuple[bool, str]],
    max_attempts: int = 3,
) -> Any:
    """
    Interrupt with automatic input validation and re-prompting.
    
    Args:
        payload: The interrupt payload to display to user
        validator: Function that returns (is_valid, error_message)
        max_attempts: Maximum validation attempts before accepting anyway
    
    Returns:
        The validated response from the user
    
    Example:
        def validate_approval(response):
            if response.get("decision") in ["approve", "reject", "retry"]:
                return True, ""
            return False, f"Invalid decision: {response.get('decision')}"
        
        response = interrupt_with_validation(
            payload={"type": "review", ...},
            validator=validate_approval,
        )
    """
    attempt = 0
    current_payload = payload.copy()
    
    while attempt < max_attempts:
        response = interrupt(current_payload)
        
        is_valid, error_message = validator(response)
        
        if is_valid:
            return response
        
        attempt += 1
        
        # Update payload with validation error for re-prompt
        current_payload = {
            **payload,
            "validation_error": error_message,
            "attempt": attempt,
            "prompt_override": f"Invalid input: {error_message}. Please try again.",
        }
    
    # After max attempts, accept whatever we got (with warning)
    print(f"Warning: Accepting input after {max_attempts} failed validations")
    return response


# Validation functions for common response types

def validate_review_response(response: dict) -> tuple[bool, str]:
    """Validate a human review response."""
    if not isinstance(response, dict):
        return False, f"Expected dict, got {type(response).__name__}"
    
    # Must have 'approved' or 'retry' field
    if "approved" not in response and "retry" not in response:
        return False, "Response must include 'approved' or 'retry' field"
    
    # If approved is present, must be boolean
    if "approved" in response and not isinstance(response["approved"], bool):
        return False, f"'approved' must be boolean, got {type(response['approved']).__name__}"
    
    return True, ""


def validate_question_response(response: dict, options: list[str] = None) -> tuple[bool, str]:
    """Validate a human question response."""
    if not isinstance(response, dict):
        return False, f"Expected dict, got {type(response).__name__}"
    
    if "answer" not in response:
        return False, "Response must include 'answer' field"
    
    # If options were provided, validate against them
    if options and response["answer"] not in options:
        return False, f"Answer must be one of: {options}"
    
    return True, ""
```

**Update `_request_human_review_node` to use validation:**

```python
async def _request_human_review_node(self, state: OrchestratorState) -> OrchestratorState:
    """Request human review with input validation."""
    task_data = state["current_task"]
    
    # Build payload (idempotent)
    review_payload: HumanReviewPayload = {
        "type": "review",
        "task_id": task_data["id"],
        # ... rest of payload building
    }
    
    # Use validation wrapper
    human_response = interrupt_with_validation(
        payload=review_payload,
        validator=validate_review_response,
        max_attempts=3,
    )
    
    # Log and process (runs only once, after valid response)
    self._log_execution(
        event_type="human_review_response",
        task_id=task_data["id"],
        extra={"approved": human_response.get("approved")},
    )
    
    # Route based on validated response
    if human_response.get("approved"):
        return {**state, "next_action": "commit", "human_response": human_response}
    elif human_response.get("retry"):
        return {**state, "next_action": "retry", "human_response": human_response}
    else:
        return {**state, "next_action": "skip", "human_response": human_response}
```

---

### Amendment 2.C: Add Tests for Validation Loop

**Append to Task 2.10 tests:**

```python
class TestInterruptValidation:
    """Test interrupt validation patterns."""
    
    def test_validate_review_response_valid(self):
        """Test valid review responses pass validation."""
        valid_responses = [
            {"approved": True},
            {"approved": False},
            {"approved": True, "feedback": "Looks good"},
            {"retry": True},
            {"approved": False, "retry": True, "feedback": "Try again"},
        ]
        
        for response in valid_responses:
            is_valid, error = validate_review_response(response)
            assert is_valid, f"Should be valid: {response}, got error: {error}"
    
    def test_validate_review_response_invalid(self):
        """Test invalid review responses fail validation."""
        invalid_responses = [
            "just a string",
            {"wrong_field": True},
            {"approved": "yes"},  # Should be bool
            None,
            [],
        ]
        
        for response in invalid_responses:
            is_valid, error = validate_review_response(response)
            assert not is_valid, f"Should be invalid: {response}"
            assert error, "Should have error message"
    
    def test_validate_question_response_with_options(self):
        """Test question validation with predefined options."""
        options = ["yes", "no", "skip"]
        
        # Valid
        is_valid, _ = validate_question_response({"answer": "yes"}, options)
        assert is_valid
        
        # Invalid - not in options
        is_valid, error = validate_question_response({"answer": "maybe"}, options)
        assert not is_valid
        assert "yes" in error and "no" in error  # Should list valid options
```

---

## Amendments to Plan 3: Idempotent Tasks

### Amendment 3.A: Deployment Warning and Fallback Patterns

**Insert as new section after "Understanding Task Caching"**

---

## Critical: Task Caching Deployment Considerations

### Known Limitation: API Server Environments

The `@task` decorator relies on LangGraph's runtime context for caching. In some deployment scenarios, particularly when using LangGraph API servers, task caching may not work as expected.

**Symptoms of broken task caching:**
- Tasks re-execute on graph resume
- Duplicate API calls after human-in-the-loop interrupts
- Different results on graph replay

### Mitigation Strategies

**Strategy 1: Verify in Your Environment**

Before relying on `@task` caching, test it explicitly:

```python
# Test script: verify_task_caching.py
import asyncio
from langgraph.func import task
from langgraph.checkpoint.sqlite import SqliteSaver
import sqlite3

execution_count = 0

@task
async def counted_task(input_value: str) -> str:
    global execution_count
    execution_count += 1
    return f"Executed {execution_count} times with {input_value}"

async def test_caching():
    """Test that @task caching works in current environment."""
    global execution_count
    execution_count = 0
    
    # Setup checkpointer (required for task caching)
    conn = sqlite3.connect(":memory:")
    checkpointer = SqliteSaver(conn)
    checkpointer.setup()
    
    # Create a simple graph that uses the task
    # ... (graph setup code)
    
    # Run once
    result1 = await counted_task("test")
    
    # Simulate resume by calling again
    result2 = await counted_task("test")
    
    if execution_count == 1:
        print("✅ Task caching works! Task executed only once.")
    else:
        print(f"❌ Task caching NOT working! Task executed {execution_count} times.")
        print("   Consider using node-boundary pattern instead.")

if __name__ == "__main__":
    asyncio.run(test_caching())
```

**Strategy 2: Node-Boundary Pattern (Most Reliable)**

If task caching is unreliable, use node boundaries instead. Each completed node IS reliably checkpointed.

```python
# BEFORE: Single node with multiple tasks (relies on @task caching)
async def _execute_and_validate_node(self, state):
    # If tasks don't cache, both re-run on resume
    result = await execute_claude_cli(state["prompt"])  # @task
    validation = await run_validation(result)           # @task
    return {"result": result, "validation": validation}

# AFTER: Separate nodes (each node completion is checkpointed)
async def _execute_implementation_node(self, state):
    """Execute CLI. Checkpointed on completion."""
    result = await self._run_cli(state["prompt"])
    return {"execution_result": result, "next_action": "validate"}

async def _validate_implementation_node(self, state):
    """Validate. Only runs if execution node completed."""
    validation = await self._run_validation(state["execution_result"])
    return {"validation_result": validation, "next_action": "continue"}

# Wire as separate nodes
graph.add_node("execute", self._execute_implementation_node)
graph.add_node("validate", self._validate_implementation_node)
graph.add_edge("execute", "validate")
```

**Strategy 3: External Idempotency Tracking**

For critical operations, implement explicit idempotency:

```python
import hashlib
from pathlib import Path

class IdempotencyTracker:
    """Track completed operations to prevent duplicates."""
    
    def __init__(self, storage_path: Path):
        self.storage_path = storage_path
        self.storage_path.mkdir(parents=True, exist_ok=True)
    
    def _get_key(self, operation: str, *args, **kwargs) -> str:
        """Generate idempotency key from operation and inputs."""
        content = f"{operation}:{args}:{sorted(kwargs.items())}"
        return hashlib.sha256(content.encode()).hexdigest()[:32]
    
    def _get_marker_path(self, key: str) -> Path:
        return self.storage_path / f"{key}.done"
    
    def is_completed(self, operation: str, *args, **kwargs) -> bool:
        """Check if operation was already completed."""
        key = self._get_key(operation, *args, **kwargs)
        return self._get_marker_path(key).exists()
    
    def mark_completed(self, operation: str, result: Any, *args, **kwargs) -> None:
        """Mark operation as completed and store result."""
        key = self._get_key(operation, *args, **kwargs)
        marker = self._get_marker_path(key)
        marker.write_text(json.dumps({
            "result": result,
            "completed_at": datetime.now().isoformat(),
        }))
    
    def get_cached_result(self, operation: str, *args, **kwargs) -> Optional[Any]:
        """Get cached result if operation was completed."""
        key = self._get_key(operation, *args, **kwargs)
        marker = self._get_marker_path(key)
        if marker.exists():
            data = json.loads(marker.read_text())
            return data.get("result")
        return None


# Usage in execute node
async def _execute_implementation_node(self, state):
    tracker = IdempotencyTracker(self.config.state_dir / "idempotency")
    
    task_id = state["current_task"]["id"]
    attempt = state["execution_attempt"]
    
    # Check if already completed
    cached = tracker.get_cached_result("execute_cli", task_id, attempt)
    if cached is not None:
        print(f"Using cached result for {task_id} attempt {attempt}")
        return {"execution_result": cached}
    
    # Actually execute
    result = await self._run_cli(state["prompt"])
    
    # Mark completed
    tracker.mark_completed("execute_cli", result.to_dict(), task_id, attempt)
    
    return {"execution_result": result}
```

### Recommendation for Sharpy Auto Builder

Given that the orchestrator:
1. Makes expensive CLI calls (Claude Code, Copilot)
2. Uses human-in-the-loop (interrupts)
3. Needs reliable resumption

**Recommended approach:**

1. **Use node boundaries** for the main execution flow (most reliable)
2. **Use @task** for subsidiary operations (logging, notifications)
3. **Add IdempotencyTracker** as a safety net for critical CLI calls

---

### Amendment 3.B: Update Task Module with Fallback

**Update Task 3.1 (tasks.py) with fallback support:**

```python
"""
Task-wrapped operations for idempotent execution.

IMPORTANT: @task caching behavior may vary by deployment environment.
This module includes fallback idempotency tracking for reliability.
See Amendment 3.A for details.
"""

import asyncio
import hashlib
import json
from dataclasses import dataclass, asdict
from datetime import datetime
from pathlib import Path
from typing import Optional, Set, Any

from langgraph.func import task

# ... existing imports ...


class TaskIdempotencyFallback:
    """
    Fallback idempotency tracker for when @task caching is unreliable.
    
    Uses file-based markers to track completed operations.
    Thread-safe via atomic file operations.
    """
    
    def __init__(self, base_path: Path):
        self.base_path = base_path / ".task_idempotency"
        self.base_path.mkdir(parents=True, exist_ok=True)
    
    def _marker_path(self, input_hash: str) -> Path:
        return self.base_path / f"{input_hash}.json"
    
    def get_cached(self, input_hash: str) -> Optional[dict]:
        """Get cached result if available."""
        marker = self._marker_path(input_hash)
        if marker.exists():
            try:
                return json.loads(marker.read_text())
            except (json.JSONDecodeError, IOError):
                return None
        return None
    
    def cache_result(self, input_hash: str, result: dict) -> None:
        """Cache a result for future lookups."""
        marker = self._marker_path(input_hash)
        # Atomic write via temp file
        temp = marker.with_suffix('.tmp')
        temp.write_text(json.dumps(result))
        temp.rename(marker)


# Global fallback tracker (initialized lazily)
_fallback_tracker: Optional[TaskIdempotencyFallback] = None

def _get_fallback_tracker() -> TaskIdempotencyFallback:
    global _fallback_tracker
    if _fallback_tracker is None:
        # Use current working directory by default
        _fallback_tracker = TaskIdempotencyFallback(Path.cwd() / "state")
    return _fallback_tracker


@task
async def execute_claude_cli(
    prompt: str,
    tools: Set[str],
    model: str = "claude-sonnet-4-5-20250929",
    timeout: int = 300,
    working_dir: str = ".",
    task_id: str = "",
    attempt: int = 0,
    use_fallback_idempotency: bool = True,  # NEW: Enable fallback
) -> TaskExecutionResult:
    """
    Execute Claude Code CLI as a memoized task.
    
    This function uses multiple layers of idempotency:
    1. @task decorator (LangGraph native, may not work in all environments)
    2. Fallback file-based tracking (reliable, optional)
    
    Args:
        ... existing args ...
        use_fallback_idempotency: If True, use file-based idempotency
            as a fallback in case @task caching doesn't work.
    """
    input_hash = _compute_input_hash(prompt, tools, model, task_id, attempt)
    
    # Check fallback cache first
    if use_fallback_idempotency:
        tracker = _get_fallback_tracker()
        cached = tracker.get_cached(input_hash)
        if cached is not None:
            print(f"[Task] Using fallback cached result for {task_id}")
            return TaskExecutionResult.from_dict(cached)
    
    # ... existing execution code ...
    
    start_time = datetime.now()
    
    # Build and execute command
    # ... (existing implementation) ...
    
    result = TaskExecutionResult(
        success=True,
        output=stdout_text,
        error=None,
        backend="claude_code",
        model=model,
        duration_seconds=duration,
        exit_code=process.returncode,
        timestamp=start_time.isoformat(),
        input_hash=input_hash,
    )
    
    # Store in fallback cache
    if use_fallback_idempotency:
        tracker.cache_result(input_hash, result.to_dict())
    
    return result
```

---

## New Section: Command-Based Routing

**Insert as new Implementation Plan 2.5 or as amendment to Plan 2**

---

## Amendment 2.D: Command-Based Routing (Cleaner Flow Control)

### Background

The current orchestrator uses conditional edges for routing:

```python
graph.add_conditional_edges(
    "analyze_response",
    self._route_after_analysis,
    {
        "accept": "commit_changes",
        "retry": "execute_implementation", 
        "review": "request_human_review",
        "error": "handle_error",
    }
)
```

This works but has drawbacks:
- Routing logic is separate from node logic
- Need to maintain edge mappings
- Harder to understand flow at a glance

### Solution: Use `Command` for Dynamic Routing

LangGraph's `Command` primitive combines state updates with routing decisions inside nodes:

```python
from langgraph.types import Command

async def _analyze_response_node(self, state: OrchestratorState) -> Command:
    """
    Analyze agent response and route dynamically.
    
    Returns Command to update state AND specify next node.
    """
    analysis = self.response_analyzer.analyze(
        state["last_execution_result"],
        state["current_task"],
    )
    
    # Decision logic stays with the node
    if analysis.auto_accept:
        return Command(
            goto="commit_changes",
            update={
                "response_analysis": analysis.to_dict(),
                "next_action": "commit",
                "messages": [f"✅ Auto-accepting task {state['current_task']['id']}"],
            }
        )
    
    elif analysis.needs_retry and state["execution_attempt"] < self.config.max_retries:
        return Command(
            goto="execute_implementation",
            update={
                "response_analysis": analysis.to_dict(),
                "execution_attempt": state["execution_attempt"] + 1,
                "next_action": "retry",
                "messages": [f"🔄 Retrying (attempt {state['execution_attempt'] + 1})"],
            }
        )
    
    elif analysis.has_critical_issues:
        return Command(
            goto="request_human_review",
            update={
                "response_analysis": analysis.to_dict(),
                "next_action": "review",
                "messages": ["⚠️ Critical issues detected, requesting review"],
            }
        )
    
    else:
        return Command(
            goto="handle_error",
            update={
                "response_analysis": analysis.to_dict(),
                "error_message": "Unable to determine next action",
                "next_action": "error",
            }
        )
```

### Benefits of Command-Based Routing

1. **Colocated logic**: Routing decisions live with analysis logic
2. **Type safety**: IDE can track where nodes go
3. **Atomic updates**: State update and routing in one operation
4. **Cleaner graph**: No conditional edge definitions needed

### Migration Guide

**Step 1: Update node return types**

```python
# Before
async def _some_node(self, state: OrchestratorState) -> OrchestratorState:
    # Process...
    return {**state, "next_action": "some_action"}

# After
async def _some_node(self, state: OrchestratorState) -> Command:
    # Process...
    return Command(
        goto="next_node_name",
        update={"field": "value"},
    )
```

**Step 2: Remove conditional edges**

```python
# Before
graph.add_conditional_edges(
    "analyze_response",
    self._route_after_analysis,
    {"accept": "commit", "retry": "execute", "review": "human_review"}
)

# After - no edge definition needed, Command handles routing
graph.add_node("analyze_response", self._analyze_response_node)
# The node's Command specifies where to go
```

**Step 3: Update graph to expect Commands**

Nodes that return `Command` don't need outgoing edges defined - the Command's `goto` field handles routing.

### Nodes to Convert

| Node | Current Routing | Command Benefit |
|------|-----------------|-----------------|
| `analyze_response` | Conditional edge with 4 branches | Logic stays with analysis |
| `run_tests` | Conditional based on pass/fail | Atomic state + route |
| `validate_*` | Multiple validation outcomes | Clearer validation flow |
| `process_human_response` | Based on approval/retry/reject | Response handling collocated |

### Example: Converting `run_tests` Node

```python
# BEFORE: Returns state, routing via conditional edge
async def _run_tests_node(self, state: OrchestratorState) -> OrchestratorState:
    result = await self._execute_tests()
    
    if result.passed:
        return {**state, "test_result": result, "next_action": "validate"}
    else:
        return {**state, "test_result": result, "next_action": "fix"}

def _route_after_tests(self, state: OrchestratorState) -> str:
    return state["next_action"]

# Graph setup
graph.add_conditional_edges("run_tests", self._route_after_tests, 
                           {"validate": "validate_spec", "fix": "fix_tests"})


# AFTER: Returns Command, routing inline
async def _run_tests_node(self, state: OrchestratorState) -> Command:
    result = await self._execute_tests()
    
    if result.passed:
        return Command(
            goto="validate_spec_adherence",
            update={
                "test_result": result.to_dict(),
                "messages": [f"✅ Tests passed ({result.tests_run} tests)"],
            }
        )
    else:
        return Command(
            goto="fix_test_failures",
            update={
                "test_result": result.to_dict(),
                "fix_attempt": state.get("fix_attempt", 0) + 1,
                "messages": [f"❌ Tests failed: {result.failure_summary}"],
            }
        )

# Graph setup - simpler, no conditional edge needed
graph.add_node("run_tests", self._run_tests_node)
```

---

## New Section: Basic Middleware for Logging

**Add as optional enhancement to any plan**

---

## Optional Enhancement: Middleware for Cross-Cutting Concerns

### Background

The orchestrator has logging scattered across nodes:

```python
async def _execute_implementation_node(self, state):
    self._log_step_start("execute_implementation", state["current_task"]["id"])
    # ... implementation ...
    self._log_step_end("execute_implementation", state["current_task"]["id"])
    return state
```

This is repetitive and clutters business logic.

### Solution: LangChain Middleware

Middleware provides hooks that run before/after operations:

```python
# File: build_tools/sharpy_auto_builder/middleware.py
"""
Middleware for cross-cutting concerns.

Provides hooks for logging, monitoring, and validation
without cluttering node logic.
"""

from datetime import datetime
from typing import Any, Optional
from langchain.agents.middleware import before_agent, after_agent

# Track timing for performance monitoring
_step_timings: dict[str, datetime] = {}


@before_agent
async def log_node_start(state: dict, runtime) -> Optional[dict]:
    """Log when any node starts execution."""
    node_name = runtime.current_node if hasattr(runtime, 'current_node') else 'unknown'
    task_id = state.get("current_task", {}).get("id", "no_task")
    
    _step_timings[f"{node_name}:{task_id}"] = datetime.now()
    
    # Emit custom event for streaming
    if hasattr(runtime, 'stream_writer'):
        runtime.stream_writer({
            "type": "node_start",
            "node": node_name,
            "task_id": task_id,
            "timestamp": datetime.now().isoformat(),
        })
    
    print(f"▶️  Starting: {node_name} (task: {task_id})")
    return None  # Don't modify state


@after_agent
async def log_node_end(state: dict, runtime) -> Optional[dict]:
    """Log when any node completes."""
    node_name = runtime.current_node if hasattr(runtime, 'current_node') else 'unknown'
    task_id = state.get("current_task", {}).get("id", "no_task")
    
    # Calculate duration
    key = f"{node_name}:{task_id}"
    start_time = _step_timings.pop(key, None)
    duration = (datetime.now() - start_time).total_seconds() if start_time else 0
    
    if hasattr(runtime, 'stream_writer'):
        runtime.stream_writer({
            "type": "node_end",
            "node": node_name,
            "task_id": task_id,
            "duration_seconds": duration,
            "timestamp": datetime.now().isoformat(),
        })
    
    print(f"✓  Completed: {node_name} ({duration:.2f}s)")
    return None


@before_agent
async def validate_state_invariants(state: dict, runtime) -> Optional[dict]:
    """Validate state invariants before each node."""
    # Example invariants
    errors = []
    
    # If we have a current task, it should have an ID
    if state.get("current_task") and not state["current_task"].get("id"):
        errors.append("current_task missing 'id' field")
    
    # Attempt counters should be non-negative
    if state.get("execution_attempt", 0) < 0:
        errors.append("execution_attempt is negative")
    
    if errors:
        print(f"⚠️  State invariant violations: {errors}")
        # Could raise exception to halt execution
    
    return None


# Middleware for summarizing long contexts (optional, requires LLM)
try:
    from langchain.agents.middleware import SummarizationMiddleware
    
    context_summarizer = SummarizationMiddleware(
        model="openai:gpt-4o-mini",  # Cheap model for summarization
        max_tokens_before_summary=8000,
        messages_to_keep=10,
        summary_prompt="Summarize the implementation context concisely, preserving key decisions and errors.",
    )
except ImportError:
    context_summarizer = None
```

### Using Middleware in Orchestrator

```python
# In orchestrator.py

from .middleware import log_node_start, log_node_end, validate_state_invariants

class Orchestrator:
    def __init__(self, config: Config):
        # ... existing init ...
        
        # Collect middleware
        self._middleware = [
            log_node_start,
            log_node_end,
            validate_state_invariants,
        ]
        
        # Add optional context summarization
        if config.get("enable_context_summarization"):
            from .middleware import context_summarizer
            if context_summarizer:
                self._middleware.append(context_summarizer)
    
    def _build_graph(self) -> StateGraph:
        # ... existing graph building ...
        
        # Note: Middleware integration depends on how you're using
        # LangGraph/LangChain. For pure LangGraph StateGraph,
        # middleware is typically applied at the agent level.
        # See LangChain docs for integration patterns.
```

### Benefits

1. **Separation of concerns**: Logging/monitoring separate from business logic
2. **Consistency**: All nodes get same treatment automatically
3. **Extensibility**: Easy to add new cross-cutting features
4. **Cleaner nodes**: Business logic without logging boilerplate

### When to Use Middleware vs Explicit Calls

| Use Case | Middleware | Explicit |
|----------|------------|----------|
| Logging all node starts/ends | ✅ | |
| Performance monitoring | ✅ | |
| State validation | ✅ | |
| Task-specific logging | | ✅ |
| Conditional behavior | | ✅ |
| Complex error handling | | ✅ |

---

## Summary: Implementation Priority

### High Priority (Add to existing plans)

1. **Plan 1, Task 1.3.1**: Checkpoint retention and cleanup
2. **Plan 2, Amendment 2.A**: Pre-interrupt idempotency guidance (critical!)
3. **Plan 3, Amendment 3.A**: @task deployment warning and fallbacks

### Medium Priority (Can be added incrementally)

4. **Plan 2, Task 2.5**: Input validation loop pattern
5. **Plan 2, Amendment 2.D**: Command-based routing (cleaner code)
6. **All plans**: Basic middleware for logging (optional)

### Verification Additions

Add these to existing verification checklists:

**Plan 1:**
- [ ] Old checkpoints are automatically cleaned up
- [ ] `checkpoint-stats` command works
- [ ] Database size stays bounded

**Plan 2:**
- [ ] No side effects before `interrupt()` calls
- [ ] Invalid human input triggers re-prompt
- [ ] Validation errors are clear and actionable

**Plan 3:**
- [ ] @task caching verified in deployment environment
- [ ] Fallback idempotency tracker works
- [ ] CLI operations not duplicated on resume
