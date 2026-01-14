"""
Rate limit error detection for AI backend responses.

This module provides utilities to detect rate limit errors in output from
various AI backends (Claude Code, GitHub Copilot, etc.).
"""

from typing import Final

# Indicators that suggest a rate limit has been hit
RATE_LIMIT_INDICATORS: Final[list[str]] = [
    "rate_limited",
    "rate limit",
    "rate-limit",
    "429",  # HTTP status code for Too Many Requests
    "too many requests",
    "exceeded your copilot token usage",
    "hit your limit",
    "quota exceeded",
    "resets 2am",
    "resets at",
    "try again later",
    "try again",
    "request limit",
]


def is_rate_limit_error(output: str, stderr: str = "") -> bool:
    """
    Detect if output indicates a rate limit error.

    This function checks both stdout and stderr for common rate limit
    error patterns from various AI backends.

    Args:
        output: The stdout/main output text from the backend
        stderr: The stderr text from the backend (optional)

    Returns:
        True if rate limit indicators are detected, False otherwise

    Examples:
        >>> is_rate_limit_error("Rate limit exceeded, try again later")
        True
        >>> is_rate_limit_error("Success! Task completed")
        False
        >>> is_rate_limit_error("", "Error: 429 Too Many Requests")
        True
    """
    # Combine both streams for detection
    combined_output = (output + " " + stderr).lower()

    # Check if any indicator is present
    return any(indicator in combined_output for indicator in RATE_LIMIT_INDICATORS)
