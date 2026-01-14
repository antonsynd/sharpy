"""Tests for shared execution logging."""

import json
import pytest
from datetime import datetime
from pathlib import Path
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
    
    def test_from_dict(self):
        """Test deserialization from dictionary."""
        data = {
            "timestamp": "2026-01-13T10:00:00",
            "event_type": "task_complete",
            "details": {"success": True},
            "event_id": "xyz",
        }
        event = LogEvent.from_dict(data)
        
        assert event.timestamp == "2026-01-13T10:00:00"
        assert event.event_type == "task_complete"
        assert event.details == {"success": True}
        assert event.event_id == "xyz"
    
    def test_from_dict_minimal(self):
        """Test deserialization with minimal fields."""
        data = {
            "timestamp": "2026-01-13T10:00:00",
            "event_type": "error",
        }
        event = LogEvent.from_dict(data)
        
        assert event.details == {}
        assert event.event_id is None


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
        
        events = ExecutionLogger.read_log(log_path)
        assert events[0].event_id == "custom-id-123"
    
    def test_log_multiple_events(self, tmp_path):
        """Test logging multiple events."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        logger.log(LogEventType.TASK_START, {"task": "1"})
        logger.log(LogEventType.TASK_COMPLETE, {"task": "1", "success": True})
        
        events = ExecutionLogger.read_log(log_path)
        assert len(events) == 2
        assert events[0].event_type == "task_start"
        assert events[1].event_type == "task_complete"
    
    def test_log_prompt(self, tmp_path):
        """Test log_prompt convenience method."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        event_id = logger.log_prompt(
            "Generate a function",
            "claude_code",
            model="claude-opus",
            task_id="task-1",
        )
        
        events = ExecutionLogger.read_log(log_path)
        assert len(events) == 1
        assert events[0].event_type == "prompt_sent"
        assert events[0].details["prompt"] == "Generate a function"
        assert events[0].details["backend"] == "claude_code"
        assert events[0].details["model"] == "claude-opus"
        assert events[0].details["task_id"] == "task-1"
        assert events[0].event_id == event_id
    
    def test_log_prompt_minimal(self, tmp_path):
        """Test log_prompt with minimal arguments."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        logger.log_prompt("Test prompt", "copilot")
        
        events = ExecutionLogger.read_log(log_path)
        assert events[0].details["prompt"] == "Test prompt"
        assert events[0].details["backend"] == "copilot"
        assert "model" not in events[0].details
        assert "task_id" not in events[0].details
    
    def test_log_response(self, tmp_path):
        """Test log_response convenience method."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        prompt_id = logger.log_prompt("Test", "claude")
        logger.log_response(
            prompt_id,
            "Here's the response",
            success=True,
            duration_seconds=2.5,
            backend="claude_code",
        )
        
        events = ExecutionLogger.read_log(log_path)
        assert len(events) == 2
        
        response_event = events[1]
        assert response_event.event_type == "response_received"
        assert response_event.details["response"] == "Here's the response"
        assert response_event.details["success"] is True
        assert response_event.details["duration_seconds"] == 2.5
        assert response_event.details["backend"] == "claude_code"
        assert response_event.event_id == prompt_id
    
    def test_log_response_with_error(self, tmp_path):
        """Test logging failed response."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        prompt_id = logger.log_prompt("Test", "claude")
        logger.log_response(
            prompt_id,
            "",
            success=False,
            duration_seconds=0.5,
            error="Connection timeout",
        )
        
        events = ExecutionLogger.read_log(log_path)
        response = events[1]
        assert response.details["success"] is False
        assert response.details["error"] == "Connection timeout"
    
    def test_log_rate_limit(self, tmp_path):
        """Test log_rate_limit convenience method."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        logger.log_rate_limit(
            "claude_code",
            wait_seconds=60.0,
            message="Rate limit exceeded",
        )
        
        events = ExecutionLogger.read_log(log_path)
        assert events[0].event_type == "rate_limit_hit"
        assert events[0].details["backend"] == "claude_code"
        assert events[0].details["wait_seconds"] == 60.0
        assert events[0].details["message"] == "Rate limit exceeded"
    
    def test_log_rate_limit_minimal(self, tmp_path):
        """Test log_rate_limit with minimal args."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        logger.log_rate_limit("copilot")
        
        events = ExecutionLogger.read_log(log_path)
        assert events[0].details["backend"] == "copilot"
        assert "wait_seconds" not in events[0].details
        assert "message" not in events[0].details
    
    def test_log_backend_switch(self, tmp_path):
        """Test log_backend_switch convenience method."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        logger.log_backend_switch(
            "claude_code",
            "copilot",
            "Rate limit on Claude",
        )
        
        events = ExecutionLogger.read_log(log_path)
        assert events[0].event_type == "backend_switch"
        assert events[0].details["from_backend"] == "claude_code"
        assert events[0].details["to_backend"] == "copilot"
        assert events[0].details["reason"] == "Rate limit on Claude"
    
    def test_log_model_selection(self, tmp_path):
        """Test log_model_selection convenience method."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        logger.log_model_selection(
            "code_generation",
            "medium",
            "claude-sonnet-4-5",
            "Sonnet handles medium complexity well",
        )
        
        events = ExecutionLogger.read_log(log_path)
        assert events[0].event_type == "model_selected"
        assert events[0].details["task_type"] == "code_generation"
        assert events[0].details["complexity"] == "medium"
        assert events[0].details["selected_model"] == "claude-sonnet-4-5"
        assert "reasoning" in events[0].details
    
    def test_log_task(self, tmp_path):
        """Test log_task convenience method."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        logger.log_task(
            LogEventType.TASK_START,
            "task-1",
            name="Test Task",
            priority="high",
        )
        
        events = ExecutionLogger.read_log(log_path)
        assert events[0].event_type == "task_start"
        assert events[0].details["task_id"] == "task-1"
        assert events[0].details["name"] == "Test Task"
        assert events[0].details["priority"] == "high"
    
    def test_read_log_empty_file(self, tmp_path):
        """Test reading empty log file."""
        log_path = tmp_path / "empty.jsonl"
        log_path.touch()
        
        events = ExecutionLogger.read_log(log_path)
        assert events == []
    
    def test_read_log_nonexistent_file(self, tmp_path):
        """Test reading nonexistent log file raises error."""
        log_path = tmp_path / "missing.jsonl"
        
        with pytest.raises(FileNotFoundError):
            ExecutionLogger.read_log(log_path)
    
    def test_read_log_with_blank_lines(self, tmp_path):
        """Test reading log with blank lines."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        logger.log(LogEventType.TASK_START, {"task": "1"})
        
        # Add blank lines manually
        with open(log_path, "a") as f:
            f.write("\n\n")
        
        logger.log(LogEventType.TASK_COMPLETE, {"task": "1"})
        
        events = ExecutionLogger.read_log(log_path)
        assert len(events) == 2
    
    def test_read_log_with_malformed_line(self, tmp_path, capsys):
        """Test reading log with malformed JSON line."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        logger.log(LogEventType.TASK_START, {"task": "1"})
        
        # Add malformed line
        with open(log_path, "a") as f:
            f.write("not valid json\n")
        
        logger.log(LogEventType.TASK_COMPLETE, {"task": "1"})
        
        events = ExecutionLogger.read_log(log_path)
        
        # Should skip malformed line and continue
        assert len(events) == 2
        
        # Should print warning
        captured = capsys.readouterr()
        assert "Skipping malformed log line" in captured.out


class TestQueryLog:
    """Tests for log querying functionality."""
    
    def setup_sample_log(self, tmp_path) -> Path:
        """Create a sample log file with various events."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        # Add various events
        logger.log_task(LogEventType.TASK_START, "task-1", name="First")
        logger.log_prompt("Prompt 1", "claude", task_id="task-1")
        logger.log_response("evt-1", "Response 1", True, 1.0)
        logger.log_rate_limit("claude", wait_seconds=60.0)
        logger.log_task(LogEventType.TASK_COMPLETE, "task-1", success=True)
        logger.log_task(LogEventType.TASK_START, "task-2", name="Second")
        
        return log_path
    
    def test_query_by_event_type(self, tmp_path):
        """Test filtering by event type."""
        log_path = self.setup_sample_log(tmp_path)
        
        events = ExecutionLogger.query_log(
            log_path,
            event_type=LogEventType.TASK_START,
        )
        
        assert len(events) == 2
        assert all(e.event_type == "task_start" for e in events)
    
    def test_query_by_event_type_string(self, tmp_path):
        """Test filtering by event type as string."""
        log_path = self.setup_sample_log(tmp_path)
        
        events = ExecutionLogger.query_log(
            log_path,
            event_type="rate_limit_hit",
        )
        
        assert len(events) == 1
        assert events[0].event_type == "rate_limit_hit"
    
    def test_query_by_task_id(self, tmp_path):
        """Test filtering by task_id."""
        log_path = self.setup_sample_log(tmp_path)
        
        events = ExecutionLogger.query_log(
            log_path,
            task_id="task-1",
        )
        
        # Should get task_start, prompt_sent, and task_complete for task-1
        assert len(events) == 3
        assert all(e.details.get("task_id") == "task-1" for e in events)
    
    def test_query_by_time_range(self, tmp_path):
        """Test filtering by time range."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        # Log events with known times
        now = datetime.now()
        
        logger.log(LogEventType.TASK_START, {"task": "1"})
        
        # Get events after "now"
        events = ExecutionLogger.query_log(
            log_path,
            start_time=now,
        )
        
        assert len(events) >= 1
    
    def test_query_no_filters(self, tmp_path):
        """Test query with no filters returns all events."""
        log_path = self.setup_sample_log(tmp_path)
        
        events = ExecutionLogger.query_log(log_path)
        
        # Should return all 6 events
        assert len(events) == 6
    
    def test_get_correlated_events(self, tmp_path):
        """Test getting correlated events by ID."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        # Log prompt and response with same ID
        event_id = logger.log_prompt("Test", "claude")
        logger.log_response(event_id, "Response", True, 1.0)
        
        # Add unrelated event
        logger.log(LogEventType.TASK_START, {"task": "other"})
        
        # Get correlated events
        correlated = ExecutionLogger.get_correlated_events(log_path, event_id)
        
        assert len(correlated) == 2
        assert all(e.event_id == event_id for e in correlated)
        assert correlated[0].event_type == "prompt_sent"
        assert correlated[1].event_type == "response_received"
    
    def test_get_correlated_events_none_found(self, tmp_path):
        """Test getting correlated events when none exist."""
        log_path = tmp_path / "test.jsonl"
        logger = ExecutionLogger(log_path)
        
        logger.log(LogEventType.TASK_START, {"task": "1"})
        
        correlated = ExecutionLogger.get_correlated_events(
            log_path,
            "nonexistent-id",
        )
        
        assert correlated == []


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
