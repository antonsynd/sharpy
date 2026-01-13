"""
AI Backend implementations for code generation and validation.

Supports GitHub Copilot CLI and Claude Code with rate limiting and failover.
"""

import asyncio
import subprocess
import time
import re
import sys
from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional, Any
from collections import deque
from datetime import datetime, timedelta

from .config import Config, BackendConfig, RateLimitConfig, BackendType


@dataclass
class ExecutionResult:
    """Result of executing an AI prompt."""

    success: bool
    output: str
    error: Optional[str] = None
    duration_seconds: float = 0.0
    backend: str = ""
    rate_limited: bool = False
    timed_out: bool = False


@dataclass
class RateLimitState:
    """Tracks rate limit state for a backend."""

    request_times: deque = field(default_factory=lambda: deque(maxlen=1000))
    consecutive_errors: int = 0
    current_backoff: float = 0.0
    last_request_time: Optional[float] = None
    disabled_until: Optional[float] = None

    def record_request(self) -> None:
        """Record a request timestamp."""
        now = time.time()
        self.request_times.append(now)
        self.last_request_time = now

    def record_success(self) -> None:
        """Record a successful request."""
        self.consecutive_errors = 0
        self.current_backoff = 0.0

    def record_error(self, config: RateLimitConfig) -> None:
        """Record a failed request and update backoff."""
        self.consecutive_errors += 1
        if self.current_backoff == 0:
            self.current_backoff = config.request_cooldown
        else:
            self.current_backoff = min(
                self.current_backoff * config.backoff_multiplier, config.max_backoff
            )

    def get_requests_in_window(self, window_seconds: int) -> int:
        """Count requests in the current time window."""
        now = time.time()
        cutoff = now - window_seconds
        return sum(1 for t in self.request_times if t > cutoff)

    def should_wait(self, config: RateLimitConfig) -> tuple[bool, float]:
        """Check if we should wait before making a request."""
        now = time.time()

        # Check if disabled
        if self.disabled_until and now < self.disabled_until:
            return True, self.disabled_until - now

        # Check rate limit
        requests_in_window = self.get_requests_in_window(config.window_seconds)
        if requests_in_window >= config.max_requests_per_window:
            oldest_in_window = min(
                t for t in self.request_times if t > now - config.window_seconds
            )
            wait_time = oldest_in_window + config.window_seconds - now
            return True, wait_time

        # Check cooldown
        if self.last_request_time:
            cooldown_remaining = (
                self.last_request_time
                + config.request_cooldown
                + self.current_backoff
                - now
            )
            if cooldown_remaining > 0:
                return True, cooldown_remaining

        return False, 0.0

    def disable_temporarily(self, seconds: float) -> None:
        """Temporarily disable this backend."""
        self.disabled_until = time.time() + seconds


def is_rate_limit_error(output: str) -> bool:
    """Check if output indicates a rate limit error."""
    indicators = [
        "rate_limited",
        "rate limit",
        "rate-limit",
        "429",
        "too many requests",
        "exceeded your copilot token usage",
        "hit your limit",
        "quota exceeded",
        "resets 2am",
        "resets at",
        "try again later",
        "request limit",
    ]
    output_lower = output.lower()
    return any(indicator in output_lower for indicator in indicators)


def extract_rate_limit_wait_time(output: str) -> Optional[float]:
    """Extract wait time from rate limit error messages."""
    output_lower = output.lower()

    # Pattern: "try again in X minutes/hours/seconds"
    match = re.search(r"try again in (\d+)\s*(second|minute|hour)s?", output_lower)
    if match:
        value = int(match.group(1))
        unit = match.group(2)
        if unit == "hour":
            return value * 3600
        elif unit == "minute":
            return value * 60
        else:
            return float(value)

    # Pattern: "wait X seconds/minutes"
    match = re.search(r"wait (\d+)\s*(second|minute)s?", output_lower)
    if match:
        value = int(match.group(1))
        unit = match.group(2)
        return value * 60 if unit == "minute" else float(value)

    # Pattern: "resets (at)? Xam/pm"
    match = re.search(r"resets\s*(?:at\s*)?(\d{1,2})([ap]m)", output_lower)
    if match:
        reset_hour = int(match.group(1))
        is_pm = match.group(2) == "pm"
        if is_pm and reset_hour != 12:
            reset_hour += 12
        elif not is_pm and reset_hour == 12:
            reset_hour = 0

        now = datetime.now()
        reset_time = now.replace(hour=reset_hour, minute=0, second=0, microsecond=0)
        if reset_time <= now:
            reset_time = reset_time + timedelta(days=1)

        wait_seconds = (reset_time - now).total_seconds()
        return max(wait_seconds, 60.0)

    return None


class Backend(ABC):
    """Abstract base class for AI execution backends."""

    def __init__(self, config: BackendConfig, project_root: Path):
        self.config = config
        self.project_root = project_root
        self.rate_limit_state = RateLimitState()

    @property
    def name(self) -> str:
        return self.config.name

    @property
    def is_available(self) -> bool:
        """Check if this backend is currently available."""
        if not self.config.enabled:
            return False
        should_wait, _ = self.rate_limit_state.should_wait(self.config.rate_limit)
        return not should_wait

    def get_wait_time(self) -> float:
        """Get time to wait before next request."""
        _, wait_time = self.rate_limit_state.should_wait(self.config.rate_limit)
        return wait_time

    async def wait_for_availability(self) -> None:
        """Wait until this backend is available."""
        should_wait, wait_time = self.rate_limit_state.should_wait(
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

                self.rate_limit_state.record_error(self.config.rate_limit)
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
                    self.rate_limit_state.record_error(self.config.rate_limit)
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
            self.rate_limit_state.record_error(self.config.rate_limit)
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

                self.rate_limit_state.record_error(self.config.rate_limit)
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
                    self.rate_limit_state.record_error(self.config.rate_limit)
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
            self.rate_limit_state.record_error(self.config.rate_limit)
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
