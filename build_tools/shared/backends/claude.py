"""
Claude Code CLI backend implementation.

Provides execution via the Claude Code CLI with rate limiting,
timeout handling, and heartbeat logging for long-running operations.
"""

import asyncio
import os
import shutil
import time
from pathlib import Path
from typing import Optional, Callable

from .base import Backend, BackendType, BackendConfig, BackendResponse, ToolPermission
from ..rate_limiting import (
    RateLimitState,
    is_rate_limit_error,
    extract_rate_limit_wait_time,
)


class ClaudeCodeBackend(Backend):
    """Backend for Claude Code CLI execution.

    Executes prompts via the Claude Code CLI with:
    - Rate limit detection and automatic backoff
    - Configurable timeouts with process termination
    - Heartbeat logging for long-running operations
    - Tool permission enforcement

    Example:
        ```python
        backend = ClaudeCodeBackend()
        config = BackendConfig(
            timeout_seconds=600,
            allowed_tools={ToolPermission.READ, ToolPermission.WRITE},
        )
        response = await backend.execute("Generate a function...", config)
        ```
    """

    def __init__(
        self,
        rate_limit_state: Optional[RateLimitState] = None,
        heartbeat_callback: Optional[Callable[[str], None]] = None,
        cli_path: Optional[str] = None,
        project_root: Optional[Path] = None,
    ):
        """Initialize Claude Code backend.

        Args:
            rate_limit_state: Optional existing rate limit state to use.
                If None, creates a new one.
            heartbeat_callback: Optional callback for heartbeat messages during
                long operations. Called with status messages.
            cli_path: Path to claude CLI executable. If None, searches PATH.
            project_root: Working directory for execution. If None, uses cwd.
        """
        self._rate_limit_state = rate_limit_state or RateLimitState()
        self._heartbeat_callback = heartbeat_callback
        self._cli_path = cli_path or self._find_claude_cli()
        self._project_root = project_root or Path.cwd()
        self._heartbeat_interval = 60.0  # Log heartbeat every 60 seconds

    @property
    def backend_type(self) -> BackendType:
        """Get backend type identifier."""
        return BackendType.CLAUDE_CODE

    def is_available(self) -> bool:
        """Check if Claude Code CLI is available and not rate limited.

        Returns:
            True if the backend can accept requests, False if rate limited
            or CLI not found.
        """
        # Check if CLI exists
        if not self._cli_path or not shutil.which(self._cli_path):
            return False

        # Check if rate limited
        return self._rate_limit_state.is_available()

    async def execute(
        self, prompt: str, config: Optional[BackendConfig] = None
    ) -> BackendResponse:
        """Execute a prompt via Claude Code CLI.

        Args:
            prompt: The instruction/prompt to send to Claude Code
            config: Optional execution configuration

        Returns:
            BackendResponse with execution results
        """
        # Use default config if none provided
        if config is None:
            config = BackendConfig()

        # Wait if rate limited (check simple availability without per-request params)
        wait_time = self._rate_limit_state.get_wait_time()
        if wait_time is not None and wait_time > 0:
            if self._heartbeat_callback:
                self._heartbeat_callback(
                    f"Rate limited - waiting {wait_time:.1f}s before retry"
                )
            await asyncio.sleep(wait_time)

        # Record request attempt
        self._rate_limit_state.record_request()
        start_time = time.time()

        try:
            # Build command
            cmd = self._build_command(config)

            # Execute with timeout and heartbeat logging
            # Strip CLAUDECODE to avoid "cannot be launched inside another
            # claude code session" errors when this process is itself running
            # under Claude Code.
            clean_env = {k: v for k, v in os.environ.items() if k != "CLAUDECODE"}
            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdin=asyncio.subprocess.PIPE,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=self._project_root,
                env=clean_env,
            )

            try:
                stdout, stderr = await asyncio.wait_for(
                    self._communicate_with_heartbeat(
                        process, prompt.encode(), start_time
                    ),
                    timeout=config.timeout_seconds,
                )
            except asyncio.TimeoutError:
                # Kill the process on timeout
                duration = time.time() - start_time
                try:
                    process.kill()
                    await process.wait()
                except Exception:
                    pass  # Process may have already exited

                self._rate_limit_state.record_error()
                return BackendResponse(
                    success=False,
                    output="",
                    stderr="",
                    exit_code=-1,
                    duration_seconds=duration,
                    rate_limited=False,
                    error_message=(
                        f"Execution timed out after {duration:.1f}s "
                        f"(limit: {config.timeout_seconds:.0f}s). "
                        f"Process was terminated."
                    ),
                )

            duration = time.time() - start_time
            stdout_text = stdout.decode(errors="replace")
            stderr_text = stderr.decode(errors="replace")

            # Check for rate limiting
            rate_limited = is_rate_limit_error(stdout_text, stderr_text)

            if rate_limited:
                # Extract wait time and update rate limit state
                wait_seconds = extract_rate_limit_wait_time(stdout_text, stderr_text)
                self._rate_limit_state.record_error(wait_seconds)

                return BackendResponse(
                    success=False,
                    output=stdout_text,
                    stderr=stderr_text,
                    exit_code=process.returncode or 1,
                    duration_seconds=duration,
                    rate_limited=True,
                    error_message=(
                        f"Rate limited. Wait {wait_seconds}s before retry."
                        if wait_seconds
                        else "Rate limited."
                    ),
                )

            # Check for success
            if process.returncode == 0:
                self._rate_limit_state.record_success()
                return BackendResponse(
                    success=True,
                    output=stdout_text,
                    stderr=stderr_text,
                    exit_code=0,
                    duration_seconds=duration,
                    rate_limited=False,
                    error_message=None,
                )
            else:
                # Non-zero exit code but not rate limited
                self._rate_limit_state.record_error()
                error_msg = stderr_text or stdout_text or "Unknown error"
                return BackendResponse(
                    success=False,
                    output=stdout_text,
                    stderr=stderr_text,
                    exit_code=process.returncode,
                    duration_seconds=duration,
                    rate_limited=False,
                    error_message=f"Execution failed: {error_msg}",
                )

        except FileNotFoundError:
            return BackendResponse(
                success=False,
                output="",
                stderr="",
                exit_code=-1,
                duration_seconds=time.time() - start_time,
                rate_limited=False,
                error_message=f"Claude Code CLI not found at: {self._cli_path}",
            )
        except Exception as e:
            self._rate_limit_state.record_error()
            return BackendResponse(
                success=False,
                output="",
                stderr="",
                exit_code=-1,
                duration_seconds=time.time() - start_time,
                rate_limited=False,
                error_message=f"Unexpected error: {str(e)}",
            )

    def _build_command(self, config: BackendConfig) -> list[str]:
        """Build the Claude Code CLI command.

        Args:
            config: Execution configuration

        Returns:
            Command as list of arguments
        """
        cmd = [self._cli_path, "--print"]

        # Add tool permissions
        if config.allowed_tools:
            tools_str = ",".join(t.value for t in config.allowed_tools)
            cmd.extend(["--allowedTools", tools_str])

        # Add model if specified
        if config.model:
            cmd.extend(["--model", config.model])

        return cmd

    async def _communicate_with_heartbeat(
        self, process: asyncio.subprocess.Process, input_data: bytes, start_time: float
    ) -> tuple[bytes, bytes]:
        """Communicate with process while logging periodic heartbeats.

        This helps track long-running operations and provides visibility
        into whether the process is still active.

        Args:
            process: The subprocess to communicate with
            input_data: Data to send to stdin
            start_time: Execution start time for elapsed time calculation

        Returns:
            Tuple of (stdout_bytes, stderr_bytes)
        """
        # Send input and close stdin
        if process.stdin:
            process.stdin.write(input_data)
            await process.stdin.drain()
            process.stdin.close()

        # Read output with periodic heartbeats
        last_heartbeat = time.time()
        stdout_task = None
        stderr_task = None

        if process.stdout:
            stdout_task = asyncio.create_task(process.stdout.read())
        if process.stderr:
            stderr_task = asyncio.create_task(process.stderr.read())

        # Wait for completion with heartbeat logging
        while process.returncode is None:
            # Check for heartbeat
            now = time.time()
            if now - last_heartbeat >= self._heartbeat_interval:
                elapsed = now - start_time
                if self._heartbeat_callback:
                    self._heartbeat_callback(
                        f"Claude Code still running... ({elapsed:.0f}s elapsed)"
                    )
                last_heartbeat = now

            # Wait a bit and check process status
            try:
                await asyncio.wait_for(process.wait(), timeout=1.0)
            except asyncio.TimeoutError:
                # Process still running, continue heartbeat loop
                continue

        # Process finished, get output
        stdout_bytes = await stdout_task if stdout_task else b""
        stderr_bytes = await stderr_task if stderr_task else b""

        return stdout_bytes, stderr_bytes

    def _find_claude_cli(self) -> str:
        """Find the Claude Code CLI executable.

        Returns:
            Path to claude CLI, or "claude" if not found (assumes in PATH)
        """
        # Check if 'claude' is in PATH
        claude_path = shutil.which("claude")
        if claude_path:
            return claude_path

        # Fall back to assuming it's in PATH
        return "claude"
