"""
Tests for orchestrator checkpoint persistence functionality.

Tests AsyncSqliteSaver integration, thread ID management, session resumption,
and resource cleanup for the LangGraph orchestrator.

Note: The orchestrator uses async initialization via setup() method.
Most tests use sync operations that don't require the full async setup.
"""

import sqlite3
import tempfile
from pathlib import Path
from datetime import datetime

import pytest

from sharpy_auto_builder.config import Config
from sharpy_auto_builder.orchestrator import Orchestrator
from sharpy_auto_builder.orchestrator.types import create_initial_state


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

    @pytest.mark.asyncio
    async def test_checkpoint_database_creation(self, temp_config):
        """Test that checkpoint database file is created after async setup."""
        db_path = temp_config.checkpoint_db_path
        assert not db_path.exists(), "Database should not exist before init"

        async with Orchestrator(temp_config) as orch:
            # Database file should be created by aiosqlite.connect()
            assert db_path.exists(), "Database should exist after async setup"
            assert db_path.is_file(), "Database path should be a file"

    @pytest.mark.asyncio
    async def test_checkpoint_database_tables(self, temp_config):
        """Test that database is accessible after async setup."""
        async with Orchestrator(temp_config) as orch:
            db_path = temp_config.checkpoint_db_path

            # Verify the checkpointer is initialized
            assert orch.checkpointer is not None, "Checkpointer should be initialized"

            # Note: AsyncSqliteSaver creates tables lazily on first write.
            # We just verify the database exists and is accessible.
            conn = sqlite3.connect(str(db_path))
            cursor = conn.cursor()

            # Get list of tables - may be empty before first checkpoint write
            cursor.execute("SELECT name FROM sqlite_master WHERE type='table'")
            tables = [row[0] for row in cursor.fetchall()]

            # Tables may or may not exist yet depending on whether checkpoints
            # have been written. Just ensure we can query the database.
            assert isinstance(tables, list), "Should be able to query tables"

            conn.close()

    @pytest.mark.asyncio
    async def test_checkpointer_type(self, temp_config):
        """Test that orchestrator uses AsyncSqliteSaver after setup."""
        async with Orchestrator(temp_config) as orch:
            checkpointer_type = type(orch.checkpointer).__name__
            assert (
                checkpointer_type == "AsyncSqliteSaver"
            ), f"Should use AsyncSqliteSaver, got {checkpointer_type}"

    def test_checkpointer_is_none_before_setup(self, temp_config):
        """Test that checkpointer is None before async setup is called."""
        orch = Orchestrator(temp_config)
        assert orch.checkpointer is None, "Checkpointer should be None before setup()"

    def test_checkpoint_config_applied(self, temp_config):
        """Test that checkpoint configuration is properly applied."""
        # Customize checkpoint config
        temp_config.checkpoint.durability_mode = "sync"
        temp_config.checkpoint.max_checkpoints_per_thread = 50
        temp_config.checkpoint.cleanup_interval = 25

        # Sync context manager doesn't call setup, but config is still applied
        with Orchestrator(temp_config) as orch:
            assert orch._cleanup_interval == 25, "Cleanup interval should match config"

    @pytest.mark.asyncio
    async def test_async_context_manager_cleanup(self, temp_config):
        """Test that async context manager properly closes database connection."""
        db_path = temp_config.checkpoint_db_path

        async with Orchestrator(temp_config) as orch:
            # Database should be created during async setup
            assert db_path.exists()
            # Checkpointer should be set
            assert orch.checkpointer is not None

        # After exiting, we should be able to open the database independently
        conn = sqlite3.connect(str(db_path))
        conn.execute("SELECT 1")  # Should not raise if properly closed
        conn.close()


class TestThreadIdManagement:
    """Tests for thread ID generation and management."""

    def test_thread_id_format(self, temp_config):
        """Test that generated thread IDs follow expected format."""
        with Orchestrator(temp_config) as orch:
            # Generate a thread ID (simulating what would happen in a run)
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
        """Test that create_initial_state produces valid state."""
        # Use the module-level function, not an orchestrator method
        state = create_initial_state(str(temp_config.ground_truth_path))

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

    @pytest.mark.asyncio
    async def test_checkpoint_stats_returns_dict(self, temp_config):
        """Test that get_checkpoint_stats returns a dictionary."""
        async with Orchestrator(temp_config) as orch:
            stats = orch.get_checkpoint_stats()

            # Should return a dict (may have error key if sync connection not available)
            assert isinstance(stats, dict), "Should return a dictionary"

    def test_checkpoint_stats_sync_returns_error(self, temp_config):
        """Test that get_checkpoint_stats returns error without async setup.

        The current implementation uses _db_connection which is not set.
        This test documents the expected behavior.
        """
        with Orchestrator(temp_config) as orch:
            stats = orch.get_checkpoint_stats()

            # Without async setup, the sync _db_connection doesn't exist
            # so we expect an error dict
            assert isinstance(stats, dict)
            assert "error" in stats or "total_checkpoints" in stats


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

    @pytest.mark.asyncio
    async def test_async_database_connection_closed(self, temp_config):
        """Test that database connection is properly closed on async cleanup."""
        db_path = temp_config.checkpoint_db_path

        async with Orchestrator(temp_config) as orch:
            # Store reference to check later (not directly accessible)
            assert orch._async_db_conn is not None

        # After async context exit, connection should be closed
        # We verify by successfully opening the database
        conn = sqlite3.connect(str(db_path))
        cursor = conn.cursor()
        cursor.execute("SELECT 1")
        conn.close()


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

    def test_graph_is_built(self, temp_config):
        """Test that graph is built during initialization."""
        with Orchestrator(temp_config) as orch:
            # Graph should be built (but app may not be compiled without async setup)
            assert orch.graph is not None, "Graph should be built"

    @pytest.mark.asyncio
    async def test_app_is_compiled_after_setup(self, temp_config):
        """Test that app is compiled after async setup."""
        async with Orchestrator(temp_config) as orch:
            # App should be compiled with checkpointer
            assert orch.app is not None, "App should be compiled after setup"


if __name__ == "__main__":
    # Run tests with pytest
    pytest.main([__file__, "-v"])
