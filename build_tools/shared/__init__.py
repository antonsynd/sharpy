"""
Shared utilities for Sharpy build tools.

This module provides common functionality used across multiple build tools:
- Backend abstraction for AI models (Claude, Copilot, etc.)
- Rate limiting detection and handling
- Model selection based on task complexity
- Configuration management
- Execution logging
"""

from .config import BaseConfig
from .model_selector import (
    TaskComplexity,
    TaskType,
    ModelRecommendation,
    ModelSelector,
    HAIKU,
    SONNET,
    OPUS,
)

__all__ = [
    # Configuration
    "BaseConfig",
    # Model selection
    "TaskComplexity",
    "TaskType",
    "ModelRecommendation",
    "ModelSelector",
    "HAIKU",
    "SONNET",
    "OPUS",
]
