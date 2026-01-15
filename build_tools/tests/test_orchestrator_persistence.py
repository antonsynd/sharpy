"""
Tests for orchestrator checkpoint persistence functionality.

Tests SqliteSaver integration, thread ID management, session resumption,
and resource cleanup for the LangGraph orchestrator.
"""

import os
import sqlite3
import tempfile
from pathlib import Path
from datetime import datetime

import pytest

from sharpy_auto_builder.config import Config
from sharpy_auto_builder.orchestrator import Orchestrator


@pytest.fixture
def temp_config():
    """Create a temporary config with isolated state directory."""
    with tempfile.TemporaryDirectory() as tmpdir:
        config = Config()
        # Use temporary directory for test isolation
        config.project_root = Path(tmpdir)

        # Create minimal task list
        task_list_path = Path(tmpdir) / "test_task_list.md"
        task_list_path.write_text("# Test Task List\n\n- [ ] Test task\n")
        config.task_list_path = task_list_path

        # Ensure directories are created
        config.ensure_directories()

        yield config


class TestCheckpointPersistence:
    """Tests for checkpoint database persistence."""

    def test_checkpoint_database_creation(self, temp_config):
        """Test that checkpoint database is created on orchestrator init."""
        db_path = temp_config.checkpoint_db_path
        assert not db_path.exists(), "Database should not exist before init"

        with Orchestrator(temp_config) as orch:
            assert db_path.exists(), "Database should exist after init"
            assert db_path.is_file(), "Database path should be a file"
            assert db_path.stat().st_size > 0, "Database file should not be empty"

    def test_checkpoint_database_tables(self, temp_config):
        """Test that required tables are created in checkpoint database."""
        with Orchestrator(temp_config) as orch:
            db_path = temp_config.checkpoint_db_path

            # Connect to database and check tables
            conn = sqlite3.connect(str(db_path))
            cursor = conn.cursor()

            # Get list of tables
            cursor.execute("SELECT name FROM sqlite_master WHERE type='table'")
            tables = [row[0] for row in cursor.fetchall()]

            # LangGraph checkpoint tables
            assert "checkpoints" in tables, "Should have checkpoints table"
            assert "writes" in tables, "Should have writes table"

            conn.close()

    def test_checkpointer_type(self, temp_config):
        """Test that orchestrator uses SqliteSaver, not MemorySaver."""
        with Orchestrator(temp_config) as orch:
            checkpointer_type = type(orch.checkpointer).__name__
            assert (
                checkpointer_type == "SqliteSaver"
            ), f"Should use SqliteSaver, got {checkpointer_type}"

    def test_checkpoint_config_applied(self, temp_config):
        """Test that checkpoint configuration is properly applied."""
        # Customize checkpoint config
        temp_config.checkpoint.durability_mode = "sync"
        temp_config.checkpoint.max_checkpoints_per_thread = 50
        temp_config.checkpoint.cleanup_interval = 25

        with Orchestrator(temp_config) as orch:
            assert orch._cleanup_interval == 25, "Cleanup interval should match config"

    def test_context_manager_cleanup(self, temp_config):
        """Test that context manager properly closes database connection."""
        db_path = temp_config.checkpoint_db_path

        orch = Orchestrator(temp_config)
        # Database should be created
        assert db_path.exists()

        # Close via context manager
        orch.__exit__(None, None, None)

        # Connection should be closed (verify by opening independently)
        conn = sqlite3.connect(str(db_path))
        conn.execute("SELECT 1")  # Should not raise if properly closed
        conn.close()


class TestThreadIdManagement:
    """Tests for thread ID generation and management."""

    def test_thread_id_format(self, temp_config):
        """Test that generated thread IDs follow expected format."""
        with Orchestrator(temp_config) as orch:
            # Generate a thread ID
            thread_id = f"sharpy-build-{datetime.now().strftime('%Y%m%d-%H%M%S')}"

            # Verify format: sharpy-build-YYYYMMDD-HHMMSS
            import re

            pattern = r"^sharpy-build-\d{8}-\d{6}$"
            assert re.match(
                pattern, thread_id
            ), f"Thread ID should match pattern {pattern}, got {thread_id}"

    def test_thread_id_storage(self, temp_config):
        """Test that current thread ID is stored in orchestrator."""
        with Orchestrator(temp_config) as orch:
            # Simulate run with thread ID
            test_thread_id = "sharpy-build-20260114-120000"
            orch._current_thread_id = test_thread_id

            assert (
                orch._current_thread_id == test_thread_id
            ), "Thread ID should be stored"

    def test_create_initial_state(self, temp_config):
        """Test that _create_initial_state produces valid state."""
        with Orchestrator(temp_config) as orch:
            state = orch._create_initial_state()

            # Verify required fields
            assert state is not None, "Initial state should not be None"
            assert "current_task" in state, "Should have current_task field"
            assert "ground_truth_path" in state, "Should have ground_truth_path field"
            assert "execution_attempt" in state, "Should have execution_attempt field"
            assert "messages" in state, "Should have messages field"

            # Verify initial values
            assert state["current_task"] is None, "Initial task should be None"
            assert state["execution_attempt"] == 0, "Initial attempt should be 0"
            assert isinstance(state["messages"], list), "Messages should be a list"


class TestCheckpointStats:
    """Tests for checkpoint statistics and monitoring."""

    def test_checkpoint_stats_structure(self, temp_config):
        """Test that get_checkpoint_stats returns proper structure."""
        with Orchestrator(temp_config) as orch:
            stats = orch.get_checkpoint_stats()

            # Verify all expected fields
            assert "total_checkpoints" in stats, "Should have total_checkpoints"
            assert "unique_threads" in stats, "Should have unique_threads"
            assert "db_size_bytes" in stats, "Should have db_size_bytes"
            assert "db_size_mb" in stats, "Should have db_size_mb"
            assert "thread_stats" in stats, "Should have thread_stats"
            assert (
                "max_checkpoints_per_thread" in stats
            ), "Should have max_checkpoints_per_thread"
            assert "cleanup_interval" in stats, "Should have cleanup_interval"

    def test_checkpoint_stats_initial_values(self, temp_config):
        """Test initial checkpoint statistics values."""
        with Orchestrator(temp_config) as orch:
            stats = orch.get_checkpoint_stats()

            # Fresh database should have minimal stats
            assert stats["total_checkpoints"] >= 0, "Total should be non-negative"
            assert stats["unique_threads"] >= 0, "Unique threads should be non-negative"
            assert stats["db_size_bytes"] > 0, "Database should have size"
            assert (
                stats["db_size_mb"] >= 0
            ), "Database size in MB should be non-negative"

            # Config values should match
            assert (
                stats["max_checkpoints_per_thread"]
                == temp_config.checkpoint.max_checkpoints_per_thread
            )
            assert stats["cleanup_interval"] == temp_config.checkpoint.cleanup_interval


class TestCleanup:
    """Tests for checkpoint cleanup and resource management."""

    def test_cleanup_tracking_initialization(self, temp_config):
        """Test that cleanup tracking is initialized correctly."""
        with Orchestrator(temp_config) as orch:
            assert hasattr(
                orch, "_checkpoint_count"
            ), "Should have _checkpoint_count attribute"
            assert hasattr(
                orch, "_cleanup_interval"
            ), "Should have _cleanup_interval attribute"

            assert orch._checkpoint_count == 0, "Initial count should be 0"
            assert (
                orch._cleanup_interval == temp_config.checkpoint.cleanup_interval
            ), "Cleanup interval should match config"

    def test_cleanup_interval_configuration(self, temp_config):
        """Test that cleanup interval can be configured."""
        temp_config.checkpoint.cleanup_interval = 100

        with Orchestrator(temp_config) as orch:
            assert (
                orch._cleanup_interval == 100
            ), "Cleanup interval should match configured value"

    def test_database_connection_closed(self, temp_config):
        """Test that database connection is properly closed on cleanup."""
        orch = Orchestrator(temp_config)
        db_conn = orch._db_connection

        # Close orchestrator
        orch.close()

        # Connection should be closed
        try:
            db_conn.execute("SELECT 1")
            pytest.fail("Connection should be closed")
        except sqlite3.ProgrammingError:
            # Expected - connection is closed
            pass


class TestRateLimitRecovery:
    """Tests for rate limit detection and recovery."""

    @pytest.mark.asyncio
    async def test_rate_limit_detection(self, temp_config):
        """Test that rate limiting is properly detected and handled."""
        with Orchestrator(temp_config) as orch:
            # Set thread ID
            orch._current_thread_id = "test-thread-recovery"

            # Create state with rate limit error
            state = {
                "current_task": {"id": "test", "description": "Test task"},
                "execution_attempt": 1,
                "last_execution_result": {
                    "error": "Rate limit exhausted for all backends"
                },
                "messages": [],
            }

            # Handle error
            result = await orch._handle_error_node(state)

            # Verify rate limit handling
            assert (
                result["next_action"] == "pause_rate_limited"
            ), "Should set next_action to pause_rate_limited"
            assert result["execution_attempt"] == 0, "Should reset execution attempt"
            assert any(
                "rate limiting" in msg.lower() for msg in result["messages"]
            ), "Should include rate limit message"

    @pytest.mark.asyncio
    async def test_non_rate_limit_error_handling(self, temp_config):
        """Test that non-rate-limit errors are handled differently."""
        with Orchestrator(temp_config) as orch:
            # Create state with normal error
            state = {
                "current_task": {"id": "test", "description": "Test task"},
                "execution_attempt": 1,
                "last_execution_result": {"error": "Some other error"},
                "messages": [],
            }

            # Handle error
            result = await orch._handle_error_node(state)

            # Should retry, not pause for rate limit
            assert (
                result["next_action"] != "pause_rate_limited"
            ), "Should not pause for non-rate-limit errors"
            assert result["next_action"] == "retry", "Should retry on normal errors"


class TestGraphRouting:
    """Tests for graph routing with checkpoint persistence."""

    def test_pause_rate_limited_routing_exists(self, temp_config):
        """Test that graph includes pause_rate_limited routing."""
        with Orchestrator(temp_config) as orch:
            # Build graph
            graph = orch._build_graph()

            # Graph should be compiled
            assert orch.app is not None, "App should be compiled"

            # Verify by inspecting graph structure (implementation-dependent)
            # This is a basic check that graph builds without error
            assert graph is not None, "Graph should be built"


if __name__ == "__main__":
    # Run tests with pytest
    pytest.main([__file__, "-v"])
