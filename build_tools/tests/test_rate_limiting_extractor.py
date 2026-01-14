"""
Unit tests for rate limit wait time extraction.
"""

import unittest
from datetime import datetime, timedelta
from build_tools.shared.rate_limiting.extractor import extract_rate_limit_wait_time


class TestExtractRateLimitWaitTime(unittest.TestCase):
    """Test extraction of wait times from error messages."""

    def test_try_again_in_minutes(self):
        """Test parsing 'try again in X minutes' pattern."""
        assert (
            extract_rate_limit_wait_time("Rate limit exceeded. Try again in 5 minutes.")
            == 300
        )
        assert extract_rate_limit_wait_time("Please try again in 10 minutes") == 600
        assert extract_rate_limit_wait_time("try again in 1 minute") == 60

    def test_try_again_in_seconds(self):
        """Test parsing 'try again in X seconds' pattern."""
        assert extract_rate_limit_wait_time("Try again in 30 seconds") == 30
        assert extract_rate_limit_wait_time("try again in 45 second") == 45

    def test_try_again_in_hours(self):
        """Test parsing 'try again in X hours' pattern."""
        assert extract_rate_limit_wait_time("Try again in 1 hour") == 3600
        assert extract_rate_limit_wait_time("try again in 2 hours") == 7200

    def test_wait_x_seconds(self):
        """Test parsing 'wait X seconds' pattern."""
        assert extract_rate_limit_wait_time("Please wait 30 seconds") == 30
        assert extract_rate_limit_wait_time("wait 60 seconds before retrying") == 60
        assert extract_rate_limit_wait_time("wait 1 second") == 1

    def test_wait_x_minutes(self):
        """Test parsing 'wait X minutes' pattern."""
        assert extract_rate_limit_wait_time("Please wait 5 minutes") == 300
        assert extract_rate_limit_wait_time("wait 10 minutes") == 600

    def test_wait_x_hours(self):
        """Test parsing 'wait X hours' pattern."""
        assert extract_rate_limit_wait_time("Please wait 1 hour") == 3600
        assert extract_rate_limit_wait_time("wait 2 hours") == 7200

    def test_retry_after_seconds(self):
        """Test parsing 'retry after X' pattern."""
        assert extract_rate_limit_wait_time("Retry after 60") == 60
        assert extract_rate_limit_wait_time("retry after 120 seconds") == 120

    def test_quota_resets_in_minutes(self):
        """Test parsing 'quota resets in X minutes' pattern."""
        assert extract_rate_limit_wait_time("Quota resets in 15 minutes") == 900
        assert extract_rate_limit_wait_time("quota resets in 30 minutes") == 1800

    def test_quota_resets_in_hours(self):
        """Test parsing 'quota resets in X hours' pattern."""
        assert extract_rate_limit_wait_time("Quota resets in 1 hour") == 3600
        assert extract_rate_limit_wait_time("quota resets in 2 hours") == 7200

    def test_x_minute_window(self):
        """Test parsing 'X-minute window' pattern."""
        assert extract_rate_limit_wait_time("60-minute window exceeded") == 3600
        assert extract_rate_limit_wait_time("30 minute window") == 1800

    def test_x_hour_window(self):
        """Test parsing 'X-hour window' pattern."""
        assert extract_rate_limit_wait_time("1-hour window exceeded") == 3600
        assert extract_rate_limit_wait_time("2 hour window") == 7200

    def test_resets_at_am(self):
        """Test parsing 'resets at Xam' pattern."""
        # This is time-dependent, so we calculate expected value
        now = datetime.now()

        # Test with a time that should be in the future today
        future_hour = (now.hour + 2) % 24
        am_pm = "am" if future_hour < 12 else "pm"
        display_hour = future_hour if future_hour <= 12 else future_hour - 12
        if display_hour == 0:
            display_hour = 12

        message = f"Rate limit resets at {display_hour}{am_pm}"
        result = extract_rate_limit_wait_time(message)

        # Should return a positive value (at least 60 seconds)
        assert result is not None
        assert result >= 60

    def test_resets_at_pm(self):
        """Test parsing 'resets at Xpm' pattern."""
        now = datetime.now()

        # Test with 2pm
        message = "Rate limit resets at 2pm"
        result = extract_rate_limit_wait_time(message)

        # Should return a positive value
        assert result is not None
        assert result >= 60

    def test_resets_at_midnight(self):
        """Test parsing 'resets at 12am' (midnight)."""
        message = "Rate limit resets at 12am"
        result = extract_rate_limit_wait_time(message)

        # Should return time until midnight (or tomorrow if past midnight)
        assert result is not None
        assert result >= 60

    def test_resets_at_noon(self):
        """Test parsing 'resets at 12pm' (noon)."""
        message = "Rate limit resets at 12pm"
        result = extract_rate_limit_wait_time(message)

        # Should return time until noon (or tomorrow if past noon)
        assert result is not None
        assert result >= 60

    def test_resets_without_at(self):
        """Test parsing 'resets Xam/pm' without 'at'."""
        message = "Rate limit resets 3pm"
        result = extract_rate_limit_wait_time(message)

        assert result is not None
        assert result >= 60

    def test_case_insensitive(self):
        """Test that matching is case-insensitive."""
        assert extract_rate_limit_wait_time("TRY AGAIN IN 5 MINUTES") == 300
        assert extract_rate_limit_wait_time("WAIT 30 SECONDS") == 30
        assert extract_rate_limit_wait_time("Rate Limit Resets At 2PM") >= 60

    def test_stderr_parameter(self):
        """Test that stderr is also searched."""
        # Error message in stderr
        result = extract_rate_limit_wait_time("", stderr="Try again in 10 minutes")
        assert result == 600

        # Error split between output and stderr
        result = extract_rate_limit_wait_time(
            "Rate limit exceeded.", stderr="Try again in 5 minutes"
        )
        assert result == 300

    def test_no_match_returns_none(self):
        """Test that unrecognized patterns return None."""
        assert extract_rate_limit_wait_time("Success!") is None
        assert extract_rate_limit_wait_time("Unknown error") is None
        assert extract_rate_limit_wait_time("") is None

    def test_partial_matches_ignored(self):
        """Test that partial matches don't cause false positives."""
        # "try again" without time should not match
        assert extract_rate_limit_wait_time("Please try again later") is None

        # "wait" without time should not match
        assert extract_rate_limit_wait_time("Please wait") is None

    def test_first_match_wins(self):
        """Test that only the first pattern match is returned."""
        message = "Try again in 5 minutes. Quota resets in 30 minutes."
        # Should match the first pattern (5 minutes)
        assert extract_rate_limit_wait_time(message) == 300

    def test_real_world_claude_error(self):
        """Test with a real-world Claude API rate limit error."""
        message = """
        Error: Rate limit exceeded for model claude-3-5-sonnet-20241022.
        Your usage has exceeded the rate limit. Please try again in 30 seconds.
        """
        assert extract_rate_limit_wait_time(message) == 30

    def test_real_world_anthropic_error(self):
        """Test with real Anthropic API error format."""
        message = "rate_limit_error: Rate limit exceeded. Quota resets at 2am."
        result = extract_rate_limit_wait_time(message)
        assert result is not None
        assert result >= 60

    def test_multiline_messages(self):
        """Test extraction from multiline error messages."""
        message = """
        Error: Too many requests
        Rate limit exceeded
        Try again in 15 minutes
        Contact support if this persists
        """
        assert extract_rate_limit_wait_time(message) == 900


if __name__ == "__main__":
    unittest.main()
