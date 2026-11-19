"""Tests for shared execution logging."""

import json
import pytest
from build_tools.shared.logging import (
    LogEvent,
    LogEventType,
    ExecutionLogger,
)


class TestLogEvent:
    """Tests for LogEvent dataclass."""

    def test_create_event_with_enum(self):
        """Test creating event with LogEventType enum."""
        event = LogEvent(
            timestamp="2026-01-13T10:00:00",
            event_type=LogEventType.PROMPT_SENT,
            details={"prompt": "test"},
        )
        assert event.timestamp == "2026-01-13T10:00:00"
        assert event.event_type == "prompt_sent"
        assert event.details == {"prompt": "test"}
        assert event.event_id is None

    def test_create_event_with_string(self):
        """Test creating event with string event_type."""
        event = LogEvent(
            timestamp="2026-01-13T10:00:00",
            event_type="custom_event",
            details={},
        )
        assert event.event_type == "custom_event"

    def test_event_with_id(self):
        """Test event with explicit ID."""
        event = LogEvent(
            timestamp="2026-01-13T10:00:00",
            event_type=LogEventType.TASK_START,
            details={"task": "1"},
            event_id="test-123",
        )
        assert event.event_id == "test-123"

    def test_to_dict(self):
        """Test serialization to dictionary."""
        event = LogEvent(
            timestamp="2026-01-13T10:00:00",
            event_type=LogEventType.RESPONSE_RECEIVED,
            details={"success": True},
            event_id="abc",
        )
        data = event.to_dict()

        assert data["timestamp"] == "2026-01-13T10:00:00"
        assert data["event_type"] == "response_received"
        assert data["details"] == {"success": True}
        assert data["event_id"] == "abc"

    def test_to_dict_without_id(self):
        """Test serialization without event_id."""
        event = LogEvent(
            timestamp="2026-01-13T10:00:00",
            event_type=LogEventType.ERROR,
            details={},
        )
        data = event.to_dict()

        assert "event_id" not in data

    def test_to_jsonl(self):
        """Test JSONL serialization."""
        event = LogEvent(
            timestamp="2026-01-13T10:00:00",
            event_type=LogEventType.PROMPT_SENT,
            details={"backend": "claude"},
        )
        jsonl = event.to_jsonl()

        # Should be valid JSON on single line
        data = json.loads(jsonl)
        assert data["event_type"] == "prompt_sent"
        assert "\n" not in jsonl


class TestExecutionLogger:
    """Tests for ExecutionLogger class."""

    def test_init_creates_parent_directory(self, tmp_path):
        """Test that logger creates parent directories."""
        log_path = tmp_path / "logs" / "nested" / "execution.jsonl"
        logger = ExecutionLogger(log_path)

        assert log_path.parent.exists()

    def test_log_basic_event(self, tmp_path):
        """Test logging a basic event."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)

        event_id = logger.log(
            LogEventType.TASK_START,
            {"task_id": "1", "name": "Test Task"},
        )

        assert event_id is not None
        assert log_path.exists()

        # Read and verify
        with open(log_path) as f:
            line = f.read().strip()
            data = json.loads(line)
            assert data["event_type"] == "task_start"
            assert data["details"]["task_id"] == "1"
            assert data["event_id"] == event_id

    def test_log_with_explicit_event_id(self, tmp_path):
        """Test logging with explicit event ID."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)

        event_id = logger.log(
            LogEventType.PROMPT_SENT,
            {"prompt": "test"},
            event_id="custom-id-123",
        )

        assert event_id == "custom-id-123"

        events = [json.loads(line) for line in log_path.read_text().splitlines()]
        assert events[0]["event_id"] == "custom-id-123"

    def test_log_multiple_events(self, tmp_path):
        """Test logging multiple events."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)

        logger.log(LogEventType.TASK_START, {"task": "1"})
        logger.log(LogEventType.TASK_COMPLETE, {"task": "1", "success": True})

        events = [json.loads(line) for line in log_path.read_text().splitlines()]
        assert len(events) == 2
        assert events[0]["event_type"] == "task_start"
        assert events[1]["event_type"] == "task_complete"


class TestLogEventTypes:
    """Tests for LogEventType enum."""

    def test_all_event_types_have_values(self):
        """Test that all enum members have string values."""
        for event_type in LogEventType:
            assert isinstance(event_type.value, str)
            assert len(event_type.value) > 0

    def test_event_type_values_unique(self):
        """Test that all enum values are unique."""
        values = [e.value for e in LogEventType]
        assert len(values) == len(set(values))

    def test_enum_from_string(self):
        """Test creating enum from string value."""
        event_type = LogEventType("prompt_sent")
        assert event_type == LogEventType.PROMPT_SENT

    def test_enum_invalid_string(self):
        """Test that invalid string raises ValueError."""
        with pytest.raises(ValueError):
            LogEventType("invalid_event_type")
