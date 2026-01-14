"""
Backend manager with automatic failover.

Manages multiple AI backends (Claude Code, Copilot, etc.) with:
- Primary/fallback backend ordering
- Automatic failover on rate limits
- Per-backend rate limit tracking
- Status monitoring and reporting
- Automatic model selection based on task complexity
"""

from dataclasses import dataclass, field
from typing import Optional
import logging

from .base import Backend, BackendType, BackendConfig, BackendResponse
from ..rate_limiting import RateLimitState
from ..model_selector import ModelSelector, TaskComplexity


logger = logging.getLogger(__name__)


@dataclass
class BackendManagerConfig:
    """Configuration for the backend manager.

    Attributes:
        primary_backend: The preferred backend to use for requests
        fallback_backends: Ordered list of backends to try if primary fails
        rate_limit_window_seconds: Time window for rate limit tracking
        max_requests_per_window: Maximum requests allowed in the window
        auto_failover: If True, automatically switch to fallback on rate limits
    """

    primary_backend: BackendType = BackendType.CLAUDE_CODE
    fallback_backends: list[BackendType] = field(default_factory=list)
    rate_limit_window_seconds: int = 3600
    max_requests_per_window: int = 50
    auto_failover: bool = True


class BackendManager:
    """Manages multiple backends with automatic failover.

    The BackendManager coordinates execution across multiple AI backends,
    automatically switching to fallback backends when the primary is
    rate limited or unavailable.

    Example:
        ```python
        manager = BackendManager()
        manager.register_backend(ClaudeCodeBackend())
        manager.register_backend(CopilotBackend())

        response, backend_used = await manager.execute("Generate code...")
        print(f"Response from {backend_used}: {response.output}")
        ```

    Features:
    - Primary backend preference with configurable fallback order
    - Per-backend rate limit tracking
    - Automatic failover on rate limits (configurable)
    - Status reporting for monitoring
    """

    def __init__(
        self,
        config: Optional[BackendManagerConfig] = None,
        model_selector: Optional[ModelSelector] = None,
    ):
        """Initialize the backend manager.

        Args:
            config: Manager configuration. If None, uses defaults.
            model_selector: Model selector instance for automatic model selection.
                           If None, creates a new one.
        """
        self._config = config or BackendManagerConfig()
        self._backends: dict[BackendType, Backend] = {}
        self._rate_states: dict[BackendType, RateLimitState] = {}
        self._model_selector = model_selector or ModelSelector()

    def register_backend(
        self,
        backend: Backend,
        rate_limit_state: Optional[RateLimitState] = None,
    ) -> None:
        """Register a backend with the manager.

        Args:
            backend: The backend instance to register
            rate_limit_state: Optional rate limit state for this backend.
                If None, creates a new one.
        """
        backend_type = backend.backend_type
        self._backends[backend_type] = backend
        self._rate_states[backend_type] = rate_limit_state or RateLimitState()
        logger.debug(f"Registered backend: {backend_type.value}")

    def unregister_backend(self, backend_type: BackendType) -> bool:
        """Unregister a backend from the manager.

        Args:
            backend_type: The type of backend to remove

        Returns:
            True if backend was removed, False if it wasn't registered
        """
        if backend_type in self._backends:
            del self._backends[backend_type]
            del self._rate_states[backend_type]
            logger.debug(f"Unregistered backend: {backend_type.value}")
            return True
        return False

    async def execute(
        self,
        prompt: str,
        config: Optional[BackendConfig] = None,
        preferred_backend: Optional[BackendType] = None,
    ) -> tuple[BackendResponse, BackendType]:
        """Execute prompt with automatic backend selection and failover.

        Tries backends in order: preferred (if specified), primary, then fallbacks.
        If a backend is rate limited and auto_failover is enabled, automatically
        tries the next available backend.

        If model is not specified in config but task_type is provided, automatically
        selects the appropriate model using the ModelSelector.

        Args:
            prompt: The instruction/prompt to send to the AI backend
            config: Optional execution configuration
            preferred_backend: Specific backend to prefer for this request

        Returns:
            Tuple of (response, backend_type that was used)

        Raises:
            RuntimeError: If no backends are registered or all backends are unavailable
        """
        if not self._backends:
            raise RuntimeError("No backends registered with the manager")

        # Perform automatic model selection if needed
        config = self._apply_model_selection(config)

        # Build ordered list of backends to try
        backends_to_try = self._get_backend_order(preferred_backend)

        if not backends_to_try:
            raise RuntimeError(
                "No backends available. All backends may be rate limited or "
                "unavailable. Use get_backend_status() to check state."
            )

        last_error: Optional[str] = None

        for backend_type in backends_to_try:
            backend = self._backends[backend_type]
            rate_state = self._rate_states[backend_type]

            # Check rate limit window capacity
            requests_in_window = rate_state.requests_in_window(
                self._config.rate_limit_window_seconds
            )
            if requests_in_window >= self._config.max_requests_per_window:
                logger.debug(
                    f"Backend {backend_type.value} at capacity: "
                    f"{requests_in_window}/{self._config.max_requests_per_window} requests"
                )
                if self._config.auto_failover:
                    continue

            # Check if backend reports itself as available
            if not backend.is_available():
                logger.debug(f"Backend {backend_type.value} reports unavailable")
                if self._config.auto_failover:
                    continue

            # Try to execute on this backend
            logger.debug(f"Executing on backend: {backend_type.value}")
            response = await backend.execute(prompt, config)

            # Track the request in our rate state
            rate_state.record_request()

            # Handle rate limit response
            if response.rate_limited:
                logger.warning(
                    f"Backend {backend_type.value} returned rate limit. "
                    f"Error: {response.error_message}"
                )
                rate_state.record_error()
                last_error = response.error_message

                if self._config.auto_failover:
                    continue
                else:
                    # Return the rate limited response if failover disabled
                    return (response, backend_type)

            # Handle other failures
            if not response.success:
                logger.warning(
                    f"Backend {backend_type.value} execution failed: "
                    f"{response.error_message}"
                )
                rate_state.record_error()
                last_error = response.error_message

                # For non-rate-limit failures, we still try fallback
                if self._config.auto_failover:
                    continue
                else:
                    return (response, backend_type)

            # Success! Record it and return
            rate_state.record_success()
            return (response, backend_type)

        # All backends exhausted
        error_msg = (
            f"All backends failed to execute. "
            f"Backends tried: {[bt.value for bt in backends_to_try]}. "
            f"Last error: {last_error}"
        )
        logger.error(error_msg)

        return (
            BackendResponse(
                success=False,
                output="",
                stderr="",
                exit_code=-1,
                duration_seconds=0.0,
                rate_limited=False,
                error_message=error_msg,
            ),
            backends_to_try[-1],  # Report last backend tried
        )

    def _get_backend_order(
        self, preferred: Optional[BackendType] = None
    ) -> list[BackendType]:
        """Get ordered list of backends to try.

        Order: preferred (if specified and registered) -> primary -> fallbacks
        Only includes backends that are actually registered.

        Args:
            preferred: Optional preferred backend type

        Returns:
            Ordered list of backend types to try
        """
        order: list[BackendType] = []
        seen: set[BackendType] = set()

        def add_if_registered(backend_type: BackendType) -> None:
            if backend_type in self._backends and backend_type not in seen:
                order.append(backend_type)
                seen.add(backend_type)

        # 1. Preferred backend (if specified)
        if preferred:
            add_if_registered(preferred)

        # 2. Primary backend from config
        add_if_registered(self._config.primary_backend)

        # 3. Fallback backends in order
        for fallback in self._config.fallback_backends:
            add_if_registered(fallback)

        # 4. Any remaining registered backends (for completeness)
        for backend_type in self._backends:
            add_if_registered(backend_type)

        return order

    def get_available_backends(self) -> list[BackendType]:
        """Get list of backends currently available for requests.

        A backend is considered available if:
        - It is registered
        - It reports itself as available (is_available())
        - It hasn't exceeded the rate limit window capacity

        Returns:
            List of available backend types
        """
        available: list[BackendType] = []

        for backend_type, backend in self._backends.items():
            rate_state = self._rate_states[backend_type]

            # Check rate limit capacity
            requests_in_window = rate_state.requests_in_window(
                self._config.rate_limit_window_seconds
            )
            if requests_in_window >= self._config.max_requests_per_window:
                continue

            # Check backend availability
            if backend.is_available():
                available.append(backend_type)

        return available

    def get_backend_status(self) -> dict[BackendType, dict]:
        """Get detailed status for all registered backends.

        Provides monitoring information including:
        - Whether the backend is currently available
        - Request count in the current window
        - Consecutive error count
        - Time until rate limit reset (if applicable)

        Returns:
            Dictionary mapping backend types to their status dictionaries
        """
        status: dict[BackendType, dict] = {}

        for backend_type, backend in self._backends.items():
            rate_state = self._rate_states[backend_type]
            requests_in_window = rate_state.requests_in_window(
                self._config.rate_limit_window_seconds
            )
            wait_time = rate_state.get_wait_time()

            status[backend_type] = {
                "available": backend.is_available(),
                "registered": True,
                "requests_in_window": requests_in_window,
                "max_requests_per_window": self._config.max_requests_per_window,
                "consecutive_errors": rate_state.consecutive_errors,
                "rate_limited": not rate_state.is_available(),
                "wait_time_seconds": wait_time if wait_time and wait_time > 0 else None,
            }

        return status

    def get_rate_state(self, backend_type: BackendType) -> Optional[RateLimitState]:
        """Get the rate limit state for a specific backend.

        Useful for detailed inspection or external manipulation of rate state.

        Args:
            backend_type: The backend type to get state for

        Returns:
            The rate limit state, or None if backend not registered
        """
        return self._rate_states.get(backend_type)

    def reset_backend_state(self, backend_type: BackendType) -> bool:
        """Reset the rate limit state for a specific backend.

        Clears all error counters and rate limit tracking for the backend.
        Useful for manual recovery or testing.

        Args:
            backend_type: The backend type to reset

        Returns:
            True if backend was found and reset, False otherwise
        """
        if backend_type in self._rate_states:
            self._rate_states[backend_type] = RateLimitState()
            logger.debug(f"Reset rate state for backend: {backend_type.value}")
            return True
        return False

    def _apply_model_selection(
        self, config: Optional[BackendConfig]
    ) -> Optional[BackendConfig]:
        """Apply automatic model selection if model not explicitly specified.

        If config.model is None and config.task_type is provided, uses the
        ModelSelector to determine the appropriate model.

        Args:
            config: The original backend configuration

        Returns:
            Updated config with model selected, or original config if no selection needed
        """
        # If no config or model already specified, no selection needed
        if not config or config.model is not None:
            return config

        # If no task type provided, can't do automatic selection
        if config.task_type is None:
            return config

        # Use default complexity if not specified
        complexity = config.task_complexity or TaskComplexity.MEDIUM

        # Select model based on task characteristics
        recommendation = self._model_selector.select_model(config.task_type, complexity)

        # Log the selection decision
        logger.info(
            f"Auto-selected model '{recommendation.model}' for "
            f"{config.task_type.value} task with {complexity.value} complexity. "
            f"Reasoning: {recommendation.reasoning}"
        )

        # Create new config with selected model (preserve immutability)
        from dataclasses import replace

        return replace(config, model=recommendation.model)

    def __len__(self) -> int:
        """Return the number of registered backends."""
        return len(self._backends)

    def __contains__(self, backend_type: BackendType) -> bool:
        """Check if a backend type is registered."""
        return backend_type in self._backends
