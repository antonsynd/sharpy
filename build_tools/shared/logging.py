"""
Shared execution logging for build tools.

This module provides a standardized JSONL-based logging system for tracking
AI backend executions, prompts, responses, and other events across all build tools.

Consolidates logging patterns from:
- generate_code_walkthroughs.py (log_execution function)
- sharpy_auto_builder/orchestrator.py (_log_execution method)

Key features:
- JSONL format for easy parsing and streaming
- Prompt/response correlation via event IDs
- Automatic timestamp generation
- Extensible event types
- Query utilities for analysis
"""

from dataclasses import dataclass, asdict
from datetime import datetime
from pathlib import Path
from enum import Enum
from typing import Any, Optional
import json
import uuid


class LogEventType(Enum):
    """Types of events that can be logged."""

    # Backend execution events
    PROMPT_SENT = "prompt_sent"
    RESPONSE_RECEIVED = "response_received"

    # Rate limiting events
    RATE_LIMIT_HIT = "rate_limit_hit"
    BACKEND_SWITCH = "backend_switch"

    # Error events
    ERROR = "error"

    # Task lifecycle events
    TASK_START = "task_start"
    TASK_COMPLETE = "task_complete"
    STEP_START = "step_start"
    STEP_END = "step_end"

    # Model selection events
    MODEL_SELECTED = "model_selected"

    # File generation events (for walkthrough generator)
    GENERATE = "generate"
    SKIP = "skip"
    REGENERATE = "regenerate"


@dataclass
class LogEvent:
    """A single log event with timestamp and details."""

    timestamp: str
    event_type: LogEventType
    details: dict[str, Any]
    event_id: Optional[str] = None

    def __post_init__(self):
        """Convert enum to string for serialization."""
        if isinstance(self.event_type, LogEventType):
            self.event_type = self.event_type.value

    def to_dict(self) -> dict[str, Any]:
        """Convert to dictionary for serialization."""
        result = {
            "timestamp": self.timestamp,
            "event_type": self.event_type,
            "details": self.details,
        }
        if self.event_id:
            result["event_id"] = self.event_id
        return result

    def to_jsonl(self) -> str:
        """Serialize to JSONL format (single line JSON)."""
        return json.dumps(self.to_dict())

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "LogEvent":
        """Deserialize from dictionary."""
        return cls(
            timestamp=data["timestamp"],
            event_type=data["event_type"],
            details=data.get("details", {}),
            event_id=data.get("event_id"),
        )


class ExecutionLogger:
    """
    JSONL-based execution logger for debugging and analysis.

    Usage:
        logger = ExecutionLogger(Path("execution.jsonl"))

        # Log a prompt
        event_id = logger.log_prompt("Generate a function...", "claude_code")

        # Log the response
        logger.log_response(event_id, "Here's the function...", True, 2.5)

        # Query the log
        events = ExecutionLogger.read_log(Path("execution.jsonl"))
    """

    def __init__(self, log_path: Path):
        """
        Initialize logger.

        Args:
            log_path: Path to JSONL log file. Parent directories created automatically.
        """
        self._log_path = log_path
        # Ensure parent directory exists
        self._log_path.parent.mkdir(parents=True, exist_ok=True)

    def log(
        self,
        event_type: LogEventType | str,
        details: dict[str, Any] | None = None,
        event_id: str | None = None,
    ) -> str:
        """
        Log an event.

        Args:
            event_type: Type of event
            details: Optional event details
            event_id: Optional event ID for correlation (auto-generated if not provided)

        Returns:
            Event ID (for correlating related events)
        """
        if details is None:
            details = {}

        if event_id is None:
            event_id = str(uuid.uuid4())

        # Convert string to enum if needed
        if isinstance(event_type, str):
            try:
                event_type = LogEventType(event_type)
            except ValueError:
                # If not a known enum value, keep as string
                pass

        event = LogEvent(
            timestamp=datetime.now().isoformat(),
            event_type=event_type,
            details=details,
            event_id=event_id,
        )

        # Append to log file
        with open(self._log_path, "a", encoding="utf-8") as f:
            f.write(event.to_jsonl() + "\n")

        return event_id

    def log_prompt(
        self,
        prompt: str,
        backend: str,
        model: str | None = None,
        task_id: str | None = None,
    ) -> str:
        """
        Log a prompt being sent to a backend.

        Args:
            prompt: The prompt text
            backend: Backend name (e.g., "claude_code", "copilot")
            model: Model name if applicable
            task_id: Optional task identifier

        Returns:
            Event ID for correlating with response
        """
        details: dict[str, Any] = {
            "prompt": prompt,
            "backend": backend,
        }

        if model:
            details["model"] = model
        if task_id:
            details["task_id"] = task_id

        return self.log(LogEventType.PROMPT_SENT, details)

    def log_response(
        self,
        event_id: str,
        response: str,
        success: bool,
        duration_seconds: float,
        backend: str | None = None,
        error: str | None = None,
    ) -> None:
        """
        Log a response received from a backend.

        Args:
            event_id: Event ID from log_prompt() for correlation
            response: The response text
            success: Whether the request succeeded
            duration_seconds: Request duration in seconds
            backend: Backend name if different from prompt
            error: Error message if success is False
        """
        details: dict[str, Any] = {
            "response": response,
            "success": success,
            "duration_seconds": round(duration_seconds, 2),
        }

        if backend:
            details["backend"] = backend
        if error:
            details["error"] = error

        self.log(LogEventType.RESPONSE_RECEIVED, details, event_id)

    def log_rate_limit(
        self,
        backend: str,
        wait_seconds: float | None = None,
        message: str | None = None,
    ) -> str:
        """
        Log a rate limit hit.

        Args:
            backend: Backend that hit the rate limit
            wait_seconds: How long to wait before retrying
            message: Optional rate limit message

        Returns:
            Event ID
        """
        details: dict[str, Any] = {"backend": backend}

        if wait_seconds is not None:
            details["wait_seconds"] = wait_seconds
        if message:
            details["message"] = message

        return self.log(LogEventType.RATE_LIMIT_HIT, details)

    def log_backend_switch(
        self,
        from_backend: str,
        to_backend: str,
        reason: str,
    ) -> str:
        """
        Log a backend switch (e.g., due to rate limiting).

        Args:
            from_backend: Backend being switched from
            to_backend: Backend being switched to
            reason: Reason for switch

        Returns:
            Event ID
        """
        details = {
            "from_backend": from_backend,
            "to_backend": to_backend,
            "reason": reason,
        }

        return self.log(LogEventType.BACKEND_SWITCH, details)

    def log_model_selection(
        self,
        task_type: str,
        complexity: str,
        selected_model: str,
        reasoning: str,
    ) -> str:
        """
        Log a model selection decision.

        Args:
            task_type: Type of task
            complexity: Task complexity level
            selected_model: Model that was selected
            reasoning: Why this model was chosen

        Returns:
            Event ID
        """
        details = {
            "task_type": task_type,
            "complexity": complexity,
            "selected_model": selected_model,
            "reasoning": reasoning,
        }

        return self.log(LogEventType.MODEL_SELECTED, details)

    def log_task(
        self,
        event_type: LogEventType,
        task_id: str,
        **kwargs: Any,
    ) -> str:
        """
        Log a task lifecycle event.

        Args:
            event_type: TASK_START, TASK_COMPLETE, STEP_START, or STEP_END
            task_id: Task identifier
            **kwargs: Additional details (e.g., success, duration_seconds, error)

        Returns:
            Event ID
        """
        details = {"task_id": task_id}
        details.update(kwargs)

        return self.log(event_type, details)

    @classmethod
    def read_log(cls, log_path: Path) -> list[LogEvent]:
        """
        Read all events from a log file.

        Args:
            log_path: Path to JSONL log file

        Returns:
            List of LogEvent objects

        Raises:
            FileNotFoundError: If log file doesn't exist
        """
        if not log_path.exists():
            raise FileNotFoundError(f"Log file not found: {log_path}")

        events = []
        with open(log_path, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                try:
                    data = json.loads(line)
                    events.append(LogEvent.from_dict(data))
                except json.JSONDecodeError as e:
                    # Skip malformed lines but continue reading
                    print(f"Warning: Skipping malformed log line: {e}")
                    continue

        return events

    @classmethod
    def query_log(
        cls,
        log_path: Path,
        event_type: LogEventType | str | None = None,
        start_time: datetime | None = None,
        end_time: datetime | None = None,
        task_id: str | None = None,
    ) -> list[LogEvent]:
        """
        Query log file with filters.

        Args:
            log_path: Path to JSONL log file
            event_type: Filter by event type
            start_time: Filter events after this time
            end_time: Filter events before this time
            task_id: Filter by task_id in details

        Returns:
            Filtered list of LogEvent objects
        """
        events = cls.read_log(log_path)

        # Apply filters
        if event_type is not None:
            if isinstance(event_type, LogEventType):
                event_type = event_type.value
            events = [e for e in events if e.event_type == event_type]

        if start_time is not None:
            events = [
                e for e in events
                if datetime.fromisoformat(e.timestamp) >= start_time
            ]

        if end_time is not None:
            events = [
                e for e in events
                if datetime.fromisoformat(e.timestamp) <= end_time
            ]

        if task_id is not None:
            events = [
                e for e in events
                if e.details.get("task_id") == task_id
            ]

        return events

    @classmethod
    def get_correlated_events(
        cls,
        log_path: Path,
        event_id: str,
    ) -> list[LogEvent]:
        """
        Get all events with the same event_id.

        Useful for finding prompt/response pairs and related events.

        Args:
            log_path: Path to JSONL log file
            event_id: Event ID to search for

        Returns:
            List of correlated LogEvent objects
        """
        events = cls.read_log(log_path)
        return [e for e in events if e.event_id == event_id]
