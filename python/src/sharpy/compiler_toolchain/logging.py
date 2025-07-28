import logging
import sys
from pathlib import Path

_formatter: logging.Formatter = logging.Formatter(
    "%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)


class Logger:
    def __init__(self, name: str):
        self._logger: logging.Logger = logging.getLogger(name)
        self._stdout_handler: logging.StreamHandler | None = None
        self._stderr_handler: logging.StreamHandler | None = None
        self._debug: bool = False

    def _get_standard_level(self) -> int:
        if self._debug:
            return logging.DEBUG

        return logging.INFO

    def enable_stdout(self) -> None:
        if self._stdout_handler:
            self._stdout_handler.setLevel(self._get_standard_level())
            return

        self._stdout_handler = logging.StreamHandler(sys.stdout)
        self._stdout_handler.setFormatter(_formatter)
        self._stdout_handler.setLevel(self._get_standard_level())
        self._stdout_handler.addFilter(lambda record: record.levelno < logging.WARNING)
        self._logger.addHandler(self._stdout_handler)

    def enable_stderr(self) -> None:
        if self._stderr_handler:
            self._stderr_handler.setLevel(logging.WARNING)
            return

        self._stderr_handler = logging.StreamHandler(sys.stderr)
        self._stderr_handler.setFormatter(_formatter)
        self._stderr_handler.setLevel(logging.WARNING)
        self._logger.addHandler(self._stderr_handler)

    def enable_debug(self) -> None:
        self._debug = True

        self._logger.setLevel(self._get_standard_level())

        if self._stdout_handler:
            self._stdout_handler.setLevel(self._get_standard_level())

    def disable_debug(self) -> None:
        self._debug = False
        self._logger.setLevel(self._get_standard_level())

        if self._stdout_handler:
            self._stdout_handler.setLevel(self._get_standard_level())

    def add_file(self, file_path: Path, level: int = logging.DEBUG) -> None:
        file_handler = logging.FileHandler(str(file_path), mode="w", encoding="utf-8")
        file_handler.setFormatter(_formatter)
        file_handler.setLevel(level)
        self._logger.addHandler(file_handler)

    def info(self, message: str) -> None:
        self._logger.info(message)

    def debug(self, message: str) -> None:
        if self._debug:
            self._logger.debug(message)

    def warning(self, message: str) -> None:
        self._logger.warning(message)

    def error(self, message: str) -> None:
        self._logger.error(message)


logger = Logger("sharpyc")
logger.enable_stdout()
logger.enable_stderr()


def get_logger(name: str) -> Logger:
    """
    Get or create a logger instance with the specified name.

    :param name: The name of the logger.
    :return: A Logger instance.
    """
    l = Logger(f"sharpyc.{name}")
    l.enable_stdout()
    l.enable_stderr()

    return l
