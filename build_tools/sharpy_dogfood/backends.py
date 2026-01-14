"""
AI Backend implementations for code generation and validation.

Supports GitHub Copilot CLI and Claude Code with rate limiting and failover.

This module has been refactored to use shared utilities from build_tools.shared:
- Rate limit detection: shared.rate_limiting.is_rate_limit_error
- Wait time extraction: shared.rate_limiting.extract_rate_limit_wait_time
- Rate limit state tracking: shared.rate_limiting.RateLimitState
- Model selection: shared.model_selector.TaskType, TaskComplexity
"""

import asyncio
import sys
import time
from abc import ABC, abstractmethod
from dataclasses import dataclass
from pathlib import Path
from typing import Optional

# Import shared utilities
from build_tools.shared.rate_limiting import (
    is_rate_limit_error,
    extract_rate_limit_wait_time,
    RateLimitState,
)
from build_tools.shared.model_selector import TaskType, TaskComplexity

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


# Adapter class to bridge shared RateLimitState with local RateLimitConfig
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
        if self.last_request_time:
            backoff_delay = self.get_backoff_delay(
                base_cooldown=config.request_cooldown,
                max_backoff=config.max_backoff,
            )
            cooldown_remaining = (
                self.last_request_time + config.request_cooldown + backoff_delay - now
            )
            if cooldown_remaining > 0:
                return True, cooldown_remaining

        return False, 0.0


class Backend(ABC):
    """Abstract base class for AI execution backends."""

    def __init__(self, config: BackendConfig, project_root: Path):
        self.config = config
        self.project_root = project_root
        self.rate_limit_state = DogfoodRateLimitState()

    @property
    def name(self) -> str:
        return self.config.name

    @property
    def is_available(self) -> bool:
        """Check if this backend is currently available."""
        if not self.config.enabled:
            return False
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

    @abstractmethod
    async def execute(
        self, prompt: str, timeout: Optional[float] = None
    ) -> ExecutionResult:
        """Execute a prompt and return the result."""
        pass


class ClaudeBackend(Backend):
    """Backend for Claude Code CLI."""

    def __init__(self, config: BackendConfig, project_root: Path):
        super().__init__(config, project_root)
        self.cli_path = config.claude_cli_path or "claude"
        self.heartbeat_interval = 30.0

    async def execute(
        self, prompt: str, timeout: Optional[float] = None
    ) -> ExecutionResult:
        """Execute a prompt using Claude Code CLI."""
        await self.wait_for_availability()
        self.rate_limit_state.record_request()

        start_time = time.time()
        effective_timeout = timeout or self.config.execution_timeout

        try:
            cmd = [
                self.cli_path,
                "--print",
                "--allowedTools",
                "Read",  # Read-only for validation
            ]

            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdin=asyncio.subprocess.PIPE,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=self.project_root,
            )

            try:
                stdout, stderr = await asyncio.wait_for(
                    process.communicate(prompt.encode()), timeout=effective_timeout
                )
            except asyncio.TimeoutError:
                duration = time.time() - start_time
                try:
                    process.kill()
                    await process.wait()
                except Exception:
                    pass

                self.rate_limit_state.record_error_with_config(self.config.rate_limit)
                return ExecutionResult(
                    success=False,
                    output="",
                    error=f"Execution timed out after {duration:.1f}s (limit: {effective_timeout:.0f}s)",
                    duration_seconds=duration,
                    backend=self.name,
                    timed_out=True,
                )

            duration = time.time() - start_time
            stdout_text = stdout.decode()
            stderr_text = stderr.decode()

            combined_output = (stdout_text + stderr_text).lower()
            rate_limited = is_rate_limit_error(combined_output)

            if process.returncode == 0 and not rate_limited:
                self.rate_limit_state.record_success()
                return ExecutionResult(
                    success=True,
                    output=stdout_text,
                    duration_seconds=duration,
                    backend=self.name,
                )
            else:
                error_msg = stderr_text or stdout_text

                if rate_limited:
                    self.rate_limit_state.record_error_with_config(
                        self.config.rate_limit
                    )
                    wait_time = extract_rate_limit_wait_time(combined_output)
                    if wait_time:
                        self.rate_limit_state.disable_temporarily(wait_time)

                return ExecutionResult(
                    success=False,
                    output=stdout_text,
                    error=error_msg,
                    duration_seconds=duration,
                    backend=self.name,
                    rate_limited=rate_limited,
                )

        except FileNotFoundError:
            return ExecutionResult(
                success=False,
                output="",
                error=f"Claude CLI not found at: {self.cli_path}",
                backend=self.name,
            )
        except Exception as e:
            self.rate_limit_state.record_error_with_config(self.config.rate_limit)
            return ExecutionResult(
                success=False,
                output="",
                error=str(e),
                duration_seconds=time.time() - start_time,
                backend=self.name,
            )


class CopilotBackend(Backend):
    """Backend for GitHub Copilot CLI."""

    def __init__(self, config: BackendConfig, project_root: Path):
        super().__init__(config, project_root)
        # Try common paths for the Copilot CLI
        self.cli_path = config.copilot_cli_path or self._find_copilot_cli()

    def _find_copilot_cli(self) -> str:
        """Find the Copilot CLI executable."""
        import os

        # Check common installation locations
        candidates = [
            # VS Code global storage (macOS)
            Path.home()
            / "Library/Application Support/Code/User/globalStorage/github.copilot-chat/copilotCli/copilot",
            # Homebrew (legacy)
            Path("/opt/homebrew/bin/copilot"),
            # Standard PATH
            Path("/usr/local/bin/copilot"),
        ]
        for path in candidates:
            if path.exists():
                return str(path)

        # Fall back to assuming it's in PATH
        return "copilot"

    async def execute(
        self, prompt: str, timeout: Optional[float] = None
    ) -> ExecutionResult:
        """Execute a prompt using GitHub Copilot CLI."""
        await self.wait_for_availability()
        self.rate_limit_state.record_request()

        start_time = time.time()
        effective_timeout = timeout or self.config.execution_timeout

        try:
            # Use --prompt flag for direct prompt input (new CLI API)
            # --allow-all-tools enables non-interactive execution
            cmd = [
                self.cli_path,
                "--prompt",
                prompt,
                "--allow-all-tools",
                "--add-dir",
                str(self.project_root),
            ]

            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdin=asyncio.subprocess.PIPE,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=self.project_root,
            )

            try:
                stdout, stderr = await asyncio.wait_for(
                    process.communicate(), timeout=effective_timeout
                )
            except asyncio.TimeoutError:
                duration = time.time() - start_time
                try:
                    process.kill()
                    await process.wait()
                except Exception:
                    pass

                self.rate_limit_state.record_error_with_config(self.config.rate_limit)
                return ExecutionResult(
                    success=False,
                    output="",
                    error=f"Execution timed out after {duration:.1f}s",
                    duration_seconds=duration,
                    backend=self.name,
                    timed_out=True,
                )

            duration = time.time() - start_time
            stdout_text = stdout.decode()
            stderr_text = stderr.decode()

            combined_output = (stdout_text + stderr_text).lower()
            rate_limited = is_rate_limit_error(combined_output)

            if process.returncode == 0 and not rate_limited:
                self.rate_limit_state.record_success()
                return ExecutionResult(
                    success=True,
                    output=stdout_text,
                    duration_seconds=duration,
                    backend=self.name,
                )
            else:
                error_msg = stderr_text or stdout_text

                if rate_limited:
                    self.rate_limit_state.record_error_with_config(
                        self.config.rate_limit
                    )
                    wait_time = extract_rate_limit_wait_time(combined_output)
                    if wait_time:
                        self.rate_limit_state.disable_temporarily(wait_time)

                return ExecutionResult(
                    success=False,
                    output=stdout_text,
                    error=error_msg,
                    duration_seconds=duration,
                    backend=self.name,
                    rate_limited=rate_limited,
                )

        except FileNotFoundError:
            return ExecutionResult(
                success=False,
                output="",
                error=f"Copilot CLI not found at: {self.cli_path}",
                backend=self.name,
            )
        except Exception as e:
            self.rate_limit_state.record_error_with_config(self.config.rate_limit)
            return ExecutionResult(
                success=False,
                output="",
                error=str(e),
                duration_seconds=time.time() - start_time,
                backend=self.name,
            )


class BackendManager:
    """Manages multiple AI backends with automatic failover."""

    BACKEND_PRIORITY = ["claude", "copilot"]

    def __init__(self, config: Config):
        self.config = config
        self.backends: dict[str, Backend] = {}
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
                elif name == "copilot":
                    self.backends[name] = CopilotBackend(
                        backend_config, self.config.project_root
                    )

    def get_available_backend(self) -> Optional[Backend]:
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
                # A backend will become available - wait for it
                print(
                    f"[BackendManager] Backend will be available in {min_wait:.1f}s",
                    file=sys.stderr,
                )

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
