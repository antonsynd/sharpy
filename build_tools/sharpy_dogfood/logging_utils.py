"""
Logging utilities for the dogfooding tool.

Provides consistent logging with timestamps and levels.
"""

import sys
import logging
from datetime import datetime
from pathlib import Path
from typing import Optional


class DogfoodLogger:
    """Logger for the dogfooding tool with file and console output."""

    def __init__(
        self,
        name: str = "sharpy_dogfood",
        log_file: Optional[Path] = None,
        level: int = logging.INFO,
    ):
        self.logger = logging.getLogger(name)
        self.logger.setLevel(level)
        self.logger.handlers = []  # Clear any existing handlers

        # Console handler (stderr)
        console_handler = logging.StreamHandler(sys.stderr)
        console_handler.setLevel(level)
        console_format = logging.Formatter(
            "%(asctime)s [%(levelname)s] %(message)s", datefmt="%H:%M:%S"
        )
        console_handler.setFormatter(console_format)
        self.logger.addHandler(console_handler)

        # File handler (if specified)
        if log_file:
            log_file.parent.mkdir(parents=True, exist_ok=True)
            file_handler = logging.FileHandler(log_file)
            file_handler.setLevel(level)
            file_format = logging.Formatter(
                "%(asctime)s [%(levelname)s] %(name)s: %(message)s",
                datefmt="%Y-%m-%d %H:%M:%S",
            )
            file_handler.setFormatter(file_format)
            self.logger.addHandler(file_handler)

    def debug(self, msg: str, *args, **kwargs) -> None:
        self.logger.debug(msg, *args, **kwargs)

    def info(self, msg: str, *args, **kwargs) -> None:
        self.logger.info(msg, *args, **kwargs)

    def warning(self, msg: str, *args, **kwargs) -> None:
        self.logger.warning(msg, *args, **kwargs)

    def error(self, msg: str, *args, **kwargs) -> None:
        self.logger.error(msg, *args, **kwargs)

    def critical(self, msg: str, *args, **kwargs) -> None:
        self.logger.critical(msg, *args, **kwargs)

    def section(self, title: str) -> None:
        """Log a section header."""
        self.info("=" * 60)
        self.info(title)
        self.info("=" * 60)

    def step(self, step_num: int, total: int, description: str) -> None:
        """Log a step in a process."""
        self.info(f"[{step_num}/{total}] {description}")

    def success(self, msg: str) -> None:
        """Log a success message."""
        self.info(f"✓ {msg}")

    def failure(self, msg: str) -> None:
        """Log a failure message."""
        self.error(f"✗ {msg}")


# Global logger instance
_logger: Optional[DogfoodLogger] = None


def get_logger(
    log_file: Optional[Path] = None,
    level: int = logging.INFO,
) -> DogfoodLogger:
    """Get or create the global logger instance."""
    global _logger
    if _logger is None:
        _logger = DogfoodLogger(log_file=log_file, level=level)
    return _logger


def set_logger(logger: DogfoodLogger) -> None:
    """Set the global logger instance."""
    global _logger
    _logger = logger
