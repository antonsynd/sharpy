#!/usr/bin/env python3
"""Test script to verify AsyncSqliteSaver integration."""
import asyncio
import os
import sys
from pathlib import Path

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent))

from sharpy_auto_builder.config import Config
from sharpy_auto_builder.orchestrator import Orchestrator


def test_checkpoint_integration():
    """Test that orchestrator initializes and sets up AsyncSqliteSaver."""
    c = Config()
    # Set a dummy task list path
    task_list_path = Path(__file__).parent / "test_task_list.md"
    task_list_path.write_text("# Test Task List\n\n- [ ] Test task\n")
    c.task_list_path = task_list_path

    # Sync context manager — checkpointer is None until async setup()
    with Orchestrator(c) as orch:
        assert orch.checkpointer is None, "Checkpointer should be None before async setup"
        print("✓ Checkpointer is None before setup()")

    # Async context manager — checkpointer gets initialized
    async def _test_async():
        async with Orchestrator(c) as orch:
            print(f"✓ Checkpointer type: {type(orch.checkpointer).__name__}")
            print(f"✓ Async DB connection type: {type(orch._async_db_conn).__name__}")
            print(f"✓ DB path: {c.checkpoint_db_path}")
            print(f"✓ DB file exists: {os.path.exists(c.checkpoint_db_path)}")

            assert (
                type(orch.checkpointer).__name__ == "AsyncSqliteSaver"
            ), "Should be AsyncSqliteSaver"
            assert os.path.exists(c.checkpoint_db_path), "Database file should exist"

    asyncio.run(_test_async())

    print("\n✓ Context manager cleanup successful")
    print("\nOK - All tests passed!")

    # Cleanup test file
    if task_list_path.exists():
        task_list_path.unlink()


if __name__ == "__main__":
    test_checkpoint_integration()
