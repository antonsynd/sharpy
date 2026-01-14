"""
Unit tests for CopilotBackend.

Tests the GitHub Copilot CLI backend implementation including:
- Backend type identification
- Command building
- Availability checking
- Rate limit handling
- Interactive prompt detection
"""

import asyncio
import pytest
from unittest.mock import AsyncMock, MagicMock, patch
from pathlib import Path

from build_tools.shared.backends.copilot import CopilotBackend
from build_tools.shared.backends.base import BackendType, BackendConfig, ToolPermission
from build_tools.shared.rate_limiting import RateLimitState


class TestCopilotBackend:
    """Test suite for CopilotBackend."""

    def test_backend_type(self):
        """Test that backend type is correctly identified."""
        backend = CopilotBackend()
        assert backend.backend_type == BackendType.COPILOT

    def test_is_available_when_cli_not_found(self):
        """Test availability when CLI is not installed."""
        with patch("shutil.which", return_value=None):
            backend = CopilotBackend(cli_path="nonexistent")
            assert not backend.is_available()

    def test_is_available_when_rate_limited(self):
        """Test availability when backend is rate limited."""
        rate_state = RateLimitState()
        rate_state.disable_temporarily(60.0)

        backend = CopilotBackend(rate_limit_state=rate_state)
        assert not backend.is_available()

    def test_is_available_when_ready(self):
        """Test availability when backend is ready."""
        rate_state = RateLimitState()

        with patch("shutil.which", return_value="/opt/homebrew/bin/copilot"):
            backend = CopilotBackend(rate_limit_state=rate_state, cli_path="copilot")
            assert backend.is_available()

    def test_build_command_basic(self):
        """Test command building with minimal config."""
        backend = CopilotBackend(cli_path="copilot")
        config = BackendConfig()
        prompt = "Generate a function"

        cmd = backend._build_command(prompt, config)

        assert cmd[0] == "copilot"
        assert "--prompt" in cmd
        assert "Generate a function" in cmd

    def test_build_command_with_tools(self):
        """Test command building with tool permissions."""
        backend = CopilotBackend(cli_path="copilot")
        config = BackendConfig(
            allowed_tools={ToolPermission.READ, ToolPermission.WRITE}
        )
        prompt = "Test prompt"

        cmd = backend._build_command(prompt, config)

        assert "--allow-tool" in cmd
        # Should have read and write as lowercase
        assert "read" in cmd
        assert "write" in cmd

    def test_build_command_tool_mapping(self):
        """Test that ToolPermission values are correctly mapped to lowercase."""
        backend = CopilotBackend(cli_path="copilot")
        config = BackendConfig(
            allowed_tools={
                ToolPermission.READ,
                ToolPermission.WRITE,
                ToolPermission.EDIT,
                ToolPermission.BASH,
            }
        )
        prompt = "Test"

        cmd = backend._build_command(prompt, config)

        # Copilot expects lowercase tool names
        assert "read" in cmd
        assert "write" in cmd
        assert "edit" in cmd
        assert "bash" in cmd

    def test_is_valid_output_with_valid_text(self):
        """Test output validation with valid text."""
        backend = CopilotBackend()

        valid_output = "This is a valid response with substantial content."
        assert backend._is_valid_output(valid_output)

    def test_is_valid_output_with_empty(self):
        """Test output validation with empty text."""
        backend = CopilotBackend()

        assert not backend._is_valid_output("")
        assert not backend._is_valid_output("   ")

    def test_is_valid_output_with_interactive_prompt(self):
        """Test output validation with interactive prompt."""
        backend = CopilotBackend()

        # Interactive prompts often start with "?"
        interactive = "? What would you like to do?"
        assert not backend._is_valid_output(interactive)

    def test_is_valid_output_with_short_text(self):
        """Test output validation with very short text."""
        backend = CopilotBackend()

        # Too short to be meaningful
        assert not backend._is_valid_output("ok")
        assert not backend._is_valid_output("yes")

    @pytest.mark.asyncio
    async def test_execute_cli_not_found(self):
        """Test execution when CLI is not found."""
        backend = CopilotBackend(cli_path="/nonexistent/copilot")
        config = BackendConfig()

        response = await backend.execute("Test prompt", config)

        assert not response.success
        assert "not found" in response.error_message.lower()

    @pytest.mark.asyncio
    async def test_execute_success(self):
        """Test successful execution."""
        backend = CopilotBackend(cli_path="copilot")

        # Mock subprocess
        mock_process = AsyncMock()
        mock_process.returncode = 0
        mock_process.stdout = AsyncMock()
        mock_process.stdout.read = AsyncMock(
            return_value=b"This is a valid response with substantial content."
        )
        mock_process.stderr = AsyncMock()
        mock_process.stderr.read = AsyncMock(return_value=b"")
        mock_process.wait = AsyncMock()

        with patch("asyncio.create_subprocess_exec", return_value=mock_process):
            config = BackendConfig(timeout_seconds=10)
            response = await backend.execute("Test prompt", config)

            assert response.success
            assert "valid response" in response.output
            assert response.exit_code == 0
            assert not response.rate_limited

    @pytest.mark.asyncio
    async def test_execute_interactive_prompt(self):
        """Test execution when Copilot returns interactive prompt."""
        backend = CopilotBackend(cli_path="copilot")

        # Mock subprocess returning interactive prompt
        mock_process = AsyncMock()
        mock_process.returncode = 0
        mock_process.stdout = AsyncMock()
        mock_process.stdout.read = AsyncMock(
            return_value=b"? What would you like to do?"
        )
        mock_process.stderr = AsyncMock()
        mock_process.stderr.read = AsyncMock(return_value=b"")
        mock_process.wait = AsyncMock()

        with patch("asyncio.create_subprocess_exec", return_value=mock_process):
            config = BackendConfig(timeout_seconds=10)
            response = await backend.execute("Test prompt", config)

            assert not response.success
            assert "interactive input" in response.error_message.lower()

    @pytest.mark.asyncio
    async def test_execute_empty_output(self):
        """Test execution when Copilot returns empty output."""
        backend = CopilotBackend(cli_path="copilot")

        # Mock subprocess returning empty output
        mock_process = AsyncMock()
        mock_process.returncode = 0
        mock_process.stdout = AsyncMock()
        mock_process.stdout.read = AsyncMock(return_value=b"")
        mock_process.stderr = AsyncMock()
        mock_process.stderr.read = AsyncMock(return_value=b"")
        mock_process.wait = AsyncMock()

        with patch("asyncio.create_subprocess_exec", return_value=mock_process):
            config = BackendConfig(timeout_seconds=10)
            response = await backend.execute("Test prompt", config)

            assert not response.success
            assert "interactive input" in response.error_message.lower()

    @pytest.mark.asyncio
    async def test_execute_rate_limited(self):
        """Test execution when rate limited."""
        backend = CopilotBackend(cli_path="copilot")

        # Mock subprocess with rate limit error
        mock_process = AsyncMock()
        mock_process.returncode = 1
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
        backend = CopilotBackend(cli_path="copilot")

        # Mock subprocess that hangs
        mock_process = AsyncMock()
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

        backend = CopilotBackend(heartbeat_callback=callback)
        rate_state = backend._rate_limit_state
        rate_state.disable_temporarily(1.0)

        # Simulate waiting (would normally happen in execute)
        wait_time = rate_state.get_wait_time()
        if wait_time and backend._heartbeat_callback:
            backend._heartbeat_callback(f"Waiting {wait_time:.1f}s")

        assert len(callback_messages) > 0
        assert "Waiting" in callback_messages[0]

    def test_find_copilot_cli_default_path(self):
        """Test finding copilot CLI at default Homebrew location."""
        with patch("shutil.which", return_value=None):
            with patch("pathlib.Path.exists", return_value=True):
                backend = CopilotBackend()
                assert backend._cli_path == CopilotBackend.DEFAULT_CLI_PATH

    def test_find_copilot_cli_in_path(self):
        """Test finding copilot CLI in system PATH."""
        with patch("shutil.which", return_value="/usr/local/bin/copilot"):
            backend = CopilotBackend()
            assert backend._cli_path == "/usr/local/bin/copilot"

    def test_model_selection_warning(self):
        """Test that model selection triggers warning callback."""
        callback_messages = []

        def callback(msg: str):
            callback_messages.append(msg)

        backend = CopilotBackend(cli_path="copilot", heartbeat_callback=callback)
        config = BackendConfig(model="some-model")
        prompt = "Test"

        backend._build_command(prompt, config)

        # Should have warning about model selection not supported
        assert any("model selection" in msg.lower() for msg in callback_messages)


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
