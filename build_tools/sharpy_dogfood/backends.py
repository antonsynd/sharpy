"""
AI Backend implementations for code generation and validation.

Supports GitHub Copilot CLI and Claude Code with rate limiting and failover.

This module has been refactored to use shared backends from build_tools.shared.backends:
- ClaudeCodeBackend: Claude Code CLI with heartbeat logging
- CopilotBackend: GitHub Copilot CLI with heartbeat logging
- BackendManager: Automatic failover and rate limiting
- ExecutionLogger: JSONL-based execution logging

The shared backends provide:
- Rate limit detection and automatic backoff
- Heartbeat logging for long-running operations (visibility into active processes)
- Timeout handling with process termination
- Tool permission enforcement
"""

import asyncio
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Optional, Callable

# Import shared backends (with heartbeat support)
from build_tools.shared.backends import (
    ClaudeCodeBackend as SharedClaudeBackend,
    CopilotBackend as SharedCopilotBackend,
    BackendConfig as SharedBackendConfig,
    BackendResponse,
    ToolPermission,
)
from build_tools.shared.rate_limiting import RateLimitState
from build_tools.shared.logging import ExecutionLogger, LogEventType

from .config import Config, BackendConfig, RateLimitConfig, BackendType


@dataclass
class ExecutionResult:
    """Result of executing an AI prompt.

    This is a dogfood-specific result type that wraps execution results
    with additional fields like timed_out. The shared BackendResponse
    provides the core response fields.
    """

    success: bool
    output: str
    error: Optional[str] = None
    duration_seconds: float = 0.0
    backend: str = ""
    rate_limited: bool = False
    timed_out: bool = False

    @classmethod
    def from_backend_response(
        cls, response: BackendResponse, backend_name: str
    ) -> "ExecutionResult":
        """Convert a shared BackendResponse to ExecutionResult."""
        return cls(
            success=response.success,
            output=response.output,
            error=response.error_message,
            duration_seconds=response.duration_seconds,
            backend=backend_name,
            rate_limited=response.rate_limited,
            timed_out="timed out" in (response.error_message or "").lower(),
        )


def create_heartbeat_callback(backend_name: str) -> Callable[[str], None]:
    """Create a heartbeat callback that logs to stderr.

    This provides visibility into long-running AI backend operations,
    matching the behavior of sharpy_auto_builder.

    Args:
        backend_name: Name of the backend for log messages

    Returns:
        Callback function that prints heartbeat messages
    """

    def heartbeat_callback(message: str) -> None:
        print(f"[{backend_name}] {message}", file=sys.stderr)

    return heartbeat_callback


class DogfoodRateLimitState(RateLimitState):
    """
    Rate limit state adapter for the dogfood tool.

    Extends the shared RateLimitState with methods that accept
    the dogfood-specific RateLimitConfig.
    """

    def record_error_with_config(self, config: RateLimitConfig) -> None:
        """Record a failed request using local config parameters."""
        self.record_error(
            wait_seconds=None,
            base_cooldown=config.request_cooldown,
            max_backoff=config.max_backoff,
            multiplier=config.backoff_multiplier,
        )

    def should_wait_with_config(self, config: RateLimitConfig) -> tuple[bool, float]:
        """Check if we should wait before making a request using local config."""
        now = time.time()

        # Check if disabled
        if self.disabled_until and now < self.disabled_until:
            return True, self.disabled_until - now

        # Check rate limit window
        requests_in_window = self.requests_in_window(config.window_seconds)
        if requests_in_window >= config.max_requests_per_window:
            # Find oldest request in window to calculate wait time
            if self.request_times:
                oldest_in_window = min(
                    t for t in self.request_times if t > now - config.window_seconds
                )
                wait_time = oldest_in_window + config.window_seconds - now
                return True, max(0.0, wait_time)

        # Check cooldown with backoff
        # Note: backoff parameters are applied in record_error_with_config(),
        # get_backoff_delay() returns the pre-computed backoff_multiplier
        if self.last_request_time:
            backoff_delay = self.get_backoff_delay()
            cooldown_remaining = (
                self.last_request_time + config.request_cooldown + backoff_delay - now
            )
            if cooldown_remaining > 0:
                return True, cooldown_remaining

        return False, 0.0


class ClaudeBackend:
    """Backend adapter for Claude Code CLI using shared implementation.

    Wraps the shared ClaudeCodeBackend to provide the dogfood-specific
    interface while leveraging heartbeat logging and rate limiting.
    """

    def __init__(self, config: BackendConfig, project_root: Path):
        self.config = config
        self.project_root = project_root
        self.rate_limit_state = DogfoodRateLimitState()

        # Create the shared backend with heartbeat callback
        self._shared_backend = SharedClaudeBackend(
            rate_limit_state=self.rate_limit_state,
            heartbeat_callback=create_heartbeat_callback("claude"),
            cli_path=config.claude_cli_path,
            project_root=project_root,
        )

    @property
    def name(self) -> str:
        return self.config.name

    @property
    def is_available(self) -> bool:
        """Check if this backend is currently available.

        Checks both the shared backend (CLI exists, disabled_until) and the
        dogfood-specific rate limit state (cooldown, backoff, window limits).
        """
        if not self.config.enabled:
            return False
        if not self._shared_backend.is_available():
            return False
        # Also check dogfood-specific rate limit (cooldown + backoff)
        should_wait, _ = self.rate_limit_state.should_wait_with_config(
            self.config.rate_limit
        )
        return not should_wait

    def get_wait_time(self) -> float:
        """Get time to wait before next request."""
        _, wait_time = self.rate_limit_state.should_wait_with_config(
            self.config.rate_limit
        )
        return wait_time

    async def wait_for_availability(self) -> None:
        """Wait until this backend is available."""
        should_wait, wait_time = self.rate_limit_state.should_wait_with_config(
            self.config.rate_limit
        )
        if should_wait and wait_time > 0:
            print(
                f"[{self.name}] Waiting {wait_time:.1f}s for rate limit...",
                file=sys.stderr,
            )
            await asyncio.sleep(wait_time)

    async def execute(
        self, prompt: str, timeout: Optional[float] = None
    ) -> ExecutionResult:
        """Execute a prompt using Claude Code CLI with heartbeat logging.

        Note: Does NOT wait for rate limit availability — the BackendManager
        controls failover/wait logic so it can try other backends first.
        """
        # Build shared config
        effective_timeout = timeout or self.config.execution_timeout
        shared_config = SharedBackendConfig(
            timeout_seconds=int(effective_timeout),
            allowed_tools={ToolPermission.READ},  # Read-only for validation
            model=self.config.model,  # Use model from backend config
        )

        # Execute via shared backend (includes heartbeat logging)
        response = await self._shared_backend.execute(prompt, shared_config)

        # Update our rate limit state based on response
        if response.rate_limited:
            self.rate_limit_state.record_error_with_config(self.config.rate_limit)

        return ExecutionResult.from_backend_response(response, self.name)


class CopilotBackend:
    """Backend adapter for GitHub Copilot CLI using shared implementation.

    Wraps the shared CopilotBackend to provide the dogfood-specific
    interface while leveraging heartbeat logging and rate limiting.
    """

    def __init__(self, config: BackendConfig, project_root: Path):
        self.config = config
        self.project_root = project_root
        self.rate_limit_state = DogfoodRateLimitState()

        # Create the shared backend with heartbeat callback
        self._shared_backend = SharedCopilotBackend(
            rate_limit_state=self.rate_limit_state,
            heartbeat_callback=create_heartbeat_callback("copilot"),
            cli_path=config.copilot_cli_path,
            project_root=project_root,
        )

    @property
    def name(self) -> str:
        return self.config.name

    @property
    def is_available(self) -> bool:
        """Check if this backend is currently available.

        Checks both the shared backend (CLI exists, disabled_until) and the
        dogfood-specific rate limit state (cooldown, backoff, window limits).
        """
        if not self.config.enabled:
            return False
        if not self._shared_backend.is_available():
            return False
        # Also check dogfood-specific rate limit (cooldown + backoff)
        should_wait, _ = self.rate_limit_state.should_wait_with_config(
            self.config.rate_limit
        )
        return not should_wait

    def get_wait_time(self) -> float:
        """Get time to wait before next request."""
        _, wait_time = self.rate_limit_state.should_wait_with_config(
            self.config.rate_limit
        )
        return wait_time

    async def wait_for_availability(self) -> None:
        """Wait until this backend is available."""
        should_wait, wait_time = self.rate_limit_state.should_wait_with_config(
            self.config.rate_limit
        )
        if should_wait and wait_time > 0:
            print(
                f"[{self.name}] Waiting {wait_time:.1f}s for rate limit...",
                file=sys.stderr,
            )
            await asyncio.sleep(wait_time)

    async def execute(
        self, prompt: str, timeout: Optional[float] = None
    ) -> ExecutionResult:
        """Execute a prompt using GitHub Copilot CLI with heartbeat logging.

        Note: Does NOT wait for rate limit availability — the BackendManager
        controls failover/wait logic so it can try other backends first.
        """
        # Build shared config
        effective_timeout = timeout or self.config.execution_timeout
        shared_config = SharedBackendConfig(
            timeout_seconds=int(effective_timeout),
            allowed_tools={ToolPermission.READ},  # Read-only for validation
            model=self.config.model,  # Use model from backend config
        )

        # Execute via shared backend (includes heartbeat logging)
        response = await self._shared_backend.execute(prompt, shared_config)

        # Update our rate limit state based on response
        if response.rate_limited:
            self.rate_limit_state.record_error_with_config(self.config.rate_limit)

        return ExecutionResult.from_backend_response(response, self.name)


# Type alias for any dogfood backend
DogfoodBackend = ClaudeBackend | CopilotBackend


class BackendManager:
    """Manages multiple AI backends with automatic failover."""

    BACKEND_PRIORITY: list[BackendType] = ["claude", "copilot"]

    def __init__(self, config: Config):
        self.config = config
        self.backends: dict[str, DogfoodBackend] = {}
        self._init_backends()

    def _init_backends(self) -> None:
        """Initialize available backends."""
        for name in self.BACKEND_PRIORITY:
            backend_config = self.config.backends.get(name)
            if backend_config and backend_config.enabled:
                if name == "claude":
                    self.backends[name] = ClaudeBackend(
                        backend_config, self.config.project_root
                    )
                if name == "copilot":
                    self.backends[name] = CopilotBackend(
                        backend_config, self.config.project_root
                    )

    def get_available_backend(self) -> Optional[DogfoodBackend]:
        """Get the best available backend."""
        for name in self.BACKEND_PRIORITY:
            if name in self.backends and self.backends[name].is_available:
                return self.backends[name]
        return None

    async def execute(
        self, prompt: str, timeout: Optional[float] = None, max_retries: int = 3
    ) -> ExecutionResult:
        """Execute prompt using the best available backend with failover and retry.

        Args:
            prompt: The prompt to execute
            timeout: Optional timeout in seconds
            max_retries: Maximum number of retry attempts when all backends are unavailable

        Returns:
            ExecutionResult with success/failure status
        """
        retry_backoff = 5.0  # Initial backoff in seconds
        max_backoff = 60.0  # Maximum backoff time

        for retry in range(max_retries + 1):
            if retry > 0:
                # Wait with exponential backoff before retry
                wait_time = min(retry_backoff * (2 ** (retry - 1)), max_backoff)
                print(
                    f"[Retry {retry}/{max_retries}] Waiting {wait_time:.1f}s before retry...",
                    file=sys.stderr,
                )
                await asyncio.sleep(wait_time)

            for name in self.BACKEND_PRIORITY:
                if name not in self.backends:
                    continue

                backend = self.backends[name]
                if not backend.is_available:
                    wait_time = backend.get_wait_time()
                    print(
                        f"[{name}] Not available, wait time: {wait_time:.1f}s",
                        file=sys.stderr,
                    )
                    continue

                print(f"[{name}] Executing prompt...", file=sys.stderr)
                result = await backend.execute(prompt, timeout)

                if result.success:
                    return result

                if not result.rate_limited:
                    # Failed but not rate limited - this is a real error, return it
                    return result

                # Rate limited, try next backend
                print(f"[{name}] Rate limited, trying next backend...", file=sys.stderr)

            # Check if any backend might become available soon
            min_wait = float("inf")
            for name in self.BACKEND_PRIORITY:
                if name in self.backends:
                    wait = self.backends[name].get_wait_time()
                    if wait < min_wait:
                        min_wait = wait

            if min_wait < float("inf") and min_wait > 0 and retry < max_retries:
                # A backend will become available — wait for it, capped at max_backoff
                actual_wait = min(min_wait, max_backoff)
                print(
                    f"[BackendManager] All backends busy. Waiting {actual_wait:.1f}s "
                    f"(backend available in {min_wait:.1f}s)...",
                    file=sys.stderr,
                )
                await asyncio.sleep(actual_wait)

        # All backends exhausted after retries
        return ExecutionResult(
            success=False,
            output="",
            error=f"All backends are unavailable or rate limited after {max_retries} retries",
            backend="none",
        )

    def check_availability(self) -> dict[str, bool]:
        """Check which backends are available."""
        return {name: backend.is_available for name, backend in self.backends.items()}
