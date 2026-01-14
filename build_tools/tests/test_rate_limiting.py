"""Unit tests for rate limit detection."""

import pytest
from build_tools.shared.rate_limiting.detector import (
    is_rate_limit_error,
    RATE_LIMIT_INDICATORS,
)


class TestRateLimitDetection:
    """Tests for rate limit error detection."""

    def test_detects_rate_limit_message_in_output(self):
        """Should detect 'rate limit' in output."""
        assert is_rate_limit_error("Rate limit exceeded", "")
        assert is_rate_limit_error("You have hit a rate limit", "")
        assert is_rate_limit_error("Error: rate-limit", "")

    def test_detects_rate_limit_message_in_stderr(self):
        """Should detect rate limit in stderr."""
        assert is_rate_limit_error("", "Too many requests")
        assert is_rate_limit_error("", "Rate limit exceeded")

    def test_detects_rate_limit_in_combined_output(self):
        """Should detect rate limit when split across stdout/stderr."""
        assert is_rate_limit_error("Error occurred", "rate limit")
        assert is_rate_limit_error("Request failed", "429 Too Many Requests")

    def test_case_insensitive_detection(self):
        """Detection should be case-insensitive."""
        assert is_rate_limit_error("RATE LIMIT EXCEEDED", "")
        assert is_rate_limit_error("Rate Limit Exceeded", "")
        assert is_rate_limit_error("rate limit exceeded", "")

    def test_detects_http_429_status(self):
        """Should detect HTTP 429 status code."""
        assert is_rate_limit_error("HTTP Error 429", "")
        assert is_rate_limit_error("Status: 429", "")

    def test_detects_copilot_specific_messages(self):
        """Should detect Copilot-specific rate limit messages."""
        assert is_rate_limit_error("exceeded your copilot token usage", "")
        assert is_rate_limit_error("hit your limit", "")

    def test_detects_quota_exceeded(self):
        """Should detect quota exceeded messages."""
        assert is_rate_limit_error("quota exceeded", "")
        assert is_rate_limit_error("Error: quota exceeded, please wait", "")

    def test_detects_reset_time_messages(self):
        """Should detect messages about rate limit reset times."""
        assert is_rate_limit_error("resets at 2am", "")
        assert is_rate_limit_error("Rate limit resets 2am", "")
        assert is_rate_limit_error("resets at midnight", "")

    def test_detects_try_again_messages(self):
        """Should detect 'try again' messages."""
        assert is_rate_limit_error("try again later", "")
        assert is_rate_limit_error("Please try again in a few minutes", "")

    def test_does_not_detect_success_messages(self):
        """Should not detect rate limits in success messages."""
        assert not is_rate_limit_error("Success! Task completed", "")
        assert not is_rate_limit_error("Operation successful", "")
        assert not is_rate_limit_error("Done", "")

    def test_does_not_detect_other_errors(self):
        """Should not detect non-rate-limit errors."""
        assert not is_rate_limit_error("Syntax error", "")
        assert not is_rate_limit_error("File not found", "")
        assert not is_rate_limit_error("Compilation failed", "")

    def test_handles_empty_strings(self):
        """Should handle empty strings without error."""
        assert not is_rate_limit_error("", "")
        assert not is_rate_limit_error("", "")

    def test_handles_none_as_empty_string(self):
        """Should handle stderr defaulting to empty string."""
        assert is_rate_limit_error("rate limit exceeded")
        assert not is_rate_limit_error("success")

    def test_detects_request_limit(self):
        """Should detect 'request limit' messages."""
        assert is_rate_limit_error("request limit reached", "")
        assert is_rate_limit_error("You've exceeded the request limit", "")

    def test_all_indicators_in_constant(self):
        """Verify RATE_LIMIT_INDICATORS contains expected patterns."""
        assert "rate limit" in RATE_LIMIT_INDICATORS
        assert "429" in RATE_LIMIT_INDICATORS
        assert "too many requests" in RATE_LIMIT_INDICATORS
        assert "quota exceeded" in RATE_LIMIT_INDICATORS
        assert len(RATE_LIMIT_INDICATORS) > 10  # Should have comprehensive list

    def test_real_world_claude_error(self):
        """Should detect real-world Claude rate limit error."""
        error = """
        Error: Rate limit exceeded. Your request limit resets at 2am PST.
        Please try again later.
        """
        assert is_rate_limit_error(error, "")

    def test_real_world_copilot_error(self):
        """Should detect real-world Copilot rate limit error."""
        error = (
            "You have exceeded your copilot token usage limit. Please try again later."
        )
        assert is_rate_limit_error(error, "")

    def test_real_world_http_429_error(self):
        """Should detect real-world HTTP 429 error."""
        stderr = "HTTP/1.1 429 Too Many Requests"
        assert is_rate_limit_error("", stderr)

    def test_partial_matches(self):
        """Should match indicators even as part of larger words/phrases."""
        assert is_rate_limit_error("rate_limited_error", "")
        assert is_rate_limit_error("The rate-limit has been exceeded", "")
