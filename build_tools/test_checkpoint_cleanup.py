#!/usr/bin/env python3
"""Test script to verify checkpoint cleanup functionality."""
import sys
from pathlib import Path

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent))

from sharpy_auto_builder.config import Config
from sharpy_auto_builder.orchestrator import Orchestrator


def test_checkpoint_cleanup():
    """Test checkpoint cleanup tracking and statistics."""
    c = Config()
    # Set a dummy task list path
    task_list_path = Path(__file__).parent / "test_task_list.md"
    task_list_path.write_text("# Test Task List\n\n- [ ] Test task\n")
    c.task_list_path = task_list_path

    # Create orchestrator with context manager
    with Orchestrator(c) as orch:
        print("✓ Orchestrator initialized")

        # Check cleanup tracking is initialized
        assert hasattr(orch, "_checkpoint_count"), "Should have _checkpoint_count"
        assert hasattr(orch, "_cleanup_interval"), "Should have _cleanup_interval"
        assert orch._checkpoint_count == 0, "Initial count should be 0"
        assert (
            orch._cleanup_interval == c.checkpoint.cleanup_interval
        ), "Should match config"
        print(f"✓ Cleanup tracking initialized: interval={orch._cleanup_interval}")

        # Get checkpoint stats
        stats = orch.get_checkpoint_stats()
        print(f"\n✓ Checkpoint statistics:")
        print(f"  - Total checkpoints: {stats.get('total_checkpoints', 0)}")
        print(f"  - Unique threads: {stats.get('unique_threads', 0)}")
        print(f"  - DB size: {stats.get('db_size_mb', 0)} MB")
        print(f"  - Max per thread: {stats.get('max_checkpoints_per_thread')}")
        print(f"  - Cleanup interval: {stats.get('cleanup_interval')}")

        # Verify stats structure
        assert "total_checkpoints" in stats, "Should have total_checkpoints"
        assert "unique_threads" in stats, "Should have unique_threads"
        assert "db_size_bytes" in stats, "Should have db_size_bytes"
        assert "db_size_mb" in stats, "Should have db_size_mb"
        assert "thread_stats" in stats, "Should have thread_stats"
        assert (
            "max_checkpoints_per_thread" in stats
        ), "Should have max_checkpoints_per_thread"
        assert "cleanup_interval" in stats, "Should have cleanup_interval"

        print("\n✓ All checkpoint cleanup tests passed!")

    # Cleanup test file
    if task_list_path.exists():
        task_list_path.unlink()


if __name__ == "__main__":
    test_checkpoint_cleanup()
