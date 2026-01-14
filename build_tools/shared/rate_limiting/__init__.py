"""
Rate limiting detection and handling utilities.

Provides functionality to:
- Detect rate limit errors from AI backend responses
- Extract wait times from error messages
- Track rate limit state across multiple requests
"""

from .detector import is_rate_limit_error, RATE_LIMIT_INDICATORS
from .extractor import extract_rate_limit_wait_time
from .state import RateLimitState

__all__ = [
    "is_rate_limit_error",
    "RATE_LIMIT_INDICATORS",
    "extract_rate_limit_wait_time",
    "RateLimitState",
]
