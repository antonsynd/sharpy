"""
Backend implementations for executing agent tasks.

Supports GitHub Copilot CLI and Claude Code with rate limiting and failover.
"""

import asyncio
import subprocess
import json
import time
from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from pathlib import Path
from typing import Optional, Any, AsyncIterator
from collections import deque

from .config import Config, BackendConfig, RateLimitConfig, BackendType


@dataclass
class ExecutionResult:
    """Result of executing a command via a backend."""

    success: bool
    output: str
    error: Optional[str] = None
    duration_seconds: float = 0.0
    backend: str = ""
    rate_limited: bool = False
    tokens_used: Optional[int] = None


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
            # Calculate when the oldest request will expire
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


class Backend(ABC):
    """Abstract base class for execution backends."""

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
            await asyncio.sleep(wait_time)

    @abstractmethod
    async def execute(
        self, prompt: str, context: dict[str, Any] | None = None
    ) -> ExecutionResult:
        """Execute a prompt and return the result."""
        pass

    @abstractmethod
    async def execute_command(
        self, command: str, cwd: Optional[Path] = None
    ) -> ExecutionResult:
        """Execute a shell command."""
        pass


class ClaudeCodeBackend(Backend):
    """Backend for Claude Code CLI."""

    def __init__(self, config: BackendConfig, project_root: Path):
        super().__init__(config, project_root)
        self.claude_code_path = config.claude_code_path or "claude"

    async def execute(
        self, prompt: str, context: dict[str, Any] | None = None
    ) -> ExecutionResult:
        """Execute a prompt using Claude Code."""
        await self.wait_for_availability()
        self.rate_limit_state.record_request()

        start_time = time.time()

        try:
            # Build the command
            cmd = [
                self.claude_code_path,
                "--print",  # Print output instead of interactive
                "--model",
                self.config.model,
            ]

            # Add context files if provided
            if context and "files" in context:
                for file_path in context["files"]:
                    cmd.extend(["--file", str(file_path)])

            # Run Claude Code
            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdin=asyncio.subprocess.PIPE,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=self.project_root,
            )

            stdout, stderr = await process.communicate(input=prompt.encode())

            duration = time.time() - start_time

            if process.returncode == 0:
                self.rate_limit_state.record_success()
                return ExecutionResult(
                    success=True,
                    output=stdout.decode(),
                    duration_seconds=duration,
                    backend=self.name,
                )
            else:
                error_msg = stderr.decode()
                rate_limited = "rate limit" in error_msg.lower()

                if rate_limited:
                    self.rate_limit_state.record_error(self.config.rate_limit)

                return ExecutionResult(
                    success=False,
                    output=stdout.decode(),
                    error=error_msg,
                    duration_seconds=duration,
                    backend=self.name,
                    rate_limited=rate_limited,
                )

        except FileNotFoundError:
            return ExecutionResult(
                success=False,
                output="",
                error=f"Claude Code CLI not found at: {self.claude_code_path}",
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

    async def execute_command(
        self, command: str, cwd: Optional[Path] = None
    ) -> ExecutionResult:
        """Execute a shell command."""
        start_time = time.time()

        try:
            process = await asyncio.create_subprocess_shell(
                command,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=cwd or self.project_root,
            )

            stdout, stderr = await process.communicate()
            duration = time.time() - start_time

            return ExecutionResult(
                success=process.returncode == 0,
                output=stdout.decode(),
                error=stderr.decode() if process.returncode != 0 else None,
                duration_seconds=duration,
                backend=self.name,
            )
        except Exception as e:
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
        self.copilot_cli_path = config.copilot_cli_path or "gh"

    async def execute(
        self, prompt: str, context: dict[str, Any] | None = None
    ) -> ExecutionResult:
        """Execute a prompt using GitHub Copilot CLI."""
        await self.wait_for_availability()
        self.rate_limit_state.record_request()

        start_time = time.time()

        try:
            # Use gh copilot suggest or gh copilot explain
            cmd = [
                self.copilot_cli_path,
                "copilot",
                "suggest",  # or "explain" depending on the task
                "-t",
                "shell",  # target type
            ]

            # Run Copilot CLI
            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdin=asyncio.subprocess.PIPE,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=self.project_root,
            )

            stdout, stderr = await process.communicate(input=prompt.encode())

            duration = time.time() - start_time

            if process.returncode == 0:
                self.rate_limit_state.record_success()
                return ExecutionResult(
                    success=True,
                    output=stdout.decode(),
                    duration_seconds=duration,
                    backend=self.name,
                )
            else:
                error_msg = stderr.decode()
                rate_limited = "rate limit" in error_msg.lower() or "429" in error_msg

                if rate_limited:
                    self.rate_limit_state.record_error(self.config.rate_limit)

                return ExecutionResult(
                    success=False,
                    output=stdout.decode(),
                    error=error_msg,
                    duration_seconds=duration,
                    backend=self.name,
                    rate_limited=rate_limited,
                )

        except FileNotFoundError:
            return ExecutionResult(
                success=False,
                output="",
                error=f"GitHub CLI not found at: {self.copilot_cli_path}",
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

    async def execute_command(
        self, command: str, cwd: Optional[Path] = None
    ) -> ExecutionResult:
        """Execute a shell command."""
        start_time = time.time()

        try:
            process = await asyncio.create_subprocess_shell(
                command,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=cwd or self.project_root,
            )

            stdout, stderr = await process.communicate()
            duration = time.time() - start_time

            return ExecutionResult(
                success=process.returncode == 0,
                output=stdout.decode(),
                error=stderr.decode() if process.returncode != 0 else None,
                duration_seconds=duration,
                backend=self.name,
            )
        except Exception as e:
            return ExecutionResult(
                success=False,
                output="",
                error=str(e),
                duration_seconds=time.time() - start_time,
                backend=self.name,
            )


class BackendManager:
    """Manages multiple backends with failover and load balancing."""

    def __init__(self, config: Config):
        self.config = config
        self.backends: dict[BackendType, Backend] = {}
        self._initialize_backends()

    def _initialize_backends(self) -> None:
        """Initialize all configured backends."""
        for backend_type, backend_config in self.config.backends.items():
            if backend_config.enabled:
                if backend_type == "claude_code":
                    self.backends[backend_type] = ClaudeCodeBackend(
                        backend_config, self.config.project_root
                    )
                elif backend_type == "copilot":
                    self.backends[backend_type] = CopilotBackend(
                        backend_config, self.config.project_root
                    )

    def get_available_backend(self) -> Optional[Backend]:
        """Get the best available backend based on priority and availability."""
        for backend_type in self.config.backend_priority:
            if backend_type in self.backends:
                backend = self.backends[backend_type]
                if backend.is_available:
                    return backend
        return None

    def get_backend(self, backend_type: BackendType) -> Optional[Backend]:
        """Get a specific backend."""
        return self.backends.get(backend_type)

    async def execute_with_failover(
        self,
        prompt: str,
        context: dict[str, Any] | None = None,
        preferred_backend: Optional[BackendType] = None,
    ) -> ExecutionResult:
        """Execute a prompt with automatic failover to other backends."""

        # Try preferred backend first
        if preferred_backend and preferred_backend in self.backends:
            backend = self.backends[preferred_backend]
            if backend.is_available:
                result = await backend.execute(prompt, context)
                if result.success or not result.rate_limited:
                    return result

        # Try backends in priority order
        for backend_type in self.config.backend_priority:
            if backend_type in self.backends:
                backend = self.backends[backend_type]

                # Skip if we just tried this one
                if preferred_backend and backend_type == preferred_backend:
                    continue

                # Wait for availability
                await backend.wait_for_availability()

                result = await backend.execute(prompt, context)
                if result.success:
                    return result

                # If rate limited, try next backend
                if result.rate_limited:
                    continue

                # For other errors, return the result
                return result

        # All backends exhausted
        return ExecutionResult(
            success=False,
            output="",
            error="All backends exhausted or rate limited",
            backend="none",
        )

    async def execute_command(
        self,
        command: str,
        cwd: Optional[Path] = None,
        backend_type: Optional[BackendType] = None,
    ) -> ExecutionResult:
        """Execute a shell command using any available backend."""
        backend = None
        if backend_type and backend_type in self.backends:
            backend = self.backends[backend_type]
        else:
            backend = self.get_available_backend()

        if not backend:
            return ExecutionResult(
                success=False,
                output="",
                error="No available backend",
                backend="none",
            )

        return await backend.execute_command(command, cwd)

    def get_status(self) -> dict[str, Any]:
        """Get status of all backends."""
        status = {}
        for backend_type, backend in self.backends.items():
            state = backend.rate_limit_state
            status[backend_type] = {
                "enabled": backend.config.enabled,
                "available": backend.is_available,
                "wait_time": backend.get_wait_time(),
                "consecutive_errors": state.consecutive_errors,
                "requests_in_window": state.get_requests_in_window(
                    backend.config.rate_limit.window_seconds
                ),
                "current_backoff": state.current_backoff,
            }
        return status
