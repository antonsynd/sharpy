"""
Sharpy compiler interface for compiling and running generated code.
"""

import asyncio
import subprocess
import tempfile
import os
from dataclasses import dataclass
from pathlib import Path
from typing import Optional
import sys


@dataclass
class CompilationResult:
    """Result of compiling Sharpy code."""

    success: bool
    output: str
    error: Optional[str] = None
    generated_cs: Optional[str] = None
    duration_seconds: float = 0.0


@dataclass
class ExecutionResult:
    """Result of running compiled Sharpy code."""

    success: bool
    output: str
    error: Optional[str] = None
    exit_code: int = 0
    duration_seconds: float = 0.0
    timed_out: bool = False


def _strip_compilation_header(output: str) -> str:
    """Strip the compilation success message and '=== Running Program ===' header.

    The Sharpy CLI outputs:
        Successfully compiled to: /path/to/file.exe

        === Running Program ===

        <actual program output>

    This function extracts only the actual program output.
    """
    # Look for the "=== Running Program ===" marker
    marker = "=== Running Program ==="
    marker_idx = output.find(marker)
    if marker_idx != -1:
        # Skip the marker and any following newlines
        program_output = output[marker_idx + len(marker) :]
        # Strip leading whitespace/newlines but preserve trailing newline structure
        return program_output.lstrip("\n")
    # If no marker found, return original output (might be an error case)
    return output


class SharpyCompiler:
    """Interface to the Sharpy compiler."""

    def __init__(self, project_root: Path, cli_project: Path):
        self.project_root = project_root
        self.cli_project = cli_project
        self._dotnet_path = "dotnet"

    async def compile_file(
        self,
        source_path: Path,
        output_path: Optional[Path] = None,
        timeout: float = 60.0,
    ) -> CompilationResult:
        """Compile a Sharpy source file."""
        import time

        start_time = time.time()

        if output_path is None:
            output_path = source_path.with_suffix(".dll")

        cmd = [
            self._dotnet_path,
            "run",
            "--project",
            str(self.cli_project),
            "--",
            "build",
            str(source_path),
            "-o",
            str(output_path),
        ]

        try:
            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=self.project_root,
            )

            try:
                stdout, stderr = await asyncio.wait_for(
                    process.communicate(), timeout=timeout
                )
            except asyncio.TimeoutError:
                try:
                    process.kill()
                    await process.wait()
                except Exception:
                    pass
                return CompilationResult(
                    success=False,
                    output="",
                    error=f"Compilation timed out after {timeout}s",
                    duration_seconds=time.time() - start_time,
                )

            duration = time.time() - start_time
            stdout_text = stdout.decode()
            stderr_text = stderr.decode()

            if process.returncode == 0:
                return CompilationResult(
                    success=True,
                    output=stdout_text,
                    duration_seconds=duration,
                )
            else:
                return CompilationResult(
                    success=False,
                    output=stdout_text,
                    error=stderr_text or stdout_text,
                    duration_seconds=duration,
                )

        except Exception as e:
            return CompilationResult(
                success=False,
                output="",
                error=f"Compilation failed: {e}",
                duration_seconds=time.time() - start_time,
            )

    async def run_file(
        self,
        source_path: Path,
        timeout: float = 30.0,
    ) -> ExecutionResult:
        """Compile and run a Sharpy source file using 'sharpyc run'."""
        import time

        start_time = time.time()

        cmd = [
            self._dotnet_path,
            "run",
            "--project",
            str(self.cli_project),
            "--",
            "run",
            str(source_path),
        ]

        try:
            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=self.project_root,
            )

            try:
                stdout, stderr = await asyncio.wait_for(
                    process.communicate(), timeout=timeout
                )
            except asyncio.TimeoutError:
                try:
                    process.kill()
                    await process.wait()
                except Exception:
                    pass
                return ExecutionResult(
                    success=False,
                    output="",
                    error=f"Execution timed out after {timeout}s",
                    duration_seconds=time.time() - start_time,
                    timed_out=True,
                )

            duration = time.time() - start_time
            stdout_text = stdout.decode()
            stderr_text = stderr.decode()

            if process.returncode == 0:
                return ExecutionResult(
                    success=True,
                    output=_strip_compilation_header(stdout_text),
                    exit_code=process.returncode,
                    duration_seconds=duration,
                )
            else:
                return ExecutionResult(
                    success=False,
                    output=_strip_compilation_header(stdout_text),
                    error=stderr_text or stdout_text,
                    exit_code=process.returncode,
                    duration_seconds=duration,
                )

        except Exception as e:
            return ExecutionResult(
                success=False,
                output="",
                error=f"Execution failed: {e}",
                duration_seconds=time.time() - start_time,
            )

    async def run_project(
        self,
        project_dir: Path,
        entry_point: str = "main.spy",
        timeout: float = 30.0,
    ) -> ExecutionResult:
        """Compile and run a multi-file Sharpy project using 'sharpyc run'.

        Args:
            project_dir: Directory containing the .spy source files.
            entry_point: The entry point file (e.g., "main.spy").
            timeout: Execution timeout in seconds.

        Returns:
            ExecutionResult with success/failure and output.
        """
        import time

        start_time = time.time()
        entry_point_path = project_dir / entry_point

        if not entry_point_path.exists():
            return ExecutionResult(
                success=False,
                output="",
                error=f"Entry point file not found: {entry_point_path}",
                duration_seconds=time.time() - start_time,
            )

        cmd = [
            self._dotnet_path,
            "run",
            "--project",
            str(self.cli_project),
            "--",
            "run",
            str(entry_point_path),
        ]

        try:
            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=project_dir,  # Run from project dir so imports resolve
            )

            try:
                stdout, stderr = await asyncio.wait_for(
                    process.communicate(), timeout=timeout
                )
            except asyncio.TimeoutError:
                try:
                    process.kill()
                    await process.wait()
                except Exception:
                    pass
                return ExecutionResult(
                    success=False,
                    output="",
                    error=f"Execution timed out after {timeout}s",
                    duration_seconds=time.time() - start_time,
                    timed_out=True,
                )

            duration = time.time() - start_time
            stdout_text = stdout.decode()
            stderr_text = stderr.decode()

            if process.returncode == 0:
                return ExecutionResult(
                    success=True,
                    output=_strip_compilation_header(stdout_text),
                    exit_code=process.returncode,
                    duration_seconds=duration,
                )
            else:
                return ExecutionResult(
                    success=False,
                    output=_strip_compilation_header(stdout_text),
                    error=stderr_text or stdout_text,
                    exit_code=process.returncode,
                    duration_seconds=duration,
                )

        except Exception as e:
            return ExecutionResult(
                success=False,
                output="",
                error=f"Execution failed: {e}",
                duration_seconds=time.time() - start_time,
            )

    async def emit_cs(
        self,
        source_path: Path,
        timeout: float = 60.0,
    ) -> CompilationResult:
        """Emit generated C# code for a Sharpy source file."""
        import time

        start_time = time.time()

        cmd = [
            self._dotnet_path,
            "run",
            "--project",
            str(self.cli_project),
            "--",
            "emit",
            "--cs",
            str(source_path),
        ]

        try:
            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=self.project_root,
            )

            try:
                stdout, stderr = await asyncio.wait_for(
                    process.communicate(), timeout=timeout
                )
            except asyncio.TimeoutError:
                try:
                    process.kill()
                    await process.wait()
                except Exception:
                    pass
                return CompilationResult(
                    success=False,
                    output="",
                    error=f"Emit timed out after {timeout}s",
                    duration_seconds=time.time() - start_time,
                )

            duration = time.time() - start_time
            stdout_text = stdout.decode()
            stderr_text = stderr.decode()

            if process.returncode == 0:
                return CompilationResult(
                    success=True,
                    output=stdout_text,
                    generated_cs=stdout_text,
                    duration_seconds=duration,
                )
            else:
                return CompilationResult(
                    success=False,
                    output=stdout_text,
                    error=stderr_text or stdout_text,
                    duration_seconds=duration,
                )

        except Exception as e:
            return CompilationResult(
                success=False,
                output="",
                error=f"Emit failed: {e}",
                duration_seconds=time.time() - start_time,
            )

    async def parse_file(
        self,
        source_path: Path,
        timeout: float = 10.0,
    ) -> CompilationResult:
        """Validate that a Sharpy source file lexes and parses successfully.

        Uses 'emit parse' which runs only lexer + parser (no semantic analysis).
        """
        import time

        start_time = time.time()

        cmd = [
            self._dotnet_path,
            "run",
            "--project",
            str(self.cli_project),
            "--",
            "emit",
            "parse",
            str(source_path),
        ]

        try:
            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=self.project_root,
            )

            try:
                stdout, stderr = await asyncio.wait_for(
                    process.communicate(), timeout=timeout
                )
            except asyncio.TimeoutError:
                try:
                    process.kill()
                    await process.wait()
                except Exception:
                    pass
                return CompilationResult(
                    success=False,
                    output="",
                    error=f"Parse validation timed out after {timeout}s",
                    duration_seconds=time.time() - start_time,
                )

            duration = time.time() - start_time
            stdout_text = stdout.decode()
            stderr_text = stderr.decode()

            if process.returncode == 0:
                return CompilationResult(
                    success=True,
                    output=stdout_text.strip(),
                    duration_seconds=duration,
                )
            else:
                return CompilationResult(
                    success=False,
                    output=stdout_text,
                    error=stderr_text or stdout_text,
                    duration_seconds=duration,
                )

        except Exception as e:
            return CompilationResult(
                success=False,
                output="",
                error=f"Parse validation failed: {e}",
                duration_seconds=time.time() - start_time,
            )

    async def emit_tokens(
        self,
        source_path: Path,
        timeout: float = 60.0,
    ) -> CompilationResult:
        """Emit token listing for a Sharpy source file."""
        import time

        start_time = time.time()

        cmd = [
            self._dotnet_path,
            "run",
            "--project",
            str(self.cli_project),
            "--",
            "emit",
            "tokens",
            str(source_path),
        ]

        try:
            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                cwd=self.project_root,
            )

            try:
                stdout, stderr = await asyncio.wait_for(
                    process.communicate(), timeout=timeout
                )
            except asyncio.TimeoutError:
                try:
                    process.kill()
                    await process.wait()
                except Exception:
                    pass
                return CompilationResult(
                    success=False,
                    output="",
                    error=f"Emit tokens timed out after {timeout}s",
                    duration_seconds=time.time() - start_time,
                )

            duration = time.time() - start_time
            stdout_text = stdout.decode()
            stderr_text = stderr.decode()

            if process.returncode == 0:
                return CompilationResult(
                    success=True,
                    output=stdout_text,
                    duration_seconds=duration,
                )
            else:
                return CompilationResult(
                    success=False,
                    output=stdout_text,
                    error=stderr_text or stdout_text,
                    duration_seconds=duration,
                )

        except Exception as e:
            return CompilationResult(
                success=False,
                output="",
                error=f"Emit tokens failed: {e}",
                duration_seconds=time.time() - start_time,
            )


class TempSourceFile:
    """Context manager for creating temporary Sharpy source files."""

    def __init__(self, code: str, suffix: str = ".spy"):
        self.code = code
        self.suffix = suffix
        self.path: Optional[Path] = None
        self._temp_dir: Optional[tempfile.TemporaryDirectory] = None

    def __enter__(self) -> Path:
        self._temp_dir = tempfile.TemporaryDirectory()
        self.path = Path(self._temp_dir.name) / f"dogfood_test{self.suffix}"
        self.path.write_text(self.code)
        return self.path

    def __exit__(self, exc_type, exc_val, exc_tb):
        if self._temp_dir:
            self._temp_dir.cleanup()


class TempProjectDir:
    """Context manager for creating a temporary multi-file Sharpy project."""

    def __init__(self, files: dict[str, str]):
        """Initialize with a dictionary mapping filenames to code content.

        Args:
            files: Dict mapping filename (e.g., "main.spy") to code content.
        """
        self.files = files
        self.project_dir: Optional[Path] = None
        self._temp_dir: Optional[tempfile.TemporaryDirectory] = None

    def __enter__(self) -> Path:
        self._temp_dir = tempfile.TemporaryDirectory()
        self.project_dir = Path(self._temp_dir.name)

        # Write all source files
        for filename, code in self.files.items():
            file_path = self.project_dir / filename
            file_path.write_text(code)

        return self.project_dir

    def __exit__(self, exc_type, exc_val, exc_tb):
        if self._temp_dir:
            self._temp_dir.cleanup()


async def verify_compiler_available(cli_project: Path) -> bool:
    """Check if the Sharpy compiler is available and can be invoked."""
    try:
        process = await asyncio.create_subprocess_exec(
            "dotnet",
            "build",
            str(cli_project),
            "--no-restore",
            "-v",
            "q",
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,
        )
        await asyncio.wait_for(process.communicate(), timeout=120.0)
        return process.returncode == 0
    except Exception as e:
        print(f"Compiler check failed: {e}", file=sys.stderr)
        return False
