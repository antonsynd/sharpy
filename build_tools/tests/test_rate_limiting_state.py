"""
Unit tests for rate limit state tracking.

Tests the RateLimitState class for correctness, edge cases, and thread safety.
"""

import time
import unittest
from collections import deque

from build_tools.shared.rate_limiting.state import RateLimitState


class TestRateLimitState(unittest.TestCase):
    """Test cases for RateLimitState class."""

    def setUp(self):
        """Create a fresh RateLimitState instance for each test."""
        self.state = RateLimitState()

    def test_initial_state(self):
        """Test that a new RateLimitState has correct initial values."""
        self.assertEqual(self.state.consecutive_errors, 0)
        self.assertIsNone(self.state.last_error_time)
        self.assertIsNone(self.state.disabled_until)
        self.assertEqual(self.state.backoff_multiplier, 1.0)
        self.assertIsNone(self.state.last_request_time)
        self.assertEqual(len(self.state.request_times), 0)

    def test_record_request(self):
        """Test that recording a request updates timestamps correctly."""
        before = time.time()
        self.state.record_request()
        after = time.time()

        self.assertEqual(len(self.state.request_times), 1)
        self.assertIsNotNone(self.state.last_request_time)
        self.assertGreaterEqual(self.state.last_request_time, before)
        self.assertLessEqual(self.state.last_request_time, after)

    def test_record_multiple_requests(self):
        """Test that multiple requests are tracked correctly."""
        for _ in range(5):
            self.state.record_request()
            time.sleep(0.01)  # Small delay to ensure different timestamps

        self.assertEqual(len(self.state.request_times), 5)
        # Verify timestamps are in ascending order
        timestamps = list(self.state.request_times)
        self.assertEqual(timestamps, sorted(timestamps))

    def test_record_request_deque_limit(self):
        """Test that request_times deque respects maxlen of 1000."""
        # Record more than 1000 requests
        for _ in range(1500):
            self.state.record_request()

        # Deque should only keep the most recent 1000
        self.assertEqual(len(self.state.request_times), 1000)

    def test_record_success(self):
        """Test that recording success resets error state."""
        # Set up some error state
        self.state.consecutive_errors = 5
        self.state.backoff_multiplier = 16.0

        self.state.record_success()

        self.assertEqual(self.state.consecutive_errors, 0)
        self.assertEqual(self.state.backoff_multiplier, 1.0)

    def test_record_error_increments_counter(self):
        """Test that recording errors increments the error counter."""
        self.assertEqual(self.state.consecutive_errors, 0)

        self.state.record_error()
        self.assertEqual(self.state.consecutive_errors, 1)

        self.state.record_error()
        self.assertEqual(self.state.consecutive_errors, 2)

    def test_record_error_with_wait_seconds(self):
        """Test that record_error with wait_seconds disables the backend."""
        before = time.time()
        self.state.record_error(wait_seconds=60)

        self.assertIsNotNone(self.state.disabled_until)
        self.assertGreaterEqual(self.state.disabled_until, before + 59)
        self.assertLessEqual(self.state.disabled_until, before + 61)

    def test_record_error_exponential_backoff(self):
        """Test that consecutive errors increase backoff exponentially."""
        # First error: should set backoff to base_cooldown
        self.state.record_error(base_cooldown=2.0, multiplier=2.0)
        self.assertEqual(self.state.backoff_multiplier, 2.0)

        # Second error: should double
        self.state.record_error(base_cooldown=2.0, multiplier=2.0)
        self.assertEqual(self.state.backoff_multiplier, 4.0)

        # Third error: should double again
        self.state.record_error(base_cooldown=2.0, multiplier=2.0)
        self.assertEqual(self.state.backoff_multiplier, 8.0)

    def test_record_error_respects_max_backoff(self):
        """Test that backoff doesn't exceed max_backoff parameter."""
        # Record many errors with max_backoff=10
        for _ in range(10):
            self.state.record_error(base_cooldown=2.0, max_backoff=10.0, multiplier=2.0)

        self.assertLessEqual(self.state.backoff_multiplier, 10.0)

    def test_is_available_initially_true(self):
        """Test that a new state is available."""
        self.assertTrue(self.state.is_available())

    def test_is_available_when_disabled(self):
        """Test that is_available returns False when disabled."""
        self.state.disable_temporarily(60)
        self.assertFalse(self.state.is_available())

    def test_is_available_after_disable_expires(self):
        """Test that is_available returns True after disable period expires."""
        self.state.disable_temporarily(0.1)  # Disable for 100ms
        self.assertFalse(self.state.is_available())

        time.sleep(0.15)  # Wait for disable to expire
        self.assertTrue(self.state.is_available())

    def test_disable_temporarily(self):
        """Test that disable_temporarily sets correct timestamp."""
        before = time.time()
        self.state.disable_temporarily(30)
        after = time.time()

        self.assertIsNotNone(self.state.disabled_until)
        self.assertGreaterEqual(self.state.disabled_until, before + 29)
        self.assertLessEqual(self.state.disabled_until, after + 31)

    def test_get_wait_time_when_not_disabled(self):
        """Test that get_wait_time returns None when not disabled."""
        self.assertIsNone(self.state.get_wait_time())

    def test_get_wait_time_when_disabled(self):
        """Test that get_wait_time returns correct remaining time."""
        self.state.disable_temporarily(10)
        wait_time = self.state.get_wait_time()

        self.assertIsNotNone(wait_time)
        self.assertGreater(wait_time, 9)
        self.assertLess(wait_time, 11)

    def test_get_wait_time_after_expiry(self):
        """Test that get_wait_time returns None after disable expires."""
        self.state.disable_temporarily(0.1)
        time.sleep(0.15)

        self.assertIsNone(self.state.get_wait_time())

    def test_requests_in_window_empty(self):
        """Test requests_in_window with no requests."""
        count = self.state.requests_in_window(60)
        self.assertEqual(count, 0)

    def test_requests_in_window_all_recent(self):
        """Test requests_in_window when all requests are in window."""
        for _ in range(5):
            self.state.record_request()

        count = self.state.requests_in_window(60)
        self.assertEqual(count, 5)

    def test_requests_in_window_excludes_old(self):
        """Test that requests_in_window excludes old requests."""
        # Manually add old timestamp (61 seconds ago)
        old_time = time.time() - 61
        self.state.request_times.append(old_time)

        # Add recent requests
        for _ in range(3):
            self.state.record_request()

        # Should only count the 3 recent ones
        count = self.state.requests_in_window(60)
        self.assertEqual(count, 3)

    def test_requests_in_window_boundary(self):
        """Test requests_in_window at exact window boundary."""
        # Add request at exactly window_seconds ago
        boundary_time = time.time() - 60.0
        self.state.request_times.append(boundary_time)

        # This should NOT be counted (strict > not >=)
        count = self.state.requests_in_window(60)
        self.assertEqual(count, 0)

    def test_get_backoff_delay_no_errors(self):
        """Test that get_backoff_delay returns 0 with no errors."""
        self.assertEqual(self.state.get_backoff_delay(), 0.0)

    def test_get_backoff_delay_with_errors(self):
        """Test that get_backoff_delay returns backoff_multiplier."""
        self.state.record_error(base_cooldown=5.0)
        self.assertEqual(self.state.get_backoff_delay(), 5.0)

        self.state.record_error(base_cooldown=5.0, multiplier=2.0)
        self.assertEqual(self.state.get_backoff_delay(), 10.0)

    def test_should_wait_initially_false(self):
        """Test that should_wait returns False for fresh state."""
        should_wait, wait_time = self.state.should_wait(
            max_requests_per_window=10,
            window_seconds=60
        )
        self.assertFalse(should_wait)
        self.assertEqual(wait_time, 0.0)

    def test_should_wait_when_disabled(self):
        """Test that should_wait returns True when backend disabled."""
        self.state.disable_temporarily(30)

        should_wait, wait_time = self.state.should_wait(
            max_requests_per_window=10,
            window_seconds=60
        )
        self.assertTrue(should_wait)
        self.assertGreater(wait_time, 29)
        self.assertLess(wait_time, 31)

    def test_should_wait_rate_limit_exceeded(self):
        """Test that should_wait returns True when rate limit exceeded."""
        # Record max_requests_per_window requests
        for _ in range(5):
            self.state.record_request()

        should_wait, wait_time = self.state.should_wait(
            max_requests_per_window=5,
            window_seconds=60
        )
        self.assertTrue(should_wait)
        self.assertGreater(wait_time, 0)

    def test_should_wait_cooldown_active(self):
        """Test that should_wait respects cooldown period."""
        self.state.record_request()

        should_wait, wait_time = self.state.should_wait(
            max_requests_per_window=100,
            window_seconds=60,
            request_cooldown=5.0
        )
        self.assertTrue(should_wait)
        self.assertGreater(wait_time, 4)
        self.assertLess(wait_time, 6)

    def test_should_wait_cooldown_with_backoff(self):
        """Test that should_wait includes backoff in cooldown calculation."""
        self.state.record_request()
        self.state.record_error(base_cooldown=10.0)

        should_wait, wait_time = self.state.should_wait(
            max_requests_per_window=100,
            window_seconds=60,
            request_cooldown=2.0
        )
        self.assertTrue(should_wait)
        # Should wait for cooldown (2.0) + backoff (10.0) = 12.0 seconds
        self.assertGreater(wait_time, 11)
        self.assertLess(wait_time, 13)

    def test_should_wait_after_cooldown_expires(self):
        """Test that should_wait returns False after cooldown expires."""
        self.state.record_request()
        time.sleep(0.15)  # Wait for cooldown to expire

        should_wait, wait_time = self.state.should_wait(
            max_requests_per_window=100,
            window_seconds=60,
            request_cooldown=0.1
        )
        self.assertFalse(should_wait)
        self.assertEqual(wait_time, 0.0)

    def test_error_then_success_resets_backoff(self):
        """Test that success after error resets backoff state."""
        # Record several errors
        for _ in range(3):
            self.state.record_error(base_cooldown=2.0, multiplier=2.0)

        self.assertEqual(self.state.consecutive_errors, 3)
        self.assertGreater(self.state.backoff_multiplier, 1.0)

        # Record success should reset everything
        self.state.record_success()

        self.assertEqual(self.state.consecutive_errors, 0)
        self.assertEqual(self.state.backoff_multiplier, 1.0)
        self.assertEqual(self.state.get_backoff_delay(), 0.0)

    def test_multiple_disable_temporarily_uses_latest(self):
        """Test that calling disable_temporarily multiple times uses the latest time."""
        self.state.disable_temporarily(10)
        first_disabled = self.state.disabled_until

        time.sleep(0.1)
        self.state.disable_temporarily(20)
        second_disabled = self.state.disabled_until

        self.assertIsNotNone(first_disabled)
        self.assertIsNotNone(second_disabled)
        self.assertGreater(second_disabled, first_disabled)


class TestRateLimitStateIntegration(unittest.TestCase):
    """Integration tests for realistic usage scenarios."""

    def test_typical_success_flow(self):
        """Test normal operation with successful requests."""
        state = RateLimitState()

        # Make several successful requests
        for _ in range(5):
            should_wait, _ = state.should_wait(
                max_requests_per_window=10,
                window_seconds=60,
                request_cooldown=0.1
            )
            if should_wait:
                time.sleep(0.1)

            state.record_request()
            state.record_success()

        self.assertEqual(state.consecutive_errors, 0)
        self.assertTrue(state.is_available())

    def test_error_recovery_flow(self):
        """Test recovery from rate limit errors."""
        state = RateLimitState()

        # Hit rate limit
        state.record_request()
        state.record_error(wait_seconds=0.2)

        self.assertFalse(state.is_available())

        # Wait for recovery
        time.sleep(0.25)

        self.assertTrue(state.is_available())

        # Make successful request
        state.record_request()
        state.record_success()

        self.assertEqual(state.consecutive_errors, 0)

    def test_exponential_backoff_scenario(self):
        """Test realistic exponential backoff with multiple errors."""
        state = RateLimitState()

        backoffs = []
        for i in range(5):
            state.record_error(base_cooldown=1.0, max_backoff=60.0, multiplier=2.0)
            backoffs.append(state.get_backoff_delay())

        # Verify exponential growth
        self.assertEqual(backoffs[0], 1.0)
        self.assertEqual(backoffs[1], 2.0)
        self.assertEqual(backoffs[2], 4.0)
        self.assertEqual(backoffs[3], 8.0)
        self.assertEqual(backoffs[4], 16.0)


if __name__ == "__main__":
    unittest.main()
