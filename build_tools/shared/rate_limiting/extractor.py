"""
Wait time extraction from rate limit error messages.

This module consolidates logic from:
- build_tools/generate_code_walkthroughs.py (lines 215-280)
- build_tools/sharpy_dogfood/backends.py (lines 127-175)
"""

import re
from datetime import datetime, timedelta
from typing import Optional


def extract_rate_limit_wait_time(output: str, stderr: str = "") -> Optional[int]:
    """
    Extract wait time in seconds from rate limit error messages.

    Handles patterns like:
    - "try again in 5 minutes"
    - "resets at 2am" / "resets 2pm"
    - "wait 30 seconds"
    - "quota resets in 1 hour"
    - "retry after X"
    - "X-minute window" / "X-hour window"

    Args:
        output: The command output to parse
        stderr: The stderr output (optional, will be searched if provided)

    Returns:
        Wait time in seconds, or None if no wait time can be extracted.
        For patterns with explicit times (like "resets at 2am"), calculates
        the time until that hour and returns at least 60 seconds.
    """
    # Combine output and stderr for searching
    combined = f"{output}\n{stderr}".lower()

    # Pattern: "try again in X minutes/hours/seconds"
    match = re.search(r"try again in (\d+)\s*(second|minute|hour)s?", combined)
    if match:
        value = int(match.group(1))
        unit = match.group(2)
        if unit == "hour":
            return value * 3600
        elif unit == "minute":
            return value * 60
        else:
            return value

    # Pattern: "wait X seconds/minutes/hours"
    match = re.search(r"wait (\d+)\s*(second|minute|hour)s?", combined)
    if match:
        value = int(match.group(1))
        unit = match.group(2)
        if unit == "hour":
            return value * 3600
        elif unit == "minute":
            return value * 60
        else:
            return value

    # Pattern: "retry after X seconds"
    match = re.search(r"retry after (\d+)", combined)
    if match:
        return int(match.group(1))

    # Pattern: "resets (at)? Xam/pm" - calculate time until reset
    match = re.search(r"resets\s*(?:at\s*)?(\d{1,2})([ap]m)", combined)
    if match:
        reset_hour = int(match.group(1))
        is_pm = match.group(2) == "pm"

        # Convert to 24-hour format
        if is_pm and reset_hour != 12:
            reset_hour += 12
        elif not is_pm and reset_hour == 12:
            reset_hour = 0

        now = datetime.now()
        reset_time = now.replace(hour=reset_hour, minute=0, second=0, microsecond=0)

        # If reset time has passed today, it must be tomorrow
        if reset_time <= now:
            reset_time = reset_time + timedelta(days=1)

        wait_seconds = int((reset_time - now).total_seconds())
        return max(wait_seconds, 60)  # At least 60 seconds

    # Pattern: "quota resets in X minutes/hours"
    match = re.search(r"quota resets in (\d+)\s*(second|minute|hour)s?", combined)
    if match:
        value = int(match.group(1))
        unit = match.group(2)
        if unit == "hour":
            return value * 3600
        elif unit == "minute":
            return value * 60
        else:
            return value

    # Pattern: "X-minute window" / "X-hour window"
    match = re.search(r"(\d+)[- ](minute|hour)\s*window", combined)
    if match:
        value = int(match.group(1))
        unit = match.group(2)
        return value * 3600 if unit == "hour" else value * 60

    # No recognizable pattern found
    return None


__all__ = ["extract_rate_limit_wait_time"]
