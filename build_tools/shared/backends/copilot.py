"""
GitHub Copilot CLI backend implementation.

Provides execution via the GitHub Copilot CLI with rate limiting,
timeout handling, and heartbeat logging for long-running operations.
"""

import asyncio
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


class CopilotBackend(Backend):
    """Backend for GitHub Copilot CLI execution.

    Executes prompts via the GitHub Copilot CLI with:
    - Rate limit detection and automatic backoff
    - Configurable timeouts with process termination
    - Heartbeat logging for long-running operations
    - Tool permission enforcement

    Note: This uses the standalone `copilot` CLI, not `gh copilot`
    (which is limited to shell command suggestions).

    Example:
        ```python
        backend = CopilotBackend()
        config = BackendConfig(
            timeout_seconds=600,
            allowed_tools={ToolPermission.READ, ToolPermission.WRITE},
        )
        response = await backend.execute("Generate a function...", config)
        ```
    """

    # Default path for Homebrew-installed copilot CLI on macOS
    DEFAULT_CLI_PATH = "/opt/homebrew/bin/copilot"

    def __init__(
        self,
        rate_limit_state: Optional[RateLimitState] = None,
        heartbeat_callback: Optional[Callable[[str], None]] = None,
        cli_path: Optional[str] = None,
        project_root: Optional[Path] = None,
    ):
        """Initialize GitHub Copilot backend.

        Args:
            rate_limit_state: Optional existing rate limit state to use.
                If None, creates a new one.
            heartbeat_callback: Optional callback for heartbeat messages during
                long operations. Called with status messages.
            cli_path: Path to copilot CLI executable. If None, searches PATH
                and common locations.
            project_root: Working directory for execution. If None, uses cwd.
        """
        self._rate_limit_state = rate_limit_state or RateLimitState()
        self._heartbeat_callback = heartbeat_callback
        self._cli_path = cli_path or self._find_copilot_cli()
        self._project_root = project_root or Path.cwd()
        self._heartbeat_interval = 60.0  # Log heartbeat every 60 seconds

    @property
    def backend_type(self) -> BackendType:
        """Get backend type identifier."""
        return BackendType.COPILOT

    def is_available(self) -> bool:
        """Check if Copilot CLI is available and not rate limited.

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
        """Execute a prompt via GitHub Copilot CLI.

        Args:
            prompt: The instruction/prompt to send to Copilot
            config: Optional execution configuration

        Returns:
            BackendResponse with execution results
        """
        # Use default config if none provided
        if config is None:
            config = BackendConfig()

        # Wait if rate limited
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
            cmd = self._build_command(prompt, config)

            # Execute with timeout and heartbeat logging
            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=self._project_root,
            )

            try:
                stdout, stderr = await asyncio.wait_for(
                    self._communicate_with_heartbeat(process, start_time),
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
            # Copilot CLI returns exit code 0 even when it needs user input
            # Check if output looks valid (not interactive prompt)
            if process.returncode == 0 and self._is_valid_output(stdout_text):
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
                # Non-zero exit code or interactive prompt detected
                self._rate_limit_state.record_error()

                # Detect if Copilot requires interactive input
                if "?" in stdout_text[:100] or not stdout_text.strip():
                    error_msg = (
                        "GitHub Copilot CLI requires interactive input. "
                        "Consider using Claude Code backend for automated tasks."
                    )
                else:
                    error_msg = stderr_text or stdout_text or "Unknown error"

                return BackendResponse(
                    success=False,
                    output=stdout_text,
                    stderr=stderr_text,
                    exit_code=process.returncode or 1,
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
                error_message=f"Copilot CLI not found at: {self._cli_path}",
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

    def _build_command(self, prompt: str, config: BackendConfig) -> list[str]:
        """Build the Copilot CLI command.

        Args:
            prompt: The prompt to execute
            config: Execution configuration

        Returns:
            Command as list of arguments
        """
        cmd = [self._cli_path, "--prompt", prompt]

        # Add tool permissions - Copilot uses --allow-tool flag (lowercase values)
        if config.allowed_tools:
            for tool in config.allowed_tools:
                # Map ToolPermission enum values to copilot CLI tool names
                # Copilot expects lowercase: read, write, edit, bash, etc.
                tool_name = tool.value.lower()
                cmd.extend(["--allow-tool", tool_name])

        # Add model if specified
        if config.model:
            cmd.extend(["--model", config.model])

        return cmd

    def _is_valid_output(self, output: str) -> bool:
        """Check if output looks like valid non-interactive output.

        Args:
            output: The stdout text from Copilot CLI

        Returns:
            True if output appears valid, False if it looks like an interactive prompt
        """
        # Empty output is not valid
        if not output.strip():
            return False

        # Interactive prompts often start with "?" in first 100 chars
        if "?" in output[:100]:
            return False

        # Valid output should have some substance
        return len(output.strip()) > 10

    async def _communicate_with_heartbeat(
        self, process: asyncio.subprocess.Process, start_time: float
    ) -> tuple[bytes, bytes]:
        """Wait for process completion while logging periodic heartbeats.

        This helps track long-running operations and provides visibility
        into whether the process is still active.

        Args:
            process: The subprocess to monitor
            start_time: Execution start time for elapsed time calculation

        Returns:
            Tuple of (stdout_bytes, stderr_bytes)
        """
        # Start reading output
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
                        f"Copilot CLI still running... ({elapsed:.0f}s elapsed)"
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

    def _find_copilot_cli(self) -> str:
        """Find the Copilot CLI executable.

        Returns:
            Path to copilot CLI, or default path if not found
        """
        # Check if 'copilot' is in PATH
        copilot_path = shutil.which("copilot")
        if copilot_path:
            return copilot_path

        # Check default Homebrew location on macOS
        if Path(self.DEFAULT_CLI_PATH).exists():
            return self.DEFAULT_CLI_PATH

        # Fall back to assuming it's in PATH
        return "copilot"
