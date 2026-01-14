"""
Unit tests for BackendManager.

Tests cover:
- Backend registration and unregistration
- Execute with single backend
- Execute with automatic failover
- Rate limit handling and tracking
- Backend status reporting
"""

import asyncio
import pytest
from unittest.mock import AsyncMock, MagicMock, patch

from build_tools.shared.backends.base import (
    Backend,
    BackendType,
    BackendConfig,
    BackendResponse,
    ToolPermission,
)
from build_tools.shared.backends.manager import BackendManager, BackendManagerConfig
from build_tools.shared.rate_limiting import RateLimitState


# --- Test Fixtures and Helpers ---


class MockBackend(Backend):
    """Mock backend for testing."""

    def __init__(
        self,
        backend_type: BackendType,
        available: bool = True,
        response: BackendResponse | None = None,
    ):
        self._backend_type = backend_type
        self._available = available
        self._response = response or BackendResponse(
            success=True, output="Mock output"
        )
        self.execute_calls: list[tuple[str, BackendConfig | None]] = []

    @property
    def backend_type(self) -> BackendType:
        return self._backend_type

    def is_available(self) -> bool:
        return self._available

    async def execute(
        self, prompt: str, config: BackendConfig | None = None
    ) -> BackendResponse:
        self.execute_calls.append((prompt, config))
        return self._response


def create_success_response(output: str = "Success") -> BackendResponse:
    return BackendResponse(success=True, output=output, duration_seconds=1.0)


def create_rate_limit_response() -> BackendResponse:
    return BackendResponse(
        success=False,
        output="",
        rate_limited=True,
        error_message="Rate limit exceeded. Try again in 5 minutes.",
    )


def create_error_response(message: str = "Error occurred") -> BackendResponse:
    return BackendResponse(
        success=False,
        output="",
        exit_code=1,
        error_message=message,
    )


# --- Test Classes ---


class TestBackendManagerConfig:
    """Tests for BackendManagerConfig defaults and creation."""

    def test_default_config(self):
        config = BackendManagerConfig()
        assert config.primary_backend == BackendType.CLAUDE_CODE
        assert config.fallback_backends == []
        assert config.rate_limit_window_seconds == 3600
        assert config.max_requests_per_window == 50
        assert config.auto_failover is True

    def test_custom_config(self):
        config = BackendManagerConfig(
            primary_backend=BackendType.COPILOT,
            fallback_backends=[BackendType.CLAUDE_CODE],
            rate_limit_window_seconds=1800,
            max_requests_per_window=25,
            auto_failover=False,
        )
        assert config.primary_backend == BackendType.COPILOT
        assert config.fallback_backends == [BackendType.CLAUDE_CODE]
        assert config.rate_limit_window_seconds == 1800
        assert config.max_requests_per_window == 25
        assert config.auto_failover is False


class TestBackendRegistration:
    """Tests for backend registration and unregistration."""

    def test_register_backend(self):
        manager = BackendManager()
        backend = MockBackend(BackendType.CLAUDE_CODE)

        manager.register_backend(backend)

        assert BackendType.CLAUDE_CODE in manager
        assert len(manager) == 1

    def test_register_multiple_backends(self):
        manager = BackendManager()
        claude = MockBackend(BackendType.CLAUDE_CODE)
        copilot = MockBackend(BackendType.COPILOT)

        manager.register_backend(claude)
        manager.register_backend(copilot)

        assert BackendType.CLAUDE_CODE in manager
        assert BackendType.COPILOT in manager
        assert len(manager) == 2

    def test_register_with_custom_rate_state(self):
        manager = BackendManager()
        backend = MockBackend(BackendType.CLAUDE_CODE)
        rate_state = RateLimitState(consecutive_errors=5)

        manager.register_backend(backend, rate_limit_state=rate_state)

        assert manager.get_rate_state(BackendType.CLAUDE_CODE) is rate_state
        assert manager.get_rate_state(BackendType.CLAUDE_CODE).consecutive_errors == 5

    def test_unregister_backend(self):
        manager = BackendManager()
        backend = MockBackend(BackendType.CLAUDE_CODE)
        manager.register_backend(backend)

        result = manager.unregister_backend(BackendType.CLAUDE_CODE)

        assert result is True
        assert BackendType.CLAUDE_CODE not in manager
        assert len(manager) == 0

    def test_unregister_nonexistent_backend(self):
        manager = BackendManager()

        result = manager.unregister_backend(BackendType.CLAUDE_CODE)

        assert result is False


class TestExecute:
    """Tests for execute method."""

    @pytest.mark.asyncio
    async def test_execute_single_backend_success(self):
        manager = BackendManager()
        backend = MockBackend(
            BackendType.CLAUDE_CODE, response=create_success_response("Hello")
        )
        manager.register_backend(backend)

        response, used_backend = await manager.execute("Test prompt")

        assert response.success is True
        assert response.output == "Hello"
        assert used_backend == BackendType.CLAUDE_CODE
        assert len(backend.execute_calls) == 1

    @pytest.mark.asyncio
    async def test_execute_with_config(self):
        manager = BackendManager()
        backend = MockBackend(BackendType.CLAUDE_CODE)
        manager.register_backend(backend)
        config = BackendConfig(
            timeout_seconds=120,
            allowed_tools={ToolPermission.READ, ToolPermission.WRITE},
        )

        await manager.execute("Test prompt", config=config)

        assert len(backend.execute_calls) == 1
        prompt, passed_config = backend.execute_calls[0]
        assert passed_config is config

    @pytest.mark.asyncio
    async def test_execute_no_backends_raises(self):
        manager = BackendManager()

        with pytest.raises(RuntimeError, match="No backends registered"):
            await manager.execute("Test prompt")

    @pytest.mark.asyncio
    async def test_execute_uses_primary_backend_first(self):
        config = BackendManagerConfig(
            primary_backend=BackendType.COPILOT,
            fallback_backends=[BackendType.CLAUDE_CODE],
        )
        manager = BackendManager(config)
        
        claude = MockBackend(
            BackendType.CLAUDE_CODE, response=create_success_response("Claude")
        )
        copilot = MockBackend(
            BackendType.COPILOT, response=create_success_response("Copilot")
        )
        manager.register_backend(claude)
        manager.register_backend(copilot)

        response, used_backend = await manager.execute("Test prompt")

        assert used_backend == BackendType.COPILOT
        assert response.output == "Copilot"
        # Primary should be tried first
        assert len(copilot.execute_calls) == 1
        assert len(claude.execute_calls) == 0

    @pytest.mark.asyncio
    async def test_execute_preferred_backend(self):
        manager = BackendManager()
        claude = MockBackend(
            BackendType.CLAUDE_CODE, response=create_success_response("Claude")
        )
        copilot = MockBackend(
            BackendType.COPILOT, response=create_success_response("Copilot")
        )
        manager.register_backend(claude)
        manager.register_backend(copilot)

        response, used_backend = await manager.execute(
            "Test prompt", preferred_backend=BackendType.COPILOT
        )

        assert used_backend == BackendType.COPILOT
        assert len(copilot.execute_calls) == 1
        assert len(claude.execute_calls) == 0


class TestFailover:
    """Tests for automatic failover behavior."""

    @pytest.mark.asyncio
    async def test_failover_on_rate_limit(self):
        config = BackendManagerConfig(
            primary_backend=BackendType.CLAUDE_CODE,
            fallback_backends=[BackendType.COPILOT],
            auto_failover=True,
        )
        manager = BackendManager(config)
        
        claude = MockBackend(
            BackendType.CLAUDE_CODE, response=create_rate_limit_response()
        )
        copilot = MockBackend(
            BackendType.COPILOT, response=create_success_response("Copilot saved us")
        )
        manager.register_backend(claude)
        manager.register_backend(copilot)

        response, used_backend = await manager.execute("Test prompt")

        assert response.success is True
        assert used_backend == BackendType.COPILOT
        assert len(claude.execute_calls) == 1  # Primary tried first
        assert len(copilot.execute_calls) == 1  # Then fallback

    @pytest.mark.asyncio
    async def test_failover_on_error(self):
        config = BackendManagerConfig(
            primary_backend=BackendType.CLAUDE_CODE,
            fallback_backends=[BackendType.COPILOT],
            auto_failover=True,
        )
        manager = BackendManager(config)
        
        claude = MockBackend(
            BackendType.CLAUDE_CODE, response=create_error_response("Network error")
        )
        copilot = MockBackend(
            BackendType.COPILOT, response=create_success_response("Copilot works")
        )
        manager.register_backend(claude)
        manager.register_backend(copilot)

        response, used_backend = await manager.execute("Test prompt")

        assert response.success is True
        assert used_backend == BackendType.COPILOT

    @pytest.mark.asyncio
    async def test_failover_on_unavailable(self):
        manager = BackendManager()
        
        claude = MockBackend(BackendType.CLAUDE_CODE, available=False)
        copilot = MockBackend(
            BackendType.COPILOT, response=create_success_response("Copilot available")
        )
        manager.register_backend(claude)
        manager.register_backend(copilot)

        response, used_backend = await manager.execute("Test prompt")

        assert response.success is True
        assert used_backend == BackendType.COPILOT
        assert len(claude.execute_calls) == 0  # Skipped due to unavailable
        assert len(copilot.execute_calls) == 1

    @pytest.mark.asyncio
    async def test_no_failover_when_disabled(self):
        config = BackendManagerConfig(
            primary_backend=BackendType.CLAUDE_CODE,
            fallback_backends=[BackendType.COPILOT],
            auto_failover=False,
        )
        manager = BackendManager(config)
        
        claude = MockBackend(
            BackendType.CLAUDE_CODE, response=create_rate_limit_response()
        )
        copilot = MockBackend(
            BackendType.COPILOT, response=create_success_response("Copilot")
        )
        manager.register_backend(claude)
        manager.register_backend(copilot)

        response, used_backend = await manager.execute("Test prompt")

        # Should return the rate limit error without trying fallback
        assert response.success is False
        assert response.rate_limited is True
        assert used_backend == BackendType.CLAUDE_CODE
        assert len(copilot.execute_calls) == 0  # Fallback not tried

    @pytest.mark.asyncio
    async def test_all_backends_fail(self):
        config = BackendManagerConfig(
            primary_backend=BackendType.CLAUDE_CODE,
            fallback_backends=[BackendType.COPILOT],
        )
        manager = BackendManager(config)
        
        claude = MockBackend(
            BackendType.CLAUDE_CODE, response=create_rate_limit_response()
        )
        copilot = MockBackend(
            BackendType.COPILOT, response=create_error_response("Copilot also failed")
        )
        manager.register_backend(claude)
        manager.register_backend(copilot)

        response, used_backend = await manager.execute("Test prompt")

        assert response.success is False
        assert "All backends failed" in response.error_message
        assert len(claude.execute_calls) == 1
        assert len(copilot.execute_calls) == 1


class TestRateLimitTracking:
    """Tests for rate limit state tracking."""

    @pytest.mark.asyncio
    async def test_records_success(self):
        manager = BackendManager()
        backend = MockBackend(
            BackendType.CLAUDE_CODE, response=create_success_response()
        )
        manager.register_backend(backend)
        
        # Add some errors first
        rate_state = manager.get_rate_state(BackendType.CLAUDE_CODE)
        rate_state.record_error()
        rate_state.record_error()
        assert rate_state.consecutive_errors == 2

        await manager.execute("Test prompt")

        # Success should reset errors
        assert rate_state.consecutive_errors == 0

    @pytest.mark.asyncio
    async def test_records_error_on_failure(self):
        manager = BackendManager()
        backend = MockBackend(
            BackendType.CLAUDE_CODE, response=create_error_response()
        )
        manager.register_backend(backend)

        # Force auto_failover off to stop at first failure
        manager._config.auto_failover = False
        
        await manager.execute("Test prompt")

        rate_state = manager.get_rate_state(BackendType.CLAUDE_CODE)
        assert rate_state.consecutive_errors == 1

    @pytest.mark.asyncio
    async def test_skips_backend_at_capacity(self):
        config = BackendManagerConfig(
            max_requests_per_window=2,
            rate_limit_window_seconds=3600,
        )
        manager = BackendManager(config)
        
        claude = MockBackend(
            BackendType.CLAUDE_CODE, response=create_success_response("Claude")
        )
        copilot = MockBackend(
            BackendType.COPILOT, response=create_success_response("Copilot")
        )
        manager.register_backend(claude)
        manager.register_backend(copilot)

        # Fill up Claude's capacity
        rate_state = manager.get_rate_state(BackendType.CLAUDE_CODE)
        rate_state.record_request()
        rate_state.record_request()

        response, used_backend = await manager.execute("Test prompt")

        # Should skip Claude and use Copilot
        assert used_backend == BackendType.COPILOT
        assert len(claude.execute_calls) == 0

    def test_reset_backend_state(self):
        manager = BackendManager()
        backend = MockBackend(BackendType.CLAUDE_CODE)
        manager.register_backend(backend)
        
        rate_state = manager.get_rate_state(BackendType.CLAUDE_CODE)
        rate_state.record_error()
        rate_state.record_error()
        rate_state.record_error()
        assert rate_state.consecutive_errors == 3

        result = manager.reset_backend_state(BackendType.CLAUDE_CODE)

        assert result is True
        new_state = manager.get_rate_state(BackendType.CLAUDE_CODE)
        assert new_state.consecutive_errors == 0

    def test_reset_nonexistent_backend_state(self):
        manager = BackendManager()

        result = manager.reset_backend_state(BackendType.CLAUDE_CODE)

        assert result is False


class TestStatusReporting:
    """Tests for backend status reporting."""

    def test_get_available_backends(self):
        manager = BackendManager()
        available = MockBackend(BackendType.CLAUDE_CODE, available=True)
        unavailable = MockBackend(BackendType.COPILOT, available=False)
        manager.register_backend(available)
        manager.register_backend(unavailable)

        result = manager.get_available_backends()

        assert BackendType.CLAUDE_CODE in result
        assert BackendType.COPILOT not in result

    def test_get_available_backends_excludes_at_capacity(self):
        config = BackendManagerConfig(max_requests_per_window=1)
        manager = BackendManager(config)
        backend = MockBackend(BackendType.CLAUDE_CODE, available=True)
        manager.register_backend(backend)

        # Fill capacity
        rate_state = manager.get_rate_state(BackendType.CLAUDE_CODE)
        rate_state.record_request()

        result = manager.get_available_backends()

        assert BackendType.CLAUDE_CODE not in result

    def test_get_backend_status(self):
        config = BackendManagerConfig(max_requests_per_window=10)
        manager = BackendManager(config)
        backend = MockBackend(BackendType.CLAUDE_CODE, available=True)
        manager.register_backend(backend)

        # Add some requests
        rate_state = manager.get_rate_state(BackendType.CLAUDE_CODE)
        rate_state.record_request()
        rate_state.record_request()
        rate_state.record_error()  # Note: record_error does not add to request_times

        status = manager.get_backend_status()

        assert BackendType.CLAUDE_CODE in status
        claude_status = status[BackendType.CLAUDE_CODE]
        assert claude_status["available"] is True
        assert claude_status["registered"] is True
        assert claude_status["requests_in_window"] == 2  # Only explicit requests count
        assert claude_status["max_requests_per_window"] == 10
        assert claude_status["consecutive_errors"] == 1
        assert claude_status["rate_limited"] is False

    def test_get_backend_status_empty(self):
        manager = BackendManager()

        status = manager.get_backend_status()

        assert status == {}

    def test_get_rate_state_nonexistent(self):
        manager = BackendManager()

        result = manager.get_rate_state(BackendType.CLAUDE_CODE)

        assert result is None


class TestBackendOrder:
    """Tests for backend ordering logic."""

    def test_order_prefers_specified_backend(self):
        config = BackendManagerConfig(
            primary_backend=BackendType.CLAUDE_CODE,
            fallback_backends=[BackendType.COPILOT],
        )
        manager = BackendManager(config)
        claude = MockBackend(BackendType.CLAUDE_CODE)
        copilot = MockBackend(BackendType.COPILOT)
        manager.register_backend(claude)
        manager.register_backend(copilot)

        order = manager._get_backend_order(preferred=BackendType.COPILOT)

        assert order[0] == BackendType.COPILOT
        assert order[1] == BackendType.CLAUDE_CODE

    def test_order_includes_primary_and_fallbacks(self):
        config = BackendManagerConfig(
            primary_backend=BackendType.CLAUDE_CODE,
            fallback_backends=[BackendType.COPILOT],
        )
        manager = BackendManager(config)
        claude = MockBackend(BackendType.CLAUDE_CODE)
        copilot = MockBackend(BackendType.COPILOT)
        manager.register_backend(claude)
        manager.register_backend(copilot)

        order = manager._get_backend_order()

        assert order == [BackendType.CLAUDE_CODE, BackendType.COPILOT]

    def test_order_skips_unregistered(self):
        config = BackendManagerConfig(
            primary_backend=BackendType.CLAUDE_CODE,
            fallback_backends=[BackendType.COPILOT],
        )
        manager = BackendManager(config)
        # Only register Claude, not Copilot
        claude = MockBackend(BackendType.CLAUDE_CODE)
        manager.register_backend(claude)

        order = manager._get_backend_order()

        assert order == [BackendType.CLAUDE_CODE]

    def test_order_no_duplicates(self):
        config = BackendManagerConfig(
            primary_backend=BackendType.CLAUDE_CODE,
            fallback_backends=[BackendType.CLAUDE_CODE, BackendType.COPILOT],
        )
        manager = BackendManager(config)
        claude = MockBackend(BackendType.CLAUDE_CODE)
        copilot = MockBackend(BackendType.COPILOT)
        manager.register_backend(claude)
        manager.register_backend(copilot)

        order = manager._get_backend_order(preferred=BackendType.CLAUDE_CODE)

        # Should not have duplicates
        assert order == [BackendType.CLAUDE_CODE, BackendType.COPILOT]


class TestIntegration:
    """Integration tests with realistic scenarios."""

    @pytest.mark.asyncio
    async def test_multiple_requests_with_failover(self):
        """Simulate multiple requests where primary gets rate limited mid-session."""
        config = BackendManagerConfig(
            primary_backend=BackendType.CLAUDE_CODE,
            fallback_backends=[BackendType.COPILOT],
        )
        manager = BackendManager(config)

        request_count = [0]

        class FlakyClaude(Backend):
            """Claude that fails after 2 requests."""

            @property
            def backend_type(self) -> BackendType:
                return BackendType.CLAUDE_CODE

            def is_available(self) -> bool:
                return True

            async def execute(
                self, prompt: str, config: BackendConfig | None = None
            ) -> BackendResponse:
                request_count[0] += 1
                if request_count[0] > 2:
                    return create_rate_limit_response()
                return create_success_response(f"Claude response {request_count[0]}")

        manager.register_backend(FlakyClaude())
        manager.register_backend(
            MockBackend(BackendType.COPILOT, response=create_success_response("Copilot"))
        )

        # First two requests should use Claude
        r1, b1 = await manager.execute("Request 1")
        assert b1 == BackendType.CLAUDE_CODE
        assert "Claude response 1" in r1.output

        r2, b2 = await manager.execute("Request 2")
        assert b2 == BackendType.CLAUDE_CODE
        assert "Claude response 2" in r2.output

        # Third request should fail over to Copilot
        r3, b3 = await manager.execute("Request 3")
        assert b3 == BackendType.COPILOT
        assert "Copilot" in r3.output


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
