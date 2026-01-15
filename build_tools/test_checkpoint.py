#!/usr/bin/env python3
"""Test script to verify SqliteSaver integration."""
import sys
from pathlib import Path

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent))

from sharpy_auto_builder.config import Config
from sharpy_auto_builder.orchestrator import Orchestrator
import os


def test_checkpoint_integration():
    """Test that orchestrator initializes with SqliteSaver."""
    c = Config()
    # Set a dummy task list path (file doesn't need to exist for this test)
    # We'll create a minimal task list
    task_list_path = Path(__file__).parent / "test_task_list.md"
    task_list_path.write_text("# Test Task List\n\n- [ ] Test task\n")
    c.task_list_path = task_list_path

    # Create orchestrator with context manager
    with Orchestrator(c) as orch:
        print(f"✓ Checkpointer type: {type(orch.checkpointer).__name__}")
        print(f"✓ DB connection type: {type(orch._db_connection).__name__}")
        print(f"✓ DB path: {c.checkpoint_db_path}")
        print(f"✓ DB file exists: {os.path.exists(c.checkpoint_db_path)}")

        # Verify it's SqliteSaver, not MemorySaver
        assert (
            type(orch.checkpointer).__name__ == "SqliteSaver"
        ), "Should be SqliteSaver"
        assert os.path.exists(c.checkpoint_db_path), "Database file should exist"

    print("\n✓ Context manager cleanup successful")
    print("\nOK - All tests passed!")

    # Cleanup test file
    if task_list_path.exists():
        task_list_path.unlink()


if __name__ == "__main__":
    test_checkpoint_integration()
