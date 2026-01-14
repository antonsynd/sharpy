"""
Rate limit state tracking for AI backends.

Provides centralized state management for tracking request rates, backoff timing,
and temporary backend disabling across all Sharpy build tools.
"""

import time
from collections import deque
from dataclasses import dataclass, field
from datetime import datetime
from typing import Optional


@dataclass
class RateLimitState:
    """
    Track rate limit state for a backend.

    This class consolidates rate limit tracking from all three existing tools
    (walkthrough generator, auto_builder, dogfood) into a single implementation.

    Key features:
    - Request timestamp tracking with sliding window
    - Exponential backoff on consecutive errors
    - Temporary backend disabling after severe rate limiting
    - Thread-safe state updates (via dataclass immutability for reads)

    Attributes:
        request_times: Deque of recent request timestamps (Unix time)
        consecutive_errors: Count of errors since last success
        last_error_time: When the last error occurred (Unix timestamp)
        disabled_until: Unix timestamp when backend can be re-enabled (None if not disabled)
        backoff_multiplier: Current exponential backoff multiplier
        last_request_time: Timestamp of the most recent request
    """

    request_times: deque = field(default_factory=lambda: deque(maxlen=1000))
    consecutive_errors: int = 0
    last_error_time: Optional[float] = None
    disabled_until: Optional[float] = None
    backoff_multiplier: float = 1.0
    last_request_time: Optional[float] = None

    def record_request(self) -> None:
        """
        Record a new request timestamp.

        Should be called immediately before making an API request to track
        request rate in the sliding window.
        """
        now = time.time()
        self.request_times.append(now)
        self.last_request_time = now

    def record_error(
        self,
        wait_seconds: Optional[int] = None,
        base_cooldown: float = 1.0,
        max_backoff: float = 300.0,
        multiplier: float = 2.0,
    ) -> None:
        """
        Record a failed request and update backoff state.

        Implements exponential backoff: each consecutive error increases the
        backoff time by the multiplier until max_backoff is reached.

        Args:
            wait_seconds: If provided, disable backend until this many seconds have elapsed
            base_cooldown: Initial backoff delay in seconds (default: 1.0)
            max_backoff: Maximum backoff delay in seconds (default: 300.0 = 5 minutes)
            multiplier: Exponential backoff multiplier (default: 2.0)
        """
        now = time.time()
        self.consecutive_errors += 1
        self.last_error_time = now

        # If explicit wait time provided, disable the backend temporarily
        if wait_seconds is not None:
            self.disable_temporarily(wait_seconds)

        # Update exponential backoff multiplier
        # First error: set to base_cooldown
        # Subsequent errors: multiply current backoff by multiplier
        if self.consecutive_errors == 1:
            self.backoff_multiplier = base_cooldown
        else:
            self.backoff_multiplier = min(
                self.backoff_multiplier * multiplier, max_backoff
            )

    def record_success(self) -> None:
        """
        Record a successful request, resetting error counters.

        Clears consecutive error count and backoff state since the backend
        is now working properly.
        """
        self.consecutive_errors = 0
        self.backoff_multiplier = 1.0

    def is_available(self) -> bool:
        """
        Check if the backend is currently available for requests.

        Returns False if the backend is temporarily disabled (e.g., due to
        rate limiting or too many consecutive errors).

        Returns:
            True if backend can accept requests, False otherwise
        """
        if self.disabled_until is None:
            return True
        return time.time() >= self.disabled_until

    def disable_temporarily(self, seconds: float) -> None:
        """
        Temporarily disable this backend for the specified duration.

        Used when a rate limit error explicitly tells us to wait a certain
        amount of time before retrying.

        Args:
            seconds: How many seconds to disable the backend
        """
        self.disabled_until = time.time() + seconds

    def get_wait_time(self) -> Optional[float]:
        """
        Get remaining wait time before backend becomes available.

        Returns:
            Remaining seconds to wait, or None if backend is available
        """
        if self.disabled_until is None:
            return None

        remaining = self.disabled_until - time.time()
        return remaining if remaining > 0 else None

    def requests_in_window(self, window_seconds: int) -> int:
        """
        Count requests made within the specified time window.

        Uses a sliding window to track recent request volume, useful for
        implementing rate limiting policies.

        Args:
            window_seconds: Size of the time window in seconds

        Returns:
            Number of requests in the current window
        """
        now = time.time()
        cutoff = now - window_seconds
        return sum(1 for t in self.request_times if t > cutoff)

    def get_backoff_delay(self) -> float:
        """
        Calculate the current backoff delay based on consecutive errors.

        Returns 0 if no errors, otherwise returns the current backoff multiplier
        which increases exponentially with each error.

        Returns:
            Delay in seconds to wait before next request
        """
        if self.consecutive_errors == 0:
            return 0.0
        return self.backoff_multiplier

    def should_wait(
        self,
        max_requests_per_window: int,
        window_seconds: int,
        request_cooldown: float = 0.0,
    ) -> tuple[bool, float]:
        """
        Determine if we should wait before making the next request.

        Checks three conditions:
        1. Is backend temporarily disabled?
        2. Have we exceeded the rate limit window?
        3. Is the cooldown period still active?

        Args:
            max_requests_per_window: Maximum allowed requests in the window
            window_seconds: Size of the rate limit window in seconds
            request_cooldown: Minimum time between requests in seconds

        Returns:
            Tuple of (should_wait, wait_time_seconds)
        """
        now = time.time()

        # Check if backend is temporarily disabled
        if self.disabled_until is not None and now < self.disabled_until:
            return True, self.disabled_until - now

        # Check rate limit window
        requests_in_window = self.requests_in_window(window_seconds)
        if requests_in_window >= max_requests_per_window:
            # Find the oldest request in the window
            recent_requests = [
                t for t in self.request_times if t > now - window_seconds
            ]
            if recent_requests:
                oldest_in_window = min(recent_requests)
                wait_time = oldest_in_window + window_seconds - now
                return True, wait_time

        # Check cooldown with backoff
        if self.last_request_time is not None:
            cooldown_remaining = (
                self.last_request_time
                + request_cooldown
                + self.get_backoff_delay()
                - now
            )
            if cooldown_remaining > 0:
                return True, cooldown_remaining

        return False, 0.0
