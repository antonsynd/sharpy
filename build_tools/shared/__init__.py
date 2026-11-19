"""
Shared utilities for Sharpy build tools.

This module provides common functionality used across multiple build tools:
- Backend abstraction for AI models (Claude, Copilot, etc.)
- Rate limiting detection and handling
- Task type and complexity taxonomy
- Configuration management
- Execution logging
- CLI command building
"""

from .config import BaseConfig
from .model_selector import (
    TaskComplexity,
    TaskType,
)
from .logging import (
    LogEvent,
    LogEventType,
    ExecutionLogger,
)
from .cli_builder import CLICommand, CLIBuilder

__all__ = [
    # Configuration
    "BaseConfig",
    # Task taxonomy
    "TaskComplexity",
    "TaskType",
    # Logging
    "LogEvent",
    "LogEventType",
    "ExecutionLogger",
    # CLI building
    "CLICommand",
    "CLIBuilder",
]
