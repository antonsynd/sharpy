import logging
import sys

logger: logging.Logger = logging.getLogger("sharpyc")
formatter: logging.Formatter = logging.Formatter(
    "%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
_stdout_handler = logging.StreamHandler(sys.stdout)
_stdout_handler.setFormatter(formatter)
_stdout_handler.setLevel(logging.INFO)
_stdout_handler.addFilter(lambda record: record.levelno < logging.WARNING)
_stderr_handler = logging.StreamHandler(sys.stderr)
_stderr_handler.setFormatter(formatter)
_stderr_handler.setLevel(logging.WARNING)
logger.addHandler(_stdout_handler)
logger.addHandler(_stderr_handler)
logger.setLevel(logging.INFO)
