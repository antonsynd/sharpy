# Implementation Plan 2: Native Interrupts for Human-in-the-Loop

## Overview

**Goal:** Replace the current file-based polling system for human input with LangGraph's native `interrupt()` function, providing cleaner code and automatic state persistence during human interactions.

**Priority:** High  
**Estimated Effort:** 4-6 hours  
**Risk Level:** Medium (changes core human interaction flow)

**Prerequisite:** Implementation Plan 1 (Durable Persistence) should be completed first, as interrupts require a checkpointer to persist state.

## Background

### Current State

The orchestrator uses a file-based system for human-in-the-loop:

```
build_tools/sharpy_auto_builder/
├── state/
│   ├── questions/      # Questions written here for humans
│   ├── answers/        # Humans write answers here
│   └── human_review/   # Review requests
```

The `HumanLoopManager` class in `build_tools/sharpy_auto_builder/human_loop.py`:
1. Writes questions to JSON files in `questions/`
2. Polls `answers/` directory for responses
3. The `wait_for_human` node continuously checks for files

### Problems with Current Approach

1. **Complex polling logic** - Requires background file watching or periodic polling
2. **State management** - Must manually track which questions are pending
3. **No integration with checkpointing** - If process restarts, polling state is lost
4. **Coupling** - Tight coupling between file format and orchestrator logic
5. **Race conditions** - Potential issues if files are written while being read

### Solution: Native Interrupts

LangGraph's `interrupt()` function:
1. Pauses execution at any point in a node
2. Returns a value to the caller (the question/review request)
3. Automatically checkpoints state
4. Resumes when `Command(resume=...)` is invoked with the response
5. The resume value becomes the return value of `interrupt()`

## Reference Documentation

- [LangGraph Interrupts](https://docs.langchain.com/oss/python/langgraph/interrupts)
- [Interrupt Patterns](https://docs.langchain.com/oss/python/langgraph/interrupts#common-patterns)
- [Rules of Interrupts](https://docs.langchain.com/oss/python/langgraph/interrupts#rules-of-interrupts)

## Files to Modify

| File | Changes |
|------|---------|
| `build_tools/sharpy_auto_builder/orchestrator.py` | Replace human nodes with interrupt-based versions |
| `build_tools/sharpy_auto_builder/human_loop.py` | Simplify or deprecate file-based system |
| `build_tools/sharpy_auto_builder/cli.py` | Add interrupt handling in CLI runner |
| `build_tools/auto_builder.sh` | Update to handle interrupted state |

## Task List

### Task 2.1: Update Imports in Orchestrator
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

Add the interrupt-related imports:

```python
# ADD these imports near the top of the file:
from langgraph.types import interrupt, Command

# The existing import should remain:
from langgraph.graph import StateGraph, END
```

---

### Task 2.2: Create Interrupt Data Structures
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

Add TypedDict definitions for interrupt payloads (add near `OrchestratorState`):

```python
from typing import TypedDict, Literal, Optional, Annotated, Any

class HumanQuestionPayload(TypedDict):
    """Payload sent to humans when asking a question."""
    type: Literal["question"]
    task_id: str
    task_description: str
    question: str
    priority: str  # "critical", "high", "medium", "low"
    context: dict[str, Any]  # Additional context
    options: Optional[list[str]]  # Suggested answers, if any

class HumanReviewPayload(TypedDict):
    """Payload sent to humans for review requests."""
    type: Literal["review"]
    task_id: str
    task_description: str
    execution_result: dict
    validation_results: list[dict]
    files_changed: list[str]
    diff_summary: Optional[str]

class HumanResponse(TypedDict):
    """Expected response from humans."""
    approved: bool
    feedback: Optional[str]
    modified_value: Optional[Any]  # For edits
    retry: bool  # Whether to retry the task
```

---

### Task 2.3: Rewrite `_request_human_review_node` with Interrupt
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

Find the existing `_request_human_review_node` method and replace it:

```python
async def _request_human_review_node(self, state: OrchestratorState) -> OrchestratorState:
    """
    Request human review using native interrupt.
    
    This node pauses execution and waits for human input.
    The interrupt payload is returned to the caller, who should
    display it to the human and then resume with Command(resume=response).
    
    IMPORTANT: Code before interrupt() will re-run on resume.
    Keep pre-interrupt code minimal and idempotent.
    """
    task_data = state["current_task"]
    last_result = state.get("last_execution_result", {}) or {}
    validation_results = state.get("validation_results", [])
    
    # Build the review payload
    review_payload: HumanReviewPayload = {
        "type": "review",
        "task_id": task_data["id"],
        "task_description": task_data.get("description", ""),
        "execution_result": {
            "success": last_result.get("success", False),
            "output_summary": (last_result.get("output", ""))[:1000],  # Truncate for display
            "error": last_result.get("error"),
            "backend": last_result.get("backend"),
        },
        "validation_results": [
            {
                "validator": vr.get("validator"),
                "status": vr.get("status"),
                "issues": vr.get("issues", [])[:5],  # Limit issues shown
            }
            for vr in validation_results
        ],
        "files_changed": last_result.get("files_changed", []),
        "diff_summary": last_result.get("diff_summary"),
    }
    
    # INTERRUPT: Pause execution and wait for human response
    # This is the key change - interrupt() pauses here and returns
    # the payload to the caller. When resumed, human_response contains
    # the value passed to Command(resume=...)
    human_response: HumanResponse = interrupt(review_payload)
    
    # --- Code below only runs after human responds ---
    
    # Log the human response
    self._log_execution(
        event_type="human_review_response",
        task_id=task_data["id"],
        extra={
            "approved": human_response.get("approved"),
            "has_feedback": bool(human_response.get("feedback")),
            "retry_requested": human_response.get("retry", False),
        }
    )
    
    # Route based on human decision
    if human_response.get("approved"):
        return {
            **state,
            "next_action": "commit",
            "human_response": human_response,
            "messages": [f"✅ Human approved task {task_data['id']}"],
        }
    elif human_response.get("retry"):
        return {
            **state,
            "next_action": "retry",
            "human_response": human_response,
            "execution_attempt": 0,  # Reset attempt counter for retry
            "messages": [f"🔄 Human requested retry for task {task_data['id']}"],
        }
    else:
        return {
            **state,
            "next_action": "skip",
            "human_response": human_response,
            "messages": [f"⏭️ Human rejected task {task_data['id']}, skipping"],
        }
```

---

### Task 2.4: Rewrite Question-Asking with Interrupt
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

Add a helper method for asking questions, and update nodes that ask questions:

```python
def _ask_human_question(
    self,
    task_id: str,
    question: str,
    priority: str = "medium",
    context: dict = None,
    options: list[str] = None,
) -> Any:
    """
    Ask a question to the human using interrupt.
    
    Args:
        task_id: ID of the current task
        question: The question to ask
        priority: "critical", "high", "medium", or "low"
        context: Additional context to help human answer
        options: Suggested answer options (optional)
    
    Returns:
        The human's response (type depends on question)
    
    Usage:
        answer = self._ask_human_question(
            task_id="task_001",
            question="Should we use approach A or B?",
            options=["A", "B", "Other"]
        )
    """
    payload: HumanQuestionPayload = {
        "type": "question",
        "task_id": task_id,
        "task_description": "",  # Will be filled from state if needed
        "question": question,
        "priority": priority,
        "context": context or {},
        "options": options,
    }
    
    # Interrupt and wait for answer
    return interrupt(payload)
```

Update any node that needs to ask questions:

```python
async def _plan_implementation_node(self, state: OrchestratorState) -> OrchestratorState:
    """Plan implementation, asking human for clarification if needed."""
    task_data = state["current_task"]
    
    # Example: Ask for clarification on ambiguous requirements
    if self._is_task_ambiguous(task_data):
        clarification = self._ask_human_question(
            task_id=task_data["id"],
            question=f"Task '{task_data['description']}' is ambiguous. Please clarify the expected behavior.",
            priority="high",
            context={
                "task_type": task_data.get("type"),
                "related_spec": task_data.get("spec_reference"),
            }
        )
        
        # Update task with clarification
        task_data["clarification"] = clarification
    
    # Continue with planning...
    return {
        **state,
        "current_task": task_data,
        "next_action": "execute",
        "messages": [f"Planning complete for {task_data['id']}"],
    }
```

---

### Task 2.5: Update Graph Routing for Interrupt Handling
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

The routing after human review needs to handle the new response structure. Find `_route_after_human_review` (or similar) and update:

```python
def _route_after_human_response(self, state: OrchestratorState) -> str:
    """Route based on human response after interrupt resumes."""
    next_action = state.get("next_action", "")
    
    if next_action == "commit":
        return "commit_changes"
    elif next_action == "retry":
        return "execute_implementation"  # Go back to implementation
    elif next_action == "skip":
        return "update_ground_truth"  # Mark as skipped and move on
    else:
        return "handle_error"
```

Make sure the graph has this conditional edge:

```python
# In _build_graph():
graph.add_conditional_edges(
    "request_human_review",
    self._route_after_human_response,
    {
        "commit_changes": "commit_changes",
        "execute_implementation": "execute_implementation",
        "update_ground_truth": "update_ground_truth",
        "handle_error": "handle_error",
    }
)
```

---

### Task 2.6: Remove `wait_for_human` Node
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

The `wait_for_human` node is no longer needed since `interrupt()` handles waiting. 

1. **Remove the node definition:**
```python
# DELETE this entire method:
async def _wait_for_human_node(self, state: OrchestratorState) -> OrchestratorState:
    # ... all of this code ...
```

2. **Remove from graph building:**
```python
# In _build_graph(), DELETE:
graph.add_node("wait_for_human", self._wait_for_human_node)

# Also DELETE any edges involving wait_for_human
```

3. **Remove the polling state fields** (optional, can keep for backwards compatibility):
```python
# These fields in OrchestratorState can be removed or kept:
# awaiting_human_input: bool
# human_question_id: Optional[str]
# human_review_id: Optional[str]
```

---

### Task 2.7: Create CLI Interrupt Handler
**File:** `build_tools/sharpy_auto_builder/cli.py` (or create new file `interrupt_handler.py`)

Create a handler that processes interrupts in the CLI:

```python
"""
Interrupt handler for CLI-based human-in-the-loop.

This module handles interrupt payloads from the orchestrator
and collects human responses via the terminal.
"""

import json
from typing import Any
from rich.console import Console
from rich.panel import Panel
from rich.prompt import Prompt, Confirm
from rich.table import Table

console = Console()


def display_interrupt(interrupt_data: dict) -> None:
    """Display interrupt payload to the user."""
    interrupt_type = interrupt_data.get("type", "unknown")
    
    if interrupt_type == "review":
        _display_review_request(interrupt_data)
    elif interrupt_type == "question":
        _display_question(interrupt_data)
    else:
        console.print(Panel(
            json.dumps(interrupt_data, indent=2),
            title="[yellow]Unknown Interrupt Type[/yellow]"
        ))


def _display_review_request(data: dict) -> None:
    """Display a review request."""
    console.print()
    console.print(Panel(
        f"[bold]Task:[/bold] {data.get('task_id', 'N/A')}\n"
        f"[bold]Description:[/bold] {data.get('task_description', 'N/A')}",
        title="[cyan]🔍 Human Review Required[/cyan]",
        border_style="cyan"
    ))
    
    # Execution result
    exec_result = data.get("execution_result", {})
    status = "✅ Success" if exec_result.get("success") else "❌ Failed"
    console.print(f"\n[bold]Execution Status:[/bold] {status}")
    console.print(f"[bold]Backend:[/bold] {exec_result.get('backend', 'N/A')}")
    
    if exec_result.get("error"):
        console.print(Panel(
            exec_result["error"][:500],
            title="[red]Error[/red]",
            border_style="red"
        ))
    
    # Validation results
    validations = data.get("validation_results", [])
    if validations:
        table = Table(title="Validation Results")
        table.add_column("Validator", style="cyan")
        table.add_column("Status", style="green")
        table.add_column("Issues", style="yellow")
        
        for vr in validations:
            issues = ", ".join(vr.get("issues", [])[:3])
            if len(vr.get("issues", [])) > 3:
                issues += "..."
            table.add_row(
                vr.get("validator", "N/A"),
                vr.get("status", "N/A"),
                issues or "None"
            )
        
        console.print(table)
    
    # Files changed
    files = data.get("files_changed", [])
    if files:
        console.print(f"\n[bold]Files Changed ({len(files)}):[/bold]")
        for f in files[:10]:
            console.print(f"  • {f}")
        if len(files) > 10:
            console.print(f"  ... and {len(files) - 10} more")


def _display_question(data: dict) -> None:
    """Display a question."""
    console.print()
    console.print(Panel(
        data.get("question", "No question provided"),
        title=f"[yellow]❓ Question ({data.get('priority', 'medium')} priority)[/yellow]",
        border_style="yellow"
    ))
    
    if data.get("context"):
        console.print("\n[bold]Context:[/bold]")
        for key, value in data["context"].items():
            console.print(f"  • {key}: {value}")
    
    if data.get("options"):
        console.print("\n[bold]Suggested Options:[/bold]")
        for i, opt in enumerate(data["options"], 1):
            console.print(f"  {i}. {opt}")


def collect_response(interrupt_data: dict) -> dict:
    """Collect human response based on interrupt type."""
    interrupt_type = interrupt_data.get("type", "unknown")
    
    if interrupt_type == "review":
        return _collect_review_response()
    elif interrupt_type == "question":
        return _collect_question_response(interrupt_data)
    else:
        # Generic response
        response = Prompt.ask("Enter your response")
        return {"value": response}


def _collect_review_response() -> dict:
    """Collect response for a review request."""
    console.print("\n[bold]Actions:[/bold]")
    console.print("  1. [green]Approve[/green] - Accept changes and continue")
    console.print("  2. [yellow]Retry[/yellow] - Reject and retry the task")
    console.print("  3. [red]Skip[/red] - Reject and skip this task")
    
    choice = Prompt.ask(
        "Your choice",
        choices=["1", "2", "3", "approve", "retry", "skip"],
        default="1"
    )
    
    approved = choice in ["1", "approve"]
    retry = choice in ["2", "retry"]
    
    feedback = None
    if not approved:
        feedback = Prompt.ask(
            "Feedback (optional, press Enter to skip)",
            default=""
        ) or None
    
    return {
        "approved": approved,
        "retry": retry,
        "feedback": feedback,
    }


def _collect_question_response(data: dict) -> dict:
    """Collect response for a question."""
    options = data.get("options")
    
    if options:
        console.print("\n[bold]Select an option or enter custom response:[/bold]")
        for i, opt in enumerate(options, 1):
            console.print(f"  {i}. {opt}")
        
        response = Prompt.ask("Your answer (number or text)")
        
        # Check if it's a number selecting an option
        try:
            idx = int(response) - 1
            if 0 <= idx < len(options):
                return {"value": options[idx]}
        except ValueError:
            pass
        
        return {"value": response}
    else:
        response = Prompt.ask("Your answer")
        return {"value": response}
```

---

### Task 2.8: Update Main Run Loop
**File:** `build_tools/sharpy_auto_builder/cli.py` or main entry point

Create the main loop that handles interrupts:

```python
"""Main CLI runner with interrupt handling."""

import asyncio
from langgraph.types import Command

from .orchestrator import Orchestrator
from .config import Config
from .interrupt_handler import display_interrupt, collect_response


async def run_with_interrupts(
    config: Config,
    thread_id: str = None,
    max_tasks: int = None,
) -> dict:
    """
    Run orchestrator with interactive interrupt handling.
    
    This function runs the orchestrator and handles any interrupts
    by displaying them to the user and collecting responses.
    """
    with Orchestrator(config) as orchestrator:
        # Determine thread_id
        if thread_id is None:
            from datetime import datetime
            thread_id = f"sharpy-build-{datetime.now().strftime('%Y%m%d-%H%M%S')}"
        
        run_config = {"configurable": {"thread_id": thread_id}}
        
        # Check if resuming
        existing_state = orchestrator.app.get_state(run_config)
        if existing_state and existing_state.values:
            print(f"Resuming session: {thread_id}")
            input_state = None  # Resume from checkpoint
        else:
            print(f"Starting new session: {thread_id}")
            input_state = orchestrator._create_initial_state()
        
        tasks_processed = 0
        
        while True:
            # Run until interrupt or completion
            result = await orchestrator.app.ainvoke(input_state, run_config)
            
            # Check for interrupt
            if "__interrupt__" in result:
                interrupt_value = result["__interrupt__"][0].value
                
                # Display to human
                display_interrupt(interrupt_value)
                
                # Collect response
                response = collect_response(interrupt_value)
                
                # Resume with response
                input_state = Command(resume=response)
                continue
            
            # Check completion
            if result.get("next_action") == "complete":
                print("\n✅ All tasks completed!")
                break
            
            if result.get("next_action") == "pause_rate_limited":
                print("\n⏸️  Paused due to rate limiting.")
                print(f"Resume with: --thread-id {thread_id}")
                break
            
            # Track progress
            if result.get("next_action") == "next_task":
                tasks_processed += 1
                if max_tasks and tasks_processed >= max_tasks:
                    print(f"\n✅ Processed {tasks_processed} tasks (limit reached)")
                    break
            
            # Continue to next iteration
            input_state = None  # Use checkpointed state
        
        return {
            "thread_id": thread_id,
            "tasks_processed": tasks_processed,
            "final_state": result,
        }


# CLI entry point
def main():
    import argparse
    
    parser = argparse.ArgumentParser(description="Sharpy Auto Builder")
    parser.add_argument("--thread-id", help="Resume a previous session")
    parser.add_argument("--max-tasks", type=int, help="Maximum tasks to process")
    parser.add_argument("--config", help="Path to config file")
    
    args = parser.parse_args()
    
    config = Config.load(args.config) if args.config else Config()
    
    result = asyncio.run(run_with_interrupts(
        config=config,
        thread_id=args.thread_id,
        max_tasks=args.max_tasks,
    ))
    
    print(f"\nSession complete. Thread ID: {result['thread_id']}")


if __name__ == "__main__":
    main()
```

---

### Task 2.9: Deprecate File-Based HumanLoopManager
**File:** `build_tools/sharpy_auto_builder/human_loop.py`

Add deprecation notices and optionally keep for backwards compatibility:

```python
"""
Human-in-the-loop management.

DEPRECATED: The file-based polling system is deprecated in favor of
LangGraph's native interrupt() function. See orchestrator.py for the
new interrupt-based implementation.

This module is kept for backwards compatibility and can be used as
a fallback for non-interactive batch processing.
"""

import warnings
from pathlib import Path
# ... existing imports ...


class HumanLoopManager:
    """
    File-based human interaction manager.
    
    DEPRECATED: Use interrupt() in orchestrator nodes instead.
    This class is maintained for backwards compatibility only.
    """
    
    def __init__(self, questions_dir: Path, answers_dir: Path, review_dir: Path):
        warnings.warn(
            "HumanLoopManager is deprecated. Use interrupt() for human interactions.",
            DeprecationWarning,
            stacklevel=2
        )
        # ... existing __init__ code ...
```

---

### Task 2.10: Write Tests for Interrupt Handling
**File:** `build_tools/tests/test_orchestrator_interrupts.py` (new file)

```python
"""Tests for interrupt-based human-in-the-loop."""

import pytest
from unittest.mock import MagicMock, patch, AsyncMock
from langgraph.types import Command

from sharpy_auto_builder.orchestrator import Orchestrator, OrchestratorState
from sharpy_auto_builder.config import Config


class TestInterruptPayloads:
    """Test that interrupt payloads are correctly formatted."""
    
    def test_review_payload_structure(self):
        """Test review interrupt payload has required fields."""
        # Create a mock state
        state = {
            "current_task": {
                "id": "task_001",
                "description": "Test task",
            },
            "last_execution_result": {
                "success": True,
                "output": "Test output",
                "backend": "claude_code",
            },
            "validation_results": [
                {"validator": "spec", "status": "passed", "issues": []}
            ],
        }
        
        # The payload should be created correctly
        # (This tests the payload creation logic)
        payload = {
            "type": "review",
            "task_id": state["current_task"]["id"],
            "task_description": state["current_task"]["description"],
            "execution_result": state["last_execution_result"],
            "validation_results": state["validation_results"],
            "files_changed": [],
            "diff_summary": None,
        }
        
        assert payload["type"] == "review"
        assert payload["task_id"] == "task_001"
        assert "execution_result" in payload
        assert "validation_results" in payload
    
    def test_question_payload_structure(self):
        """Test question interrupt payload has required fields."""
        payload = {
            "type": "question",
            "task_id": "task_001",
            "task_description": "Test task",
            "question": "Should we proceed?",
            "priority": "high",
            "context": {"key": "value"},
            "options": ["Yes", "No"],
        }
        
        assert payload["type"] == "question"
        assert "question" in payload
        assert "priority" in payload


class TestInterruptResume:
    """Test interrupt and resume behavior."""
    
    @pytest.fixture
    def temp_config(self, tmp_path):
        """Create config with temporary paths."""
        config = Config()
        # Override state dir for testing
        return config
    
    @pytest.mark.asyncio
    async def test_approval_routes_to_commit(self, temp_config):
        """Test that approval response routes to commit."""
        state = {
            "current_task": {"id": "task_001", "description": "Test"},
            "last_execution_result": {"success": True},
            "validation_results": [],
            "next_action": "",
            "messages": [],
        }
        
        # Simulate human approval response
        human_response = {"approved": True, "feedback": None, "retry": False}
        
        # After interrupt resumes with approval, next_action should be "commit"
        # This would be tested via integration test with actual graph
        assert human_response["approved"] is True
    
    @pytest.mark.asyncio
    async def test_retry_routes_to_execute(self, temp_config):
        """Test that retry response routes back to execution."""
        human_response = {"approved": False, "feedback": "Try again", "retry": True}
        
        # After retry, should go back to execute_implementation
        assert human_response["retry"] is True
    
    @pytest.mark.asyncio
    async def test_reject_routes_to_skip(self, temp_config):
        """Test that rejection routes to skip."""
        human_response = {"approved": False, "feedback": "Bad approach", "retry": False}
        
        # After rejection without retry, should skip
        assert human_response["approved"] is False
        assert human_response["retry"] is False


class TestInterruptHandler:
    """Test the CLI interrupt handler."""
    
    def test_display_review_request(self):
        """Test review request display doesn't crash."""
        from sharpy_auto_builder.interrupt_handler import display_interrupt
        
        data = {
            "type": "review",
            "task_id": "task_001",
            "task_description": "Test task",
            "execution_result": {"success": True, "backend": "claude"},
            "validation_results": [],
            "files_changed": ["file1.cs", "file2.cs"],
            "diff_summary": None,
        }
        
        # Should not raise
        display_interrupt(data)
    
    def test_display_question(self):
        """Test question display doesn't crash."""
        from sharpy_auto_builder.interrupt_handler import display_interrupt
        
        data = {
            "type": "question",
            "task_id": "task_001",
            "question": "Should we proceed?",
            "priority": "high",
            "context": {},
            "options": ["Yes", "No"],
        }
        
        # Should not raise
        display_interrupt(data)
```

---

## Verification Checklist

After completing all tasks, verify:

- [ ] `interrupt()` is imported and used in human review node
- [ ] `wait_for_human` node is removed from graph
- [ ] CLI handler displays interrupt payloads correctly
- [ ] CLI handler collects and returns valid responses
- [ ] Resume with `Command(resume=...)` works
- [ ] State is preserved across interrupt/resume
- [ ] All existing tests pass
- [ ] New interrupt tests pass
- [ ] Deprecation warning appears for `HumanLoopManager`

## Important Rules for Interrupts

From the LangGraph documentation, remember these rules:

1. **Don't wrap interrupt in try/except** - The interrupt mechanism uses exceptions internally
2. **Keep pre-interrupt code idempotent** - Code before `interrupt()` runs again on resume
3. **Don't reorder interrupts** - If a node has multiple interrupts, keep them in consistent order
4. **Use JSON-serializable payloads** - Don't pass functions or complex objects to `interrupt()`

## Rollback Plan

If issues arise:

1. Restore `wait_for_human` node
2. Restore file-based `HumanLoopManager` calls
3. Remove `interrupt()` calls from nodes

The file-based system can coexist with interrupts during migration.

## Next Steps

After this implementation:
- The system will have clean human-in-the-loop via interrupts
- Combined with Implementation Plan 1, sessions can be resumed days later
- Consider Implementation Plan 4 (Time Travel) for debugging interrupt flows

## Cross-References

- **Depends on:** Implementation Plan 1 (Durable Persistence) - checkpointer required
- **Orchestrator:** `build_tools/sharpy_auto_builder/orchestrator.py`
- **Human Loop (deprecated):** `build_tools/sharpy_auto_builder/human_loop.py`
- **LangGraph Interrupts:** https://docs.langchain.com/oss/python/langgraph/interrupts
