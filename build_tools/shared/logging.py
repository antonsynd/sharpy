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


class ExecutionLogger:
    """
    JSONL-based execution logger for debugging and analysis.

    Usage:
        logger = ExecutionLogger(Path("execution.jsonl"))

        # Log an event; the returned ID correlates related events
        event_id = logger.log(LogEventType.PROMPT_SENT, {"prompt": "..."})
        logger.log(LogEventType.RESPONSE_RECEIVED, {"success": True}, event_id)
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
