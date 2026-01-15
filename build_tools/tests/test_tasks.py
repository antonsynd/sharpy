"""Tests for idempotent task execution functions."""

import asyncio
import pytest
from pathlib import Path
from build_tools.sharpy_auto_builder.tasks import (
    execute_claude_cli,
    execute_copilot_cli,
    run_tests,
    TaskExecutionResult,
    TaskIdempotencyFallback,
    _compute_input_hash,
    _is_rate_limited,
)


class TestTaskExecutionResult:
    """Tests for TaskExecutionResult dataclass."""

    def test_create_result(self):
        """Test creating a TaskExecutionResult."""
        result = TaskExecutionResult(
            success=True,
            output="test output",
            error=None,
            backend="claude-code",
            model="sonnet",
            duration_seconds=1.5,
            exit_code=0,
            input_hash="abc123",
            metadata={"task_id": "test", "attempt": 1},
        )
        assert result.success is True
        assert result.output == "test output"
        assert result.error is None
        assert result.backend == "claude-code"
        assert result.exit_code == 0

    def test_to_dict(self):
        """Test serializing TaskExecutionResult to dict."""
        result = TaskExecutionResult(
            success=True,
            output="test",
            backend="test-backend",
            input_hash="hash123",
        )
        data = result.to_dict()
        assert isinstance(data, dict)
        assert data["success"] is True
        assert data["output"] == "test"
        assert data["backend"] == "test-backend"
        assert "timestamp" in data

    def test_from_dict(self):
        """Test deserializing TaskExecutionResult from dict."""
        data = {
            "success": False,
            "output": "",
            "error": "test error",
            "backend": "copilot",
            "model": "default",
            "duration_seconds": 2.0,
            "exit_code": 1,
            "timestamp": "2026-01-14T10:00:00",
            "input_hash": "hash456",
            "metadata": {"attempt": 2},
        }
        result = TaskExecutionResult.from_dict(data)
        assert result.success is False
        assert result.error == "test error"
        assert result.backend == "copilot"
        assert result.exit_code == 1
        assert result.metadata["attempt"] == 2

    def test_from_dict_filters_unknown_fields(self):
        """Test that from_dict filters out unknown fields."""
        data = {
            "success": True,
            "output": "test",
            "backend": "test",
            "unknown_field": "should be ignored",
            "another_unknown": 123,
        }
        result = TaskExecutionResult.from_dict(data)
        assert result.success is True
        assert not hasattr(result, "unknown_field")


class TestComputeInputHash:
    """Tests for _compute_input_hash helper."""

    def test_hash_consistency(self):
        """Test that same inputs produce same hash."""
        hash1 = _compute_input_hash("test", 123, key="value")
        hash2 = _compute_input_hash("test", 123, key="value")
        assert hash1 == hash2

    def test_hash_uniqueness(self):
        """Test that different inputs produce different hashes."""
        hash1 = _compute_input_hash("test1")
        hash2 = _compute_input_hash("test2")
        assert hash1 != hash2

    def test_hash_order_independence_for_kwargs(self):
        """Test that kwargs order doesn't affect hash."""
        hash1 = _compute_input_hash(a=1, b=2, c=3)
        hash2 = _compute_input_hash(c=3, a=1, b=2)
        assert hash1 == hash2

    def test_hash_is_sha256(self):
        """Test that hash is a valid SHA256 hex string."""
        result = _compute_input_hash("test")
        assert isinstance(result, str)
        assert len(result) == 64  # SHA256 hex string length
        assert all(c in "0123456789abcdef" for c in result)


class TestIsRateLimited:
    """Tests for _is_rate_limited helper."""

    def test_detects_rate_limit(self):
        """Test detection of rate limit indicators."""
        assert _is_rate_limited("Error: rate limit exceeded", "") is True
        assert _is_rate_limited("", "429 Too Many Requests") is True
        assert _is_rate_limited("quota exceeded", "") is True
        assert _is_rate_limited("", "overloaded_error") is True

    def test_no_rate_limit(self):
        """Test normal output doesn't trigger rate limit detection."""
        assert _is_rate_limited("Success!", "All good") is False
        assert _is_rate_limited("Processing complete", "") is False

    def test_case_insensitive(self):
        """Test rate limit detection is case insensitive."""
        assert _is_rate_limited("RATE LIMIT", "") is True
        assert _is_rate_limited("Rate-Limit", "") is True


class TestTaskIdempotencyFallback:
    """Tests for TaskIdempotencyFallback class."""

    def test_create_tracker(self, tmp_path):
        """Test creating a fallback tracker."""
        tracker = TaskIdempotencyFallback(cache_dir=tmp_path / "cache")
        assert tracker.cache_dir == tmp_path / "cache"
        assert tracker.cache_dir.exists()

    def test_cache_miss(self, tmp_path):
        """Test getting cached result when none exists."""
        tracker = TaskIdempotencyFallback(cache_dir=tmp_path / "cache")
        result = tracker.get_cached("nonexistent_hash")
        assert result is None

    def test_cache_hit(self, tmp_path):
        """Test caching and retrieving a result."""
        tracker = TaskIdempotencyFallback(cache_dir=tmp_path / "cache")

        original_result = TaskExecutionResult(
            success=True,
            output="cached output",
            backend="test",
            input_hash="test_hash_123",
        )

        tracker.cache_result("test_hash_123", original_result)
        cached_result = tracker.get_cached("test_hash_123")

        assert cached_result is not None
        assert cached_result.success is True
        assert cached_result.output == "cached output"
        assert cached_result.backend == "test"

    def test_marker_path(self, tmp_path):
        """Test marker path generation."""
        tracker = TaskIdempotencyFallback(cache_dir=tmp_path / "cache")
        path = tracker._marker_path("abc123")
        assert path == tmp_path / "cache" / "abc123.json"


# Note about testing @task decorated functions:
# ===============================================
# The task functions (execute_claude_cli, execute_copilot_cli, run_tests) are
# decorated with @task from LangGraph. This decorator requires a runnable context
# to function properly - they must be called from within a LangGraph graph.
#
# Testing these functions directly in unit tests would fail with:
# "RuntimeError: Called get_config outside of a runnable context"
#
# These functions should be tested via:
# 1. Integration tests that execute them within a LangGraph graph
# 2. Manual testing with the orchestrator
# 3. Testing the underlying implementation logic separately
#
# For now, we've tested all the helper functions and data structures.
# The task functions themselves are integration-tested through orchestrator tests.


# Note: execute_claude_cli and execute_copilot_cli require actual CLI tools
# to be installed, so they are tested in integration tests with the orchestrator.
