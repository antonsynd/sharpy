import io
import logging
import shlex
import subprocess
from pathlib import Path
from typing import Any, Callable, MutableSequence, Optional, Sequence, Tuple


class Formatter:
    def __init__(self, logger: logging.Logger):
        self._logger: logging.Logger = logger

    def logger(self) -> logging.Logger:
        return self._logger

    def format_file(self, input_path: Path, output_path: Path) -> None:
        raise NotImplementedError()

    def format_buffer(self, input_buffer: io.TextIOBase, output_buffer: io.TextIOBase) -> None:
        raise NotImplementedError()

    def format_streamable(self, output_buffer: io.TextIOBase) -> io.TextIOBase:
        raise NotImplementedError()


class CSharpierFormatStream(io.TextIOBase):
    def __init__(
        self,
        logger: logging.Logger,
        proc: subprocess.Popen,
        callback: Callable[[], None],
    ):
        super().__init__()

        self._logger: logging.Logger = logger
        self._proc: subprocess.Popen = proc
        self._callback: Callable[[], None] = callback

    def write(self, s: Any) -> int:
        return self._proc.stdin.write(s)

    def writelines(self, lines: Any) -> None:
        self._proc.stdin.writelines(lines)

    def close(self):
        if self._proc.stdin.closed:
            return

        self._proc.stdin.close()
        self._callback()

        self._proc.wait()

        errors: str = self._proc.stderr.read()

        if errors:
            self._logger.error(errors)

        self.closed = True


class CSharpierFormatter(Formatter):
    MINIMUM_VERSION: Tuple[int, int, int] = (0, 30, 6)

    def __init__(self, logger: logging.Logger, invocation: Optional[str]):
        super().__init__(logger=logger)

        if invocation:
            self._invocation: Sequence[str] = [shlex.quote(x) for x in shlex.split(invocation)]
        else:
            self._invocation: Sequence[str] = ["dotnet-csharpier"]

        if not self._test_csharpier():
            raise ValueError(
                f"Could not find a working csharpier with invocation arguments: {' '.join(self._invocation)}"
            )

    def _test_csharpier(self) -> bool:
        args: MutableSequence[str] = self._invocation + ["--version"]

        try:
            output: str = subprocess.check_output(args=args, text=True)
            version = tuple(int(x) for x in output.split("."))

            return version >= self.MINIMUM_VERSION
        except subprocess.CalledProcessError as e:
            self.logger().debug(e.stdout)
            self.logger().error(e.stderr)

            return False
        except FileNotFoundError as e:
            self.logger().error(e)

            return False

    def format_file(self, input_path: Path, output_path: Path) -> None:
        args: MutableSequence[str] = self._invocation + [
            "--no-cache",
            "--no-msbuild-check",
            "--fast",
            "--write-stdout",
            str(input_path),
            ">",
            str(output_path),
        ]

        try:
            subprocess.Popen(
                args=args,
                text=True,
                stderr=subprocess.PIPE,
            )
        except subprocess.CalledProcessError as e:
            self.logger().debug(e.stdout)
            self.logger().error(e.stderr)

            raise e

    def format_buffer(self, input_buffer: io.TextIOBase, output_buffer: io.TextIOBase) -> None:
        args: MutableSequence[str] = self._invocation + [
            "--no-cache",
            "--no-msbuild-check",
            "--fast",
            "--write-stdout",
        ]

        try:
            proc = subprocess.Popen(
                args=args,
                text=True,
                stdin=subprocess.PIPE,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )

            for line in input_buffer:
                proc.stdin.write(line)

            proc.stdin.close()
            proc.wait()

            for line in proc.stdout:
                output_buffer.write(line)

            errors: str = proc.stderr.read()

            if errors:
                self.logger().error(errors)
        except subprocess.CalledProcessError as e:
            self.logger().debug(e.stdout)
            self.logger().error(e.stderr)

            raise e

    def format_streamable(self, output_buffer: io.TextIOBase) -> io.TextIOBase:
        args: MutableSequence[str] = self._invocation + [
            "--no-cache",
            "--no-msbuild-check",
            "--fast",
            "--write-stdout",
        ]

        try:
            proc = subprocess.Popen(
                args=args,
                text=True,
                stdin=subprocess.PIPE,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )

            def callback():
                for line in proc.stdout:
                    output_buffer.write(line)

            return CSharpierFormatStream(logger=self.logger(), proc=proc, callback=callback)
        except subprocess.CalledProcessError as e:
            self.logger().debug(e.stdout)
            self.logger().error(e.stderr)

            raise e


class DotnetFormatFormatter(Formatter):
    def __init__(self):
        super().__init__()
