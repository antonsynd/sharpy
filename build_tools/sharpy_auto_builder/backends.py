"""
Backend implementations for executing agent tasks.

Supports GitHub Copilot CLI and Claude Code with rate limiting and failover.
Uses shared rate limiting utilities from build_tools.shared.
"""

import asyncio
import os
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

# Import shared rate limiting utilities
import sys

sys.path.insert(0, str(Path(__file__).parent.parent))
from shared.rate_limiting import (
    is_rate_limit_error,
    extract_rate_limit_wait_time,
    RateLimitState as SharedRateLimitState,
)
from shared.model_selector import TaskType, TaskComplexity


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
    timed_out: bool = (
        False  # True if execution was terminated due to timeout (e.g., infinite loop)
    )


class AutoBuilderRateLimitState:
    """
    Adapter for shared RateLimitState that works with local RateLimitConfig.

    Bridges the shared rate limit state implementation with the auto_builder's
    existing RateLimitConfig parameters. This allows using the shared state
    tracking while maintaining compatibility with the existing configuration
    structure.
    """

    def __init__(self):
        """Initialize with shared state implementation."""
        self._state = SharedRateLimitState()

    @property
    def request_times(self) -> deque:
        """Access underlying request times deque."""
        return self._state.request_times

    @property
    def consecutive_errors(self) -> int:
        """Get consecutive error count."""
        return self._state.consecutive_errors

    @property
    def current_backoff(self) -> float:
        """Get current backoff delay."""
        return self._state.backoff_multiplier

    @property
    def last_request_time(self) -> Optional[float]:
        """Get last request timestamp."""
        return self._state.last_request_time

    @property
    def disabled_until(self) -> Optional[float]:
        """Get disabled until timestamp."""
        return self._state.disabled_until

    def record_request(self) -> None:
        """Record a request timestamp."""
        self._state.record_request()

    def record_success(self) -> None:
        """Record a successful request."""
        self._state.record_success()

    def record_error(self, config: RateLimitConfig) -> None:
        """Record a failed request and update backoff using config parameters."""
        self._state.record_error(
            wait_seconds=None,  # No explicit wait time
            base_cooldown=config.request_cooldown,
            max_backoff=config.max_backoff,
            multiplier=config.backoff_multiplier,
        )

    def get_requests_in_window(self, window_seconds: int) -> int:
        """Count requests in the current time window."""
        return self._state.requests_in_window(window_seconds)

    def should_wait(self, config: RateLimitConfig) -> tuple[bool, float]:
        """Check if we should wait before making a request."""
        return self._state.should_wait(
            max_requests_per_window=config.max_requests_per_window,
            window_seconds=config.window_seconds,
            request_cooldown=config.request_cooldown,
        )

    def disable_temporarily(self, seconds: float) -> None:
        """Temporarily disable this backend."""
        self._state.disable_temporarily(seconds)


# Alias for backwards compatibility
RateLimitState = AutoBuilderRateLimitState


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
        self,
        prompt: str,
        context: dict[str, Any] | None = None,
        timeout: Optional[float] = None,
    ) -> ExecutionResult:
        """Execute a prompt and return the result.

        Args:
            prompt: The prompt to execute
            context: Optional context dictionary
            timeout: Maximum seconds to wait for execution (None uses backend default)

        Returns:
            ExecutionResult with timed_out=True if timeout was exceeded
        """
        pass

    @abstractmethod
    async def execute_command(
        self,
        command: str,
        cwd: Optional[Path] = None,
        env_override: dict[str, str] | None = None,
        timeout: Optional[float] = None,
    ) -> ExecutionResult:
        """Execute a shell command.

        Args:
            command: The shell command to execute
            cwd: Working directory for the command
            env_override: Environment variables to override
            timeout: Maximum seconds to wait for command completion (None for no timeout)

        Returns:
            ExecutionResult with timed_out=True if timeout was exceeded
        """
        pass


class ClaudeCodeBackend(Backend):
    """Backend for Claude Code CLI."""

    def __init__(self, config: BackendConfig, project_root: Path):
        super().__init__(config, project_root)
        self.claude_code_path = config.claude_code_path or "claude"
        # Default timeout of 10 minutes if not specified in backend config
        self.execution_timeout = config.execution_timeout or 600.0
        self.heartbeat_interval = 60.0  # Log heartbeat every 60 seconds

    async def execute(
        self,
        prompt: str,
        context: dict[str, Any] | None = None,
        timeout: Optional[float] = None,
    ) -> ExecutionResult:
        """Execute a prompt using Claude Code with timeout protection.

        Args:
            prompt: The prompt to send to Claude Code
            context: Optional context dictionary
            timeout: Override timeout in seconds (None uses default)
        """
        await self.wait_for_availability()
        self.rate_limit_state.record_request()

        start_time = time.time()
        effective_timeout = timeout or self.execution_timeout

        try:
            # Build the command
            # Claude Code CLI: pass prompt via stdin for reliability with long/complex prompts
            # --print (-p): Non-interactive output mode
            # --allowedTools: Restrict to specific tools for safety
            cmd = [
                self.claude_code_path,
                "--print",
                "--allowedTools",
                "Read,Write,Edit,Bash",  # Need all tools for implementation
            ]

            # Add permission mode if configured
            # This allows validation agents to run tests without permission prompts
            if self.config.permission_mode:
                cmd.extend(["--permission-mode", self.config.permission_mode])

            # Run Claude Code with prompt via stdin
            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdin=asyncio.subprocess.PIPE,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=self.project_root,
            )

            # Execute with timeout and heartbeat logging
            try:
                stdout, stderr = await asyncio.wait_for(
                    self._communicate_with_heartbeat(
                        process, prompt.encode(), start_time
                    ),
                    timeout=effective_timeout,
                )
            except asyncio.TimeoutError:
                # Kill the process on timeout
                duration = time.time() - start_time
                try:
                    process.kill()
                    await process.wait()
                except Exception:
                    pass  # Process may have already exited

                self.rate_limit_state.record_error(self.config.rate_limit)
                return ExecutionResult(
                    success=False,
                    output="",
                    error=f"Agent execution timed out after {duration:.1f}s (limit: {effective_timeout:.0f}s). "
                    f"The Claude Code process was killed. This may indicate a hung API call or network issue.",
                    duration_seconds=duration,
                    backend=self.name,
                    timed_out=True,
                )

            duration = time.time() - start_time
            stdout_text = stdout.decode()
            stderr_text = stderr.decode()

            # Check for rate limiting using shared detection function
            rate_limited = is_rate_limit_error(stdout_text, stderr_text)

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
                    # Extract wait time if available
                    wait_time = extract_rate_limit_wait_time(stdout_text, stderr_text)
                    if wait_time:
                        self.rate_limit_state.disable_temporarily(wait_time)
                    self.rate_limit_state.record_error(self.config.rate_limit)

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

    async def _communicate_with_heartbeat(
        self, process: asyncio.subprocess.Process, input_data: bytes, start_time: float
    ) -> tuple[bytes, bytes]:
        """Communicate with process while logging periodic heartbeats.

        This helps track long-running operations and provides visibility
        into whether the process is still active.
        """
        import sys

        # Send input and close stdin
        if process.stdin:
            process.stdin.write(input_data)
            await process.stdin.drain()
            process.stdin.close()

        # Read stdout and stderr concurrently with heartbeat logging
        async def read_with_heartbeat():
            last_heartbeat = time.time()
            stdout_chunks = []
            stderr_chunks = []

            while True:
                # Check if process has finished
                if process.returncode is not None:
                    break

                # Try to read available output (non-blocking check)
                done, pending = await asyncio.wait(
                    [
                        asyncio.create_task(
                            process.stdout.read(4096)
                            if process.stdout
                            else asyncio.sleep(0)
                        ),
                        asyncio.create_task(
                            process.stderr.read(4096)
                            if process.stderr
                            else asyncio.sleep(0)
                        ),
                    ],
                    timeout=self.heartbeat_interval,
                    return_when=asyncio.FIRST_COMPLETED,
                )

                # Collect any completed reads
                for task in done:
                    try:
                        data = task.result()
                        if data:
                            # Determine which stream this came from based on task
                            # This is a simplified approach - we append to both and dedupe later
                            if isinstance(data, bytes):
                                stdout_chunks.append(data)
                    except Exception:
                        pass

                # Cancel pending tasks
                for task in pending:
                    task.cancel()
                    try:
                        await task
                    except asyncio.CancelledError:
                        pass

                # Log heartbeat if enough time has passed
                now = time.time()
                if now - last_heartbeat >= self.heartbeat_interval:
                    elapsed = now - start_time
                    print(
                        f"[heartbeat] Agent still running... ({elapsed:.0f}s elapsed)",
                        file=sys.stderr,
                    )
                    last_heartbeat = now

                # Check if process finished
                try:
                    await asyncio.wait_for(process.wait(), timeout=0.1)
                    break
                except asyncio.TimeoutError:
                    continue

            # Read any remaining output
            if process.stdout:
                remaining = await process.stdout.read()
                if remaining:
                    stdout_chunks.append(remaining)
            if process.stderr:
                remaining = await process.stderr.read()
                if remaining:
                    stderr_chunks.append(remaining)

            return b"".join(stdout_chunks), b"".join(stderr_chunks)

        # Simpler approach: just use communicate with periodic heartbeat in parallel
        async def heartbeat_logger():
            last_log = time.time()
            while True:
                await asyncio.sleep(self.heartbeat_interval)
                elapsed = time.time() - start_time
                print(
                    f"[heartbeat] Agent still running... ({elapsed:.0f}s elapsed)",
                    file=sys.stderr,
                )

        heartbeat_task = asyncio.create_task(heartbeat_logger())
        try:
            stdout, stderr = await process.communicate()
            return stdout, stderr
        finally:
            heartbeat_task.cancel()
            try:
                await heartbeat_task
            except asyncio.CancelledError:
                pass

    async def execute_command(
        self,
        command: str,
        cwd: Optional[Path] = None,
        env_override: dict[str, str] | None = None,
        timeout: Optional[float] = None,
    ) -> ExecutionResult:
        """Execute a shell command with optional timeout.

        Args:
            command: The shell command to execute
            cwd: Working directory for the command
            env_override: Environment variables to override
            timeout: Maximum seconds to wait (None for no timeout)

        Returns:
            ExecutionResult with timed_out=True if timeout was exceeded
        """
        import os

        start_time = time.time()

        # Merge environment overrides with current environment
        env = os.environ.copy()
        if env_override:
            env.update(env_override)

        try:
            process = await asyncio.create_subprocess_shell(
                command,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=cwd or self.project_root,
                env=env,
            )

            try:
                stdout, stderr = await asyncio.wait_for(
                    process.communicate(), timeout=timeout
                )
                duration = time.time() - start_time

                return ExecutionResult(
                    success=process.returncode == 0,
                    output=stdout.decode(),
                    error=stderr.decode() if process.returncode != 0 else None,
                    duration_seconds=duration,
                    backend=self.name,
                )
            except asyncio.TimeoutError:
                # Kill the process tree on timeout
                duration = time.time() - start_time
                try:
                    process.kill()
                    await process.wait()
                except Exception:
                    pass  # Process may have already exited

                return ExecutionResult(
                    success=False,
                    output="",
                    error=f"Command timed out after {timeout:.1f} seconds. This may indicate an infinite loop in the code.",
                    duration_seconds=duration,
                    backend=self.name,
                    timed_out=True,
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
    """Backend for GitHub Copilot CLI (standalone `copilot` command).

    This uses the standalone Copilot CLI tool (not `gh copilot` which is limited
    to shell command suggestions). The standalone CLI supports:
    - --prompt: Execute arbitrary prompts
    - --allow-tool: Explicitly allow specific tools (read, write, edit, bash)

    Security: Only the specified tools are allowed, preventing unintended
    file operations or command execution.
    """

    def __init__(self, config: BackendConfig, project_root: Path):
        super().__init__(config, project_root)
        # Use standalone 'copilot' CLI, not 'gh copilot' which is for shell suggestions only
        self.copilot_cli_path = config.copilot_cli_path or "copilot"
        # Default timeout of 10 minutes if not specified in backend config
        self.execution_timeout = config.execution_timeout or 600.0

    async def execute(
        self,
        prompt: str,
        context: dict[str, Any] | None = None,
        timeout: Optional[float] = None,
    ) -> ExecutionResult:
        """Execute a prompt using GitHub Copilot CLI with timeout protection.

        Uses the standalone `copilot` CLI with explicit tool permissions.
        """
        await self.wait_for_availability()
        self.rate_limit_state.record_request()

        start_time = time.time()
        effective_timeout = timeout or self.execution_timeout

        try:
            # Copilot CLI flags for non-interactive automated execution:
            # --prompt: The prompt to execute
            # --allow-all-tools: Allow all tools without confirmation (required for non-interactive)
            # --allow-all-paths: Disable file path verification
            # --add-dir: Add project root to allowed directories
            cmd = [
                self.copilot_cli_path,
                "--prompt",
                prompt,
                "--allow-all-tools",
                "--allow-all-paths",
                "--add-dir",
                str(self.project_root),
            ]

            # Run Copilot CLI
            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=self.project_root,
            )

            try:
                stdout, stderr = await asyncio.wait_for(
                    process.communicate(), timeout=effective_timeout
                )
            except asyncio.TimeoutError:
                # Kill the process on timeout
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
                    error=f"Agent execution timed out after {duration:.1f}s (limit: {effective_timeout:.0f}s).",
                    duration_seconds=duration,
                    backend=self.name,
                    timed_out=True,
                )

            duration = time.time() - start_time

            output = stdout.decode()
            error_output = stderr.decode()

            # Copilot CLI often returns exit code 0 but with prompts/errors in output
            # Check if it actually produced useful output
            if process.returncode == 0 and output and "?" not in output[:50]:
                self.rate_limit_state.record_success()
                return ExecutionResult(
                    success=True,
                    output=output,
                    duration_seconds=duration,
                    backend=self.name,
                )
            else:
                # Copilot CLI is interactive and likely needs user input
                error_msg = (
                    error_output
                    or "GitHub Copilot CLI requires interactive input. Consider using Claude Code backend for code generation tasks."
                )
                # Use shared rate limit detection function
                rate_limited = is_rate_limit_error(output, error_output)

                if rate_limited:
                    # Extract wait time if available
                    wait_time = extract_rate_limit_wait_time(output, error_output)
                    if wait_time:
                        self.rate_limit_state.disable_temporarily(wait_time)
                    self.rate_limit_state.record_error(self.config.rate_limit)

                return ExecutionResult(
                    success=False,
                    output=output,
                    error=error_msg,
                    duration_seconds=duration,
                    backend=self.name,
                    rate_limited=rate_limited,
                )

        except FileNotFoundError:
            return ExecutionResult(
                success=False,
                output="",
                error=f"Copilot CLI not found at: {self.copilot_cli_path}. Ensure the standalone 'copilot' CLI is installed and in PATH.",
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
        self,
        command: str,
        cwd: Optional[Path] = None,
        env_override: dict[str, str] | None = None,
        timeout: Optional[float] = None,
    ) -> ExecutionResult:
        """Execute a shell command with optional timeout.

        Args:
            command: The shell command to execute
            cwd: Working directory for the command
            env_override: Environment variables to override
            timeout: Maximum seconds to wait (None for no timeout)

        Returns:
            ExecutionResult with timed_out=True if timeout was exceeded
        """
        import os

        start_time = time.time()

        # Merge environment overrides with current environment
        env = os.environ.copy()
        if env_override:
            env.update(env_override)

        try:
            process = await asyncio.create_subprocess_shell(
                command,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=cwd or self.project_root,
                env=env,
            )

            try:
                stdout, stderr = await asyncio.wait_for(
                    process.communicate(), timeout=timeout
                )
                duration = time.time() - start_time

                return ExecutionResult(
                    success=process.returncode == 0,
                    output=stdout.decode(),
                    error=stderr.decode() if process.returncode != 0 else None,
                    duration_seconds=duration,
                    backend=self.name,
                )
            except asyncio.TimeoutError:
                # Kill the process tree on timeout
                duration = time.time() - start_time
                try:
                    process.kill()
                    await process.wait()
                except Exception:
                    pass  # Process may have already exited

                return ExecutionResult(
                    success=False,
                    output="",
                    error=f"Command timed out after {timeout:.1f} seconds. This may indicate an infinite loop in the code.",
                    duration_seconds=duration,
                    backend=self.name,
                    timed_out=True,
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
        timeout: Optional[float] = None,
    ) -> ExecutionResult:
        """Execute a prompt with automatic failover to other backends.

        Args:
            prompt: The prompt to execute
            context: Optional context dictionary
            preferred_backend: Try this backend first
            timeout: Override execution timeout (None uses backend default)
        """

        # Try preferred backend first
        if preferred_backend and preferred_backend in self.backends:
            backend = self.backends[preferred_backend]
            if backend.is_available:
                result = await backend.execute(prompt, context, timeout)
                if result.success or (not result.rate_limited and not result.timed_out):
                    return result
                # If timed out, try failover to another backend
                if result.timed_out:
                    import sys

                    print(
                        f"[failover] {backend.name} timed out, trying next backend...",
                        file=sys.stderr,
                    )

        # Try backends in priority order
        for backend_type in self.config.backend_priority:
            if backend_type in self.backends:
                backend = self.backends[backend_type]

                # Skip if we just tried this one
                if preferred_backend and backend_type == preferred_backend:
                    continue

                # Wait for availability
                await backend.wait_for_availability()

                result = await backend.execute(prompt, context, timeout)
                if result.success:
                    return result

                # If rate limited or timed out, try next backend
                if result.rate_limited or result.timed_out:
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
        env_override: dict[str, str] | None = None,
        timeout: Optional[float] = None,
    ) -> ExecutionResult:
        """Execute a shell command using any available backend.

        Args:
            command: The shell command to execute
            cwd: Working directory for the command
            backend_type: Specific backend to use (optional)
            env_override: Environment variables to override
            timeout: Maximum seconds to wait (None for no timeout)

        Returns:
            ExecutionResult with timed_out=True if timeout was exceeded
        """
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

        return await backend.execute_command(command, cwd, env_override, timeout)

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
