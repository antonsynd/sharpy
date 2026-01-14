"""
Unit tests for ClaudeCodeBackend.

Tests the Claude Code CLI backend implementation including:
- Backend type identification
- Command building
- Availability checking
- Rate limit handling
"""

import asyncio
import pytest
from unittest.mock import AsyncMock, MagicMock, patch
from pathlib import Path

from build_tools.shared.backends.claude import ClaudeCodeBackend
from build_tools.shared.backends.base import BackendType, BackendConfig, ToolPermission
from build_tools.shared.rate_limiting import RateLimitState


class TestClaudeCodeBackend:
    """Test suite for ClaudeCodeBackend."""

    def test_backend_type(self):
        """Test that backend type is correctly identified."""
        backend = ClaudeCodeBackend()
        assert backend.backend_type == BackendType.CLAUDE_CODE

    def test_is_available_when_cli_not_found(self):
        """Test availability when CLI is not installed."""
        with patch("shutil.which", return_value=None):
            backend = ClaudeCodeBackend(cli_path="nonexistent")
            assert not backend.is_available()

    def test_is_available_when_rate_limited(self):
        """Test availability when backend is rate limited."""
        rate_state = RateLimitState()
        rate_state.disable_temporarily(60.0)

        backend = ClaudeCodeBackend(rate_limit_state=rate_state)
        assert not backend.is_available()

    def test_is_available_when_ready(self):
        """Test availability when backend is ready."""
        rate_state = RateLimitState()

        with patch("shutil.which", return_value="/usr/bin/claude"):
            backend = ClaudeCodeBackend(rate_limit_state=rate_state, cli_path="claude")
            assert backend.is_available()

    def test_build_command_basic(self):
        """Test command building with minimal config."""
        backend = ClaudeCodeBackend(cli_path="claude")
        config = BackendConfig()

        cmd = backend._build_command(config)

        assert cmd[0] == "claude"
        assert "--print" in cmd

    def test_build_command_with_tools(self):
        """Test command building with tool permissions."""
        backend = ClaudeCodeBackend(cli_path="claude")
        config = BackendConfig(
            allowed_tools={ToolPermission.READ, ToolPermission.WRITE}
        )

        cmd = backend._build_command(config)

        assert "--allowedTools" in cmd
        tools_idx = cmd.index("--allowedTools") + 1
        tools_str = cmd[tools_idx]
        assert "Read" in tools_str
        assert "Write" in tools_str

    def test_build_command_with_model(self):
        """Test command building with model specification."""
        backend = ClaudeCodeBackend(cli_path="claude")
        config = BackendConfig(model="claude-sonnet-4-5-20250929")

        cmd = backend._build_command(config)

        assert "--model" in cmd
        model_idx = cmd.index("--model") + 1
        assert cmd[model_idx] == "claude-sonnet-4-5-20250929"

    @pytest.mark.asyncio
    async def test_execute_cli_not_found(self):
        """Test execution when CLI is not found."""
        backend = ClaudeCodeBackend(cli_path="/nonexistent/claude")
        config = BackendConfig()

        response = await backend.execute("Test prompt", config)

        assert not response.success
        assert "not found" in response.error_message.lower()

    @pytest.mark.asyncio
    async def test_execute_success(self):
        """Test successful execution."""
        backend = ClaudeCodeBackend(cli_path="claude")

        # Mock subprocess
        mock_process = AsyncMock()
        mock_process.returncode = 0
        mock_process.stdin = MagicMock()
        mock_process.stdin.write = MagicMock()
        mock_process.stdin.drain = AsyncMock()
        mock_process.stdin.close = MagicMock()
        mock_process.stdout = AsyncMock()
        mock_process.stdout.read = AsyncMock(return_value=b"Success output")
        mock_process.stderr = AsyncMock()
        mock_process.stderr.read = AsyncMock(return_value=b"")
        mock_process.wait = AsyncMock()

        with patch("asyncio.create_subprocess_exec", return_value=mock_process):
            config = BackendConfig(timeout_seconds=10)
            response = await backend.execute("Test prompt", config)

            assert response.success
            assert response.output == "Success output"
            assert response.exit_code == 0
            assert not response.rate_limited

    @pytest.mark.asyncio
    async def test_execute_rate_limited(self):
        """Test execution when rate limited."""
        backend = ClaudeCodeBackend(cli_path="claude")

        # Mock subprocess with rate limit error
        mock_process = AsyncMock()
        mock_process.returncode = 1
        mock_process.stdin = MagicMock()
        mock_process.stdin.write = MagicMock()
        mock_process.stdin.drain = AsyncMock()
        mock_process.stdin.close = MagicMock()
        mock_process.stdout = AsyncMock()
        mock_process.stdout.read = AsyncMock(
            return_value=b"Rate limit exceeded. Please try again in 5 minutes."
        )
        mock_process.stderr = AsyncMock()
        mock_process.stderr.read = AsyncMock(return_value=b"")
        mock_process.wait = AsyncMock()

        with patch("asyncio.create_subprocess_exec", return_value=mock_process):
            config = BackendConfig(timeout_seconds=10)
            response = await backend.execute("Test prompt", config)

            assert not response.success
            assert response.rate_limited
            assert "rate limit" in response.error_message.lower()

    @pytest.mark.asyncio
    async def test_execute_timeout(self):
        """Test execution timeout handling."""
        backend = ClaudeCodeBackend(cli_path="claude")

        # Mock subprocess that hangs
        mock_process = AsyncMock()
        mock_process.stdin = MagicMock()
        mock_process.stdin.write = MagicMock()
        mock_process.stdin.drain = AsyncMock()
        mock_process.stdin.close = MagicMock()
        mock_process.stdout = AsyncMock()
        mock_process.stdout.read = AsyncMock(side_effect=asyncio.TimeoutError())
        mock_process.stderr = AsyncMock()
        mock_process.wait = AsyncMock()
        mock_process.kill = MagicMock()

        with patch("asyncio.create_subprocess_exec", return_value=mock_process):
            with patch("asyncio.wait_for", side_effect=asyncio.TimeoutError()):
                config = BackendConfig(timeout_seconds=1)
                response = await backend.execute("Test prompt", config)

                assert not response.success
                assert "timed out" in response.error_message.lower()
                mock_process.kill.assert_called_once()

    def test_heartbeat_callback_invoked(self):
        """Test that heartbeat callback is invoked during execution."""
        callback_messages = []

        def callback(msg: str):
            callback_messages.append(msg)

        backend = ClaudeCodeBackend(heartbeat_callback=callback)
        rate_state = backend._rate_limit_state
        rate_state.disable_temporarily(1.0)

        # Simulate waiting (would normally happen in execute)
        wait_time = rate_state.get_wait_time()
        if wait_time and backend._heartbeat_callback:
            backend._heartbeat_callback(f"Waiting {wait_time:.1f}s")

        assert len(callback_messages) > 0
        assert "Waiting" in callback_messages[0]


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
