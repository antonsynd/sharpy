#!/usr/bin/env python3
"""Test script to verify interrupt imports work correctly."""
import sys
from pathlib import Path

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent))

from sharpy_auto_builder.config import Config
from sharpy_auto_builder.orchestrator import Orchestrator


def test_interrupt_imports():
    """Test that interrupt and Command imports resolve correctly."""
    print("Testing interrupt imports...\n")

    # Test 1: Verify imports exist in module
    from langgraph.types import interrupt, Command

    print("✓ Direct import of interrupt and Command successful")

    # Test 2: Verify orchestrator module loads without error
    c = Config()
    task_list_path = Path(__file__).parent / "test_task_list.md"
    task_list_path.write_text("# Test Task List\n\n- [ ] Test task\n")
    c.task_list_path = task_list_path

    try:
        with Orchestrator(c) as orch:
            print("✓ Orchestrator module loaded successfully with new imports")

            # Test 3: Verify interrupt and Command are callable/usable
            assert callable(interrupt), "interrupt should be callable"
            assert Command is not None, "Command should be defined"
            print("✓ interrupt is callable")
            print("✓ Command type is defined")

        print("\n✓ All import tests passed!")
    finally:
        # Cleanup test file
        if task_list_path.exists():
            task_list_path.unlink()


if __name__ == "__main__":
    test_interrupt_imports()
