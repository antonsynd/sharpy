"""
Unit tests for orchestrator timeout-skip and rate-limit paths.

Tests that the DogfoodOrchestrator correctly handles timed_out and
rate_limited results from the AI backend, recording skips with the
expected metadata and directory structure.
"""

import tempfile
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from build_tools.sharpy_dogfood.backends import ExecutionResult as AIResult
from build_tools.sharpy_dogfood.config import Config, BackendConfig
from build_tools.sharpy_dogfood.orchestrator import (
    DogfoodOrchestrator,
    GenerationResult,
    IterationResult,
    IterationStatus,
    MultifileGenerationResult,
)


@pytest.fixture
def tmp_config(tmp_path):
    """Create a Config pointing at isolated temp directories."""
    config = Config(project_root=tmp_path)
    config.output_dir = tmp_path / "output"
    config.issues_dir = tmp_path / "output" / "issues"
    config.successes_dir = tmp_path / "output" / "successes"
    config.skips_dir = tmp_path / "output" / "skips"
    config.output_dir.mkdir(parents=True, exist_ok=True)
    config.issues_dir.mkdir(parents=True, exist_ok=True)
    config.successes_dir.mkdir(parents=True, exist_ok=True)
    config.skips_dir.mkdir(parents=True, exist_ok=True)
    return config


@pytest.fixture
def orchestrator(tmp_config):
    """Create a DogfoodOrchestrator with mocked dependencies."""
    orch = DogfoodOrchestrator(tmp_config, auto_convert=False)
    # Mock the backend manager so no real AI calls are made
    orch.backend_manager = MagicMock()
    # Mock the compiler
    orch.compiler = MagicMock()
    return orch


class TestTimeoutSkipPath:
    """Tests for timeout detection and skip recording."""

    @pytest.mark.asyncio
    async def test_run_iteration_timed_out_creates_skip_dir(self, orchestrator):
        """When generation times out, a skip directory should be created."""
        # Mock _generate_and_validate_code to return a timed_out result
        timed_out_result = GenerationResult(
            success=False,
            skip_reason="Generation timed out after 180s",
            backend_used="claude",
            generation_duration=180.0,
            timed_out=True,
            attempts=1,
        )
        orchestrator._generate_and_validate_code = AsyncMock(
            return_value=timed_out_result
        )

        result = await orchestrator.run_iteration(
            iteration=1,
            feature_focus="simple_function",
            complexity="medium",
        )

        assert result.status == IterationStatus.SKIPPED
        assert result.skip_dir is not None
        assert result.skip_dir.exists()
        assert result.skip_reason == "Generation timed out after 180s"

        # Verify metadata was written
        metadata_file = result.skip_dir / "metadata.json"
        assert metadata_file.exists()
        skip_reason_file = result.skip_dir / "skip_reason.txt"
        assert skip_reason_file.exists()
        assert "timed out" in skip_reason_file.read_text().lower()

    @pytest.mark.asyncio
    async def test_run_multifile_iteration_timed_out_creates_skip_dir(
        self, orchestrator
    ):
        """When multifile generation times out, a skip directory should be created."""
        timed_out_result = MultifileGenerationResult(
            success=False,
            skip_reason="Generation timed out after 180s",
            backend_used="claude",
            generation_duration=180.0,
            timed_out=True,
            attempts=1,
        )
        orchestrator._generate_and_validate_multifile_code = AsyncMock(
            return_value=timed_out_result
        )

        result = await orchestrator.run_multifile_iteration(
            iteration=1,
            feature_focus="module_imports",
            complexity="complex",
        )

        assert result.status == IterationStatus.SKIPPED
        assert result.skip_dir is not None
        assert result.skip_dir.exists()
        assert result.skip_reason == "Generation timed out after 180s"

    @pytest.mark.asyncio
    async def test_timeout_skip_records_backend_used(self, orchestrator):
        """Timeout skip metadata should include the backend name."""
        timed_out_result = GenerationResult(
            success=False,
            skip_reason="Generation timed out after 180s",
            backend_used="copilot",
            generation_duration=180.0,
            timed_out=True,
            attempts=1,
        )
        orchestrator._generate_and_validate_code = AsyncMock(
            return_value=timed_out_result
        )

        result = await orchestrator.run_iteration(
            iteration=1,
            feature_focus="simple_function",
            complexity="simple",
        )

        import json

        metadata = json.loads((result.skip_dir / "metadata.json").read_text())
        assert metadata["backend_used"] == "copilot"
        assert metadata["feature_focus"] == "simple_function"
        assert metadata["complexity"] == "simple"

    @pytest.mark.asyncio
    async def test_timeout_skip_default_reason(self, orchestrator):
        """When skip_reason is None, _record_timeout_skip uses default."""
        timed_out_result = GenerationResult(
            success=False,
            skip_reason=None,
            backend_used="claude",
            generation_duration=10.0,
            timed_out=True,
            attempts=1,
        )
        orchestrator._generate_and_validate_code = AsyncMock(
            return_value=timed_out_result
        )

        result = await orchestrator.run_iteration(
            iteration=1,
            feature_focus="if_else_simple",
            complexity="simple",
        )

        assert result.status == IterationStatus.SKIPPED
        assert result.skip_reason == "Generation timed out"


class TestRateLimitPath:
    """Tests for rate-limit detection and skip behavior."""

    @pytest.mark.asyncio
    async def test_run_iteration_rate_limited_returns_skipped(self, orchestrator):
        """When generation is rate limited, iteration should be SKIPPED."""
        rate_limited_result = GenerationResult(
            success=False,
            skip_reason="Rate limited: All backends unavailable",
            backend_used="claude",
            generation_duration=2.0,
            rate_limited=True,
            attempts=1,
        )
        orchestrator._generate_and_validate_code = AsyncMock(
            return_value=rate_limited_result
        )

        result = await orchestrator.run_iteration(
            iteration=1,
            feature_focus="while_loop",
            complexity="simple",
        )

        assert result.status == IterationStatus.SKIPPED
        assert result.skip_reason == "Rate limited: All backends unavailable"
        # Rate-limited skips don't create skip directories
        assert result.skip_dir is None

    @pytest.mark.asyncio
    async def test_run_multifile_iteration_rate_limited_returns_skipped(
        self, orchestrator
    ):
        """When multifile generation is rate limited, iteration should be SKIPPED."""
        rate_limited_result = MultifileGenerationResult(
            success=False,
            skip_reason="Rate limited: 429 Too Many Requests",
            backend_used="claude",
            generation_duration=1.0,
            rate_limited=True,
            attempts=1,
        )
        orchestrator._generate_and_validate_multifile_code = AsyncMock(
            return_value=rate_limited_result
        )

        result = await orchestrator.run_multifile_iteration(
            iteration=1,
            feature_focus="module_imports",
            complexity="complex",
        )

        assert result.status == IterationStatus.SKIPPED
        assert result.skip_dir is None
        assert "rate limited" in result.skip_reason.lower()


class TestExecutionResultTimeout:
    """Tests for ExecutionResult.from_backend_response timeout detection."""

    def test_timed_out_field_from_backend_response(self):
        """timed_out should be True when BackendResponse.timed_out is True."""
        from build_tools.shared.backends.base import BackendResponse

        response = BackendResponse(
            success=False,
            output="",
            exit_code=-1,
            duration_seconds=300.0,
            timed_out=True,
            error_message="Execution timed out after 300.0s",
        )
        result = AIResult.from_backend_response(response, "claude")
        assert result.timed_out is True

    def test_timed_out_false_when_no_timeout(self):
        """timed_out should be False for normal errors."""
        from build_tools.shared.backends.base import BackendResponse

        response = BackendResponse(
            success=False,
            output="",
            exit_code=1,
            duration_seconds=5.0,
            error_message="Some other error",
        )
        result = AIResult.from_backend_response(response, "claude")
        assert result.timed_out is False

    def test_timed_out_fallback_string_matching(self):
        """timed_out should detect timeout via string patterns as fallback."""
        from build_tools.shared.backends.base import BackendResponse

        # "deadline exceeded" pattern
        response = BackendResponse(
            success=False,
            output="",
            exit_code=-1,
            duration_seconds=60.0,
            timed_out=False,
            error_message="Request deadline exceeded",
        )
        result = AIResult.from_backend_response(response, "claude")
        assert result.timed_out is True

    def test_timed_out_fallback_timeout_pattern(self):
        """timed_out should detect 'timeout' substring as fallback."""
        from build_tools.shared.backends.base import BackendResponse

        response = BackendResponse(
            success=False,
            output="",
            exit_code=-1,
            duration_seconds=60.0,
            timed_out=False,
            error_message="Connection timeout occurred",
        )
        result = AIResult.from_backend_response(response, "claude")
        assert result.timed_out is True

    def test_rate_limited_from_backend_response(self):
        """rate_limited should be propagated from BackendResponse."""
        from build_tools.shared.backends.base import BackendResponse

        response = BackendResponse(
            success=False,
            output="",
            exit_code=1,
            duration_seconds=2.0,
            rate_limited=True,
            error_message="Rate limited. Wait 30s before retry.",
        )
        result = AIResult.from_backend_response(response, "claude")
        assert result.rate_limited is True
        assert result.timed_out is False


class TestIsTimeoutError:
    """Tests for the _is_timeout_error helper function."""

    def test_timed_out_pattern(self):
        from build_tools.sharpy_dogfood.backends import _is_timeout_error

        assert _is_timeout_error("Execution timed out after 300s") is True

    def test_timeout_pattern(self):
        from build_tools.sharpy_dogfood.backends import _is_timeout_error

        assert _is_timeout_error("Connection timeout") is True

    def test_deadline_exceeded_pattern(self):
        from build_tools.sharpy_dogfood.backends import _is_timeout_error

        assert _is_timeout_error("Request deadline exceeded") is True

    def test_no_match(self):
        from build_tools.sharpy_dogfood.backends import _is_timeout_error

        assert _is_timeout_error("Rate limited") is False

    def test_empty_string(self):
        from build_tools.sharpy_dogfood.backends import _is_timeout_error

        assert _is_timeout_error("") is False

    def test_case_insensitive(self):
        from build_tools.sharpy_dogfood.backends import _is_timeout_error

        assert _is_timeout_error("TIMED OUT") is True
        assert _is_timeout_error("Deadline Exceeded") is True


class TestTimeoutExceptionTypes:
    """Tests for exception-type timeout detection in backend execute() (#200)."""

    def test_timeout_exception_types_is_tuple(self):
        """_TIMEOUT_EXCEPTION_TYPES should be a tuple (may be empty if deps not installed)."""
        from build_tools.sharpy_dogfood.backends import _TIMEOUT_EXCEPTION_TYPES

        assert isinstance(_TIMEOUT_EXCEPTION_TYPES, tuple)

    @pytest.mark.asyncio
    async def test_claude_backend_catches_timeout_exception(self):
        """ClaudeBackend.execute() should return timed_out=True on timeout exceptions."""
        from build_tools.sharpy_dogfood.backends import ClaudeBackend
        from build_tools.sharpy_dogfood.config import BackendConfig, RateLimitConfig

        backend_config = BackendConfig(
            name="claude",
            enabled=True,
            rate_limit=RateLimitConfig(),
        )
        backend = ClaudeBackend(backend_config, Path("/tmp"), verbose=False)

        # Create a custom exception that simulates a timeout
        # We monkey-patch _TIMEOUT_EXCEPTION_TYPES to include our test exception
        class FakeTimeoutError(Exception):
            pass

        import build_tools.sharpy_dogfood.backends as backends_mod

        original = backends_mod._TIMEOUT_EXCEPTION_TYPES
        backends_mod._TIMEOUT_EXCEPTION_TYPES = (FakeTimeoutError,)

        try:
            # Mock the shared backend to raise our fake timeout
            backend._shared_backend = MagicMock()
            backend._shared_backend.execute = AsyncMock(
                side_effect=FakeTimeoutError("Read timed out")
            )

            result = await backend.execute("test prompt", timeout=10.0)

            assert result.timed_out is True
            assert result.success is False
            assert "FakeTimeoutError" in result.error
            assert result.backend == "claude"
        finally:
            backends_mod._TIMEOUT_EXCEPTION_TYPES = original

    @pytest.mark.asyncio
    async def test_copilot_backend_catches_timeout_exception(self):
        """CopilotBackend.execute() should return timed_out=True on timeout exceptions."""
        from build_tools.sharpy_dogfood.backends import CopilotBackend
        from build_tools.sharpy_dogfood.config import BackendConfig, RateLimitConfig

        backend_config = BackendConfig(
            name="copilot",
            enabled=True,
            rate_limit=RateLimitConfig(),
        )
        backend = CopilotBackend(backend_config, Path("/tmp"), verbose=False)

        # Create a custom exception that simulates a timeout
        class FakeTimeoutError(Exception):
            pass

        import build_tools.sharpy_dogfood.backends as backends_mod

        original = backends_mod._TIMEOUT_EXCEPTION_TYPES
        backends_mod._TIMEOUT_EXCEPTION_TYPES = (FakeTimeoutError,)

        try:
            # Mock the shared backend to raise our fake timeout
            backend._shared_backend = MagicMock()
            backend._shared_backend.execute = AsyncMock(
                side_effect=FakeTimeoutError("API timeout")
            )

            result = await backend.execute("test prompt", timeout=10.0)

            assert result.timed_out is True
            assert result.success is False
            assert "FakeTimeoutError" in result.error
            assert result.backend == "copilot"
        finally:
            backends_mod._TIMEOUT_EXCEPTION_TYPES = original

    @pytest.mark.asyncio
    async def test_non_timeout_exception_still_raises(self):
        """Non-timeout exceptions should propagate normally."""
        from build_tools.sharpy_dogfood.backends import ClaudeBackend
        from build_tools.sharpy_dogfood.config import BackendConfig, RateLimitConfig

        backend_config = BackendConfig(
            name="claude",
            enabled=True,
            rate_limit=RateLimitConfig(),
        )
        backend = ClaudeBackend(backend_config, Path("/tmp"), verbose=False)

        # Mock the shared backend to raise a non-timeout exception
        backend._shared_backend = MagicMock()
        backend._shared_backend.execute = AsyncMock(
            side_effect=RuntimeError("Something else went wrong")
        )

        with pytest.raises(RuntimeError, match="Something else went wrong"):
            await backend.execute("test prompt", timeout=10.0)

    @pytest.mark.asyncio
    async def test_timeout_exception_records_duration(self):
        """Timeout exception result should include a positive duration."""
        from build_tools.sharpy_dogfood.backends import ClaudeBackend
        from build_tools.sharpy_dogfood.config import BackendConfig, RateLimitConfig

        backend_config = BackendConfig(
            name="claude",
            enabled=True,
            rate_limit=RateLimitConfig(),
        )
        backend = ClaudeBackend(backend_config, Path("/tmp"), verbose=False)

        class FakeTimeoutError(Exception):
            pass

        import build_tools.sharpy_dogfood.backends as backends_mod

        original = backends_mod._TIMEOUT_EXCEPTION_TYPES
        backends_mod._TIMEOUT_EXCEPTION_TYPES = (FakeTimeoutError,)

        try:
            backend._shared_backend = MagicMock()
            backend._shared_backend.execute = AsyncMock(
                side_effect=FakeTimeoutError("timed out")
            )

            result = await backend.execute("test prompt", timeout=10.0)

            assert result.duration_seconds >= 0.0
            assert result.timed_out is True
        finally:
            backends_mod._TIMEOUT_EXCEPTION_TYPES = original

    @pytest.mark.asyncio
    async def test_empty_timeout_exception_tuple_no_catch(self):
        """When _TIMEOUT_EXCEPTION_TYPES is empty, exceptions propagate normally."""
        from build_tools.sharpy_dogfood.backends import ClaudeBackend
        from build_tools.sharpy_dogfood.config import BackendConfig, RateLimitConfig

        backend_config = BackendConfig(
            name="claude",
            enabled=True,
            rate_limit=RateLimitConfig(),
        )
        backend = ClaudeBackend(backend_config, Path("/tmp"), verbose=False)

        import build_tools.sharpy_dogfood.backends as backends_mod

        original = backends_mod._TIMEOUT_EXCEPTION_TYPES
        # Empty tuple - should not catch anything
        backends_mod._TIMEOUT_EXCEPTION_TYPES = ()

        try:
            backend._shared_backend = MagicMock()
            backend._shared_backend.execute = AsyncMock(
                side_effect=ValueError("not a timeout")
            )

            with pytest.raises(ValueError, match="not a timeout"):
                await backend.execute("test prompt", timeout=10.0)
        finally:
            backends_mod._TIMEOUT_EXCEPTION_TYPES = original
