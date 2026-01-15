#!/usr/bin/env python3
"""Test script to verify rate limit recovery functionality."""
import sys
from pathlib import Path

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent))

from sharpy_auto_builder.config import Config
from sharpy_auto_builder.orchestrator import Orchestrator


def test_rate_limit_detection():
    """Test rate limit detection and session pause."""
    c = Config()
    # Set a dummy task list path
    task_list_path = Path(__file__).parent / "test_task_list.md"
    task_list_path.write_text("# Test Task List\n\n- [ ] Test task\n")
    c.task_list_path = task_list_path

    with Orchestrator(c) as orch:
        print("✓ Orchestrator initialized\n")

        # Test 1: Verify _handle_error_node exists
        assert hasattr(orch, "_handle_error_node"), "Should have _handle_error_node method"
        print("✓ _handle_error_node method exists")

        # Test 2: Verify graph routes pause_rate_limited to END
        import inspect

        graph_method = inspect.getsource(orch._build_graph)
        assert (
            "pause_rate_limited" in graph_method
        ), "Graph should include pause_rate_limited routing"
        print("✓ Graph includes pause_rate_limited routing")

        # Test 3: Simulate rate limit error state
        test_state = {
            "current_task": {"id": "test_task", "description": "Test"},
            "execution_attempt": 1,
            "last_execution_result": {"error": "Rate limit exhausted"},
            "messages": [],
        }

        # Mock thread_id for test
        orch._current_thread_id = "test-thread-12345"

        import asyncio

        # Run the error handler
        result = asyncio.run(orch._handle_error_node(test_state))

        # Verify the result
        assert result is not None, "Should return state"
        assert (
            result.get("next_action") == "pause_rate_limited"
        ), f"Should set next_action to pause_rate_limited, got {result.get('next_action')}"
        assert result.get("execution_attempt") == 0, "Should reset execution_attempt"
        assert any(
            "rate limiting" in msg.lower() for msg in result.get("messages", [])
        ), "Should include rate limiting message"

        print("✓ Rate limit detection sets next_action to pause_rate_limited")
        print("✓ Execution attempt is reset to 0")
        print("✓ Messages include rate limiting notification")

        print("\n✓ All rate limit recovery tests passed!")

    # Cleanup test file
    if task_list_path.exists():
        task_list_path.unlink()


if __name__ == "__main__":
    test_rate_limit_detection()
