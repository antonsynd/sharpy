# Implementation Plan 1: Switch to Durable Persistence

## Overview

**Goal:** Replace the in-memory `MemorySaver` checkpointer with `SqliteSaver` to enable true durable execution that survives process restarts, rate limiting pauses, and crashes.

**Priority:** High  
**Estimated Effort:** 2-4 hours  
**Risk Level:** Low (mostly configuration changes)

## Background

### Current State
The orchestrator in `build_tools/sharpy_auto_builder/orchestrator.py` currently uses:

```python
from langgraph.checkpoint.memory import MemorySaver
# ...
self.memory = MemorySaver()
self.app = self.graph.compile(checkpointer=self.memory)
```

### Problem
- `MemorySaver` stores checkpoints only in RAM
- When the process exits (rate limit, crash, Ctrl+C), all graph state is lost
- The `ground_truth.json` file provides partial persistence, but execution state (current node, attempt counters, validation results) is lost
- Users must manually track where execution stopped

### Solution
Switch to `SqliteSaver` which persists checkpoints to a SQLite database file, enabling:
- Resume from exact point of interruption
- Automatic recovery after crashes
- Time-travel debugging (see Implementation Plan 4)

## Reference Documentation

- [LangGraph Persistence Docs](https://docs.langchain.com/oss/python/langgraph/persistence)
- [Checkpointer Libraries](https://docs.langchain.com/oss/python/langgraph/persistence#checkpointer-libraries)
- [Durable Execution](https://docs.langchain.com/oss/python/langgraph/durable-execution)

## Files to Modify

| File | Changes |
|------|---------|
| `build_tools/sharpy_auto_builder/requirements.txt` | Add `langgraph-checkpoint-sqlite` |
| `build_tools/sharpy_auto_builder/config.py` | Add checkpoint database path property |
| `build_tools/sharpy_auto_builder/orchestrator.py` | Replace MemorySaver with SqliteSaver |
| `build_tools/sharpy_auto_builder/cli.py` (if exists) | Add `--resume` flag for resuming sessions |

## Task List

### Task 1.1: Add SqliteSaver Dependency
**File:** `build_tools/sharpy_auto_builder/requirements.txt`

Add the SQLite checkpointer package:

```
# Add this line to requirements.txt
langgraph-checkpoint-sqlite>=2.0.0
```

**Verification:**
```bash
cd build_tools/sharpy_auto_builder
source .venv/bin/activate
pip install -r requirements.txt
python -c "from langgraph.checkpoint.sqlite import SqliteSaver; print('OK')"
```

---

### Task 1.2: Add Checkpoint Path to Configuration
**File:** `build_tools/sharpy_auto_builder/config.py`

Add a property for the checkpoint database path. Locate the `Config` class and add:

```python
@dataclass
class Config(BaseConfig):
    # ... existing fields ...
    
    @property
    def checkpoint_db_path(self) -> Path:
        """Path to SQLite database for LangGraph checkpoints."""
        return self.state_dir / "orchestrator_checkpoints.db"
```

**Location:** Add this property near the other `@property` definitions (around line 80-100, near `ground_truth_path`, `execution_log_path`, etc.)

**Verification:**
```python
from sharpy_auto_builder.config import Config
config = Config()
print(config.checkpoint_db_path)
# Should print: .../build_tools/sharpy_auto_builder/state/orchestrator_checkpoints.db
```

---

### Task 1.3: Update Orchestrator to Use SqliteSaver
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

#### Step 1: Update imports

Find the import section (near the top of the file) and modify:

```python
# REMOVE this line:
from langgraph.checkpoint.memory import MemorySaver

# ADD these lines:
import sqlite3
from langgraph.checkpoint.sqlite import SqliteSaver
```

#### Step 2: Update `__init__` method

Find the `__init__` method of the `Orchestrator` class. Locate these lines:

```python
# FIND (around line 90-100):
self.graph = self._build_graph()
self.memory = MemorySaver()
self.app = self.graph.compile(checkpointer=self.memory)
```

Replace with:

```python
# REPLACE WITH:
self.graph = self._build_graph()

# Initialize SQLite checkpointer for durable persistence
# This allows resuming execution after process restart
self._db_connection = sqlite3.connect(
    str(self.config.checkpoint_db_path),
    check_same_thread=False  # Required for async usage
)
self.checkpointer = SqliteSaver(self._db_connection)

# Run setup on first use (creates tables if they don't exist)
# This is idempotent - safe to call multiple times
self.checkpointer.setup()

self.app = self.graph.compile(checkpointer=self.checkpointer)
```

#### Step 3: Add cleanup method

Add a new method to properly close the database connection:

```python
def close(self) -> None:
    """Clean up resources. Call when done with orchestrator."""
    if hasattr(self, '_db_connection') and self._db_connection:
        self._db_connection.close()
        self._db_connection = None

def __del__(self):
    """Destructor to ensure database connection is closed."""
    self.close()
```

#### Step 4: Add context manager support (optional but recommended)

Add these methods to support `with` statement usage:

```python
def __enter__(self):
    """Support context manager protocol."""
    return self

def __exit__(self, exc_type, exc_val, exc_tb):
    """Clean up on context exit."""
    self.close()
    return False  # Don't suppress exceptions
```

---

### Task 1.4: Add Thread ID Management
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

The thread ID is crucial for resuming sessions. Update the `run` method:

```python
async def run(self, max_tasks: Optional[int] = None, thread_id: Optional[str] = None) -> dict:
    """
    Run the orchestrator to process tasks.
    
    Args:
        max_tasks: Maximum number of tasks to process (None = unlimited)
        thread_id: Thread ID for checkpoint persistence. If None, generates a new one.
                   Pass the same thread_id to resume a previous session.
    
    Returns:
        Dictionary with execution results
    """
    # Generate or use provided thread ID
    if thread_id is None:
        thread_id = f"sharpy-build-{datetime.now().strftime('%Y%m%d-%H%M%S')}"
    
    self._current_thread_id = thread_id
    
    # Log the thread ID so users can resume later
    print(f"\n{'='*60}")
    print(f"Session Thread ID: {thread_id}")
    print(f"To resume this session later, use: --thread-id {thread_id}")
    print(f"{'='*60}\n")
    
    # ... rest of existing run() method ...
    
    config = {
        "configurable": {"thread_id": thread_id},
        "recursion_limit": 150,
    }
    
    # Check if we're resuming an existing session
    existing_state = self.app.get_state(config)
    if existing_state and existing_state.values:
        print(f"Resuming existing session from checkpoint...")
        initial_state = None  # Use None to resume from checkpoint
    else:
        print(f"Starting new session...")
        initial_state = self._create_initial_state()
    
    # ... continue with existing logic, but use initial_state which may be None ...
```

**Also add a helper method:**

```python
def _create_initial_state(self) -> OrchestratorState:
    """Create fresh initial state for new sessions."""
    return {
        "current_task": None,
        "ground_truth_path": str(self.config.ground_truth_path),
        "execution_attempt": 0,
        "fix_attempt": 0,
        "validation_fix_attempt": 0,
        "last_execution_result": None,
        "baseline_test_passed": None,
        "baseline_test_output": None,
        "validation_results": [],
        "awaiting_human_input": False,
        "human_question_id": None,
        "human_review_id": None,
        "human_response": None,
        "response_analysis": None,
        "auto_decision": None,
        "next_action": "",
        "error_message": None,
        "messages": [],
    }
```

---

### Task 1.5: Update CLI to Support Resume
**File:** `build_tools/auto_builder.sh` or `build_tools/sharpy_auto_builder/cli.py`

If using argparse in a Python CLI, add:

```python
parser.add_argument(
    '--thread-id',
    type=str,
    default=None,
    help='Thread ID to resume a previous session. If not provided, starts a new session.'
)

parser.add_argument(
    '--list-sessions',
    action='store_true',
    help='List all saved sessions that can be resumed'
)
```

Add a function to list resumable sessions:

```python
def list_sessions(config: Config) -> list[dict]:
    """List all saved checkpoint sessions."""
    import sqlite3
    
    db_path = config.checkpoint_db_path
    if not db_path.exists():
        return []
    
    conn = sqlite3.connect(str(db_path))
    cursor = conn.cursor()
    
    # Query unique thread IDs with their latest checkpoint
    cursor.execute("""
        SELECT DISTINCT thread_id, MAX(checkpoint_id), MAX(created_at)
        FROM checkpoints
        GROUP BY thread_id
        ORDER BY MAX(created_at) DESC
    """)
    
    sessions = []
    for row in cursor.fetchall():
        sessions.append({
            "thread_id": row[0],
            "latest_checkpoint": row[1],
            "last_updated": row[2],
        })
    
    conn.close()
    return sessions
```

---

### Task 1.6: Add Recovery From Rate Limit
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

Update the `_handle_error_node` to leverage checkpointing:

```python
async def _handle_error_node(self, state: OrchestratorState) -> OrchestratorState:
    """Handle errors during execution."""
    # ... existing code ...
    
    if is_rate_limited and self.config.rate_limit_pause_hours > 0:
        # Log resume instructions
        print(f"\n{'='*60}")
        print(f"RATE LIMITED - Session checkpointed")
        print(f"{'='*60}")
        print(f"All backends rate-limited. Session state has been saved.")
        print(f"")
        print(f"To resume later, run:")
        print(f"  ./auto_builder.sh run --thread-id {self._current_thread_id}")
        print(f"")
        print(f"Estimated wait time: {self.config.rate_limit_pause_hours} hours")
        print(f"{'='*60}\n")
        
        # Return state that will trigger graph to stop
        # The checkpoint is automatically saved by LangGraph
        return {
            **state,
            "next_action": "pause_rate_limited",
            "error_message": f"Rate limited. Resume with thread_id: {self._current_thread_id}",
            "messages": ["Session paused due to rate limiting"],
        }
    
    # ... rest of existing error handling ...
```

---

### Task 1.7: Write Tests
**File:** `build_tools/tests/test_orchestrator_persistence.py` (new file)

```python
"""Tests for orchestrator checkpoint persistence."""

import pytest
import sqlite3
import tempfile
from pathlib import Path
from unittest.mock import MagicMock, patch

from sharpy_auto_builder.config import Config
from sharpy_auto_builder.orchestrator import Orchestrator


class TestCheckpointPersistence:
    """Test that checkpoints are properly persisted."""
    
    @pytest.fixture
    def temp_config(self, tmp_path):
        """Create config with temporary paths."""
        config = Config()
        config._state_dir_override = tmp_path / "state"
        config._state_dir_override.mkdir(parents=True)
        return config
    
    def test_checkpoint_db_created(self, temp_config):
        """Test that checkpoint database is created on init."""
        with Orchestrator(temp_config) as orch:
            assert temp_config.checkpoint_db_path.exists()
    
    def test_checkpoint_tables_created(self, temp_config):
        """Test that required tables are created."""
        with Orchestrator(temp_config) as orch:
            conn = sqlite3.connect(str(temp_config.checkpoint_db_path))
            cursor = conn.cursor()
            cursor.execute("SELECT name FROM sqlite_master WHERE type='table'")
            tables = {row[0] for row in cursor.fetchall()}
            conn.close()
            
            # SqliteSaver creates these tables
            assert "checkpoints" in tables or "checkpoint_blobs" in tables
    
    def test_session_can_be_resumed(self, temp_config):
        """Test that a session can be resumed with thread_id."""
        thread_id = "test-session-123"
        
        # Start a session
        with Orchestrator(temp_config) as orch:
            # Mock the graph execution to avoid actual CLI calls
            with patch.object(orch, '_execute_implementation_node'):
                # This would normally run the graph
                pass
        
        # Create new orchestrator and resume
        with Orchestrator(temp_config) as orch2:
            state = orch2.app.get_state(
                {"configurable": {"thread_id": thread_id}}
            )
            # State should be retrievable (may be None if nothing was checkpointed)
            # The key test is that no exception is raised


class TestThreadIdManagement:
    """Test thread ID generation and management."""
    
    def test_thread_id_generated_if_not_provided(self, temp_config):
        """Test that thread_id is auto-generated."""
        with Orchestrator(temp_config) as orch:
            # Would need to mock run() to test this fully
            pass
    
    def test_thread_id_used_when_provided(self, temp_config):
        """Test that provided thread_id is used."""
        pass  # Implementation test


class TestCleanup:
    """Test resource cleanup."""
    
    def test_context_manager_closes_connection(self, temp_config):
        """Test that __exit__ closes database connection."""
        orch = Orchestrator(temp_config)
        assert orch._db_connection is not None
        
        orch.close()
        # Connection should be None after close
        assert orch._db_connection is None
    
    def test_with_statement_cleanup(self, temp_config):
        """Test cleanup via with statement."""
        with Orchestrator(temp_config) as orch:
            conn = orch._db_connection
            assert conn is not None
        
        # After exiting, connection should be closed
        assert orch._db_connection is None
```

---

## Verification Checklist

After completing all tasks, verify:

- [ ] `langgraph-checkpoint-sqlite` is installed
- [ ] `orchestrator_checkpoints.db` is created in `state/` directory on first run
- [ ] Running with `--thread-id` resumes from checkpoint
- [ ] Rate limit pause shows resume instructions
- [ ] `--list-sessions` shows previous sessions
- [ ] All existing tests still pass
- [ ] New persistence tests pass

## Rollback Plan

If issues arise, rollback is simple:

1. Revert imports to `MemorySaver`
2. Remove SQLite connection code
3. Delete the checkpoint database file

The checkpoint database is independent of `ground_truth.json`, so no data migration is needed.

## Next Steps

After this implementation:
- Proceed to **Implementation Plan 2** (Native Interrupts) which builds on checkpointing
- Consider **Implementation Plan 4** (Time Travel) which uses checkpoint history

## Cross-References

- **Current file:** `build_tools/sharpy_auto_builder/orchestrator.py`
- **Config file:** `build_tools/sharpy_auto_builder/config.py`
- **Related:** `build_tools/sharpy_auto_builder/state.py` (GroundTruth persistence - separate from checkpoints)
