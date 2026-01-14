"""
Backend abstraction layer for AI model interactions.

Provides a unified interface for executing prompts across different
AI backends (Claude Code CLI, GitHub Copilot, etc.) with automatic
failover and rate limit handling.
"""

from .base import (
    Backend,
    BackendType,
    BackendConfig,
    BackendResponse,
    ToolPermission,
)

__all__ = [
    "Backend",
    "BackendType",
    "BackendConfig",
    "BackendResponse",
    "ToolPermission",
]
