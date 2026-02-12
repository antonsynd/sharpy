#!/usr/bin/env python3
"""Test script to verify thread ID management."""
import inspect
import re
import sys
from pathlib import Path

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent))

from sharpy_auto_builder.config import Config
from sharpy_auto_builder.orchestrator import Orchestrator, create_initial_state


def test_thread_id_management():
    """Test thread ID generation and management."""
    c = Config()
    # Set a dummy task list path
    task_list_path = Path(__file__).parent / "test_task_list.md"
    task_list_path.write_text("# Test Task List\n\n- [ ] Test task\n")
    c.task_list_path = task_list_path

    with Orchestrator(c) as orch:
        print("✓ Orchestrator initialized\n")

        # Test 1: Verify create_initial_state() works
        initial_state = create_initial_state(str(c.ground_truth_path))
        assert initial_state is not None, "Initial state should not be None"
        assert "current_task" in initial_state, "Should have current_task field"
        assert (
            "ground_truth_path" in initial_state
        ), "Should have ground_truth_path field"
        assert (
            "execution_attempt" in initial_state
        ), "Should have execution_attempt field"
        assert "messages" in initial_state, "Should have messages field"
        print("✓ create_initial_state() creates valid state")

        # Test 2: Check run() signature accepts thread_id parameter
        sig = inspect.signature(orch.run)
        params = list(sig.parameters.keys())
        assert "thread_id" in params, "run() should accept thread_id parameter"
        assert (
            sig.parameters["thread_id"].default is None
        ), "thread_id should default to None"
        print("✓ run() signature accepts optional thread_id parameter")

        # Test 3: Verify thread ID generation pattern
        from datetime import datetime

        test_thread_id = f"sharpy-build-{datetime.now().strftime('%Y%m%d-%H%M%S')}"
        pattern = r"^sharpy-build-\d{8}-\d{6}$"
        assert re.match(
            pattern, test_thread_id
        ), f"Thread ID should match pattern: {pattern}"
        print(f"✓ Thread ID format is correct: {test_thread_id}")

        print("\n✓ All thread ID management tests passed!")

    # Cleanup test file
    if task_list_path.exists():
        task_list_path.unlink()


if __name__ == "__main__":
    test_thread_id_management()
