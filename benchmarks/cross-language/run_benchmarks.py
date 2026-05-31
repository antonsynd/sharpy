#!/usr/bin/env python3
"""Cross-language benchmark harness for Sharpy vs C# vs Python.

Compiles .spy files with the Sharpy compiler, builds .cs files with dotnet,
runs all three, and produces a comparison table.

Usage:
    python3 benchmarks/cross-language/run_benchmarks.py [benchmark_name...]
    python3 benchmarks/cross-language/run_benchmarks.py --json  # Machine-readable output
"""

import json
import os
import subprocess
import sys
import tempfile
import time
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
BENCH_DIR = Path(__file__).resolve().parent
CLI_PROJECT = REPO_ROOT / "src" / "Sharpy.Cli"


@dataclass
class BenchResult:
    name: str
    language: str
    elapsed_seconds: float
    success: bool
    error: str = ""


def find_benchmarks(names: list[str] | None = None) -> list[Path]:
    """Find benchmark directories (those containing bench.spy)."""
    dirs = sorted(
        d for d in BENCH_DIR.iterdir()
        if d.is_dir() and (d / "bench.spy").exists()
    )
    if names:
        dirs = [d for d in dirs if d.name in names]
    return dirs


def time_command(cmd: list[str], cwd: Path | None = None, timeout: int = 300) -> tuple[float, bool, str]:
    """Run a command and return (elapsed_seconds, success, error_output)."""
    start = time.perf_counter()
    try:
        result = subprocess.run(
            cmd, cwd=cwd, capture_output=True, text=True, timeout=timeout
        )
        elapsed = time.perf_counter() - start
        if result.returncode != 0:
            return elapsed, False, result.stderr[:500]
        return elapsed, True, ""
    except subprocess.TimeoutExpired:
        return timeout, False, "TIMEOUT"
    except Exception as e:
        return 0.0, False, str(e)


def run_python(bench_dir: Path) -> BenchResult:
    """Run the Python benchmark."""
    elapsed, success, error = time_command(
        [sys.executable, str(bench_dir / "bench.py")]
    )
    return BenchResult(bench_dir.name, "Python", elapsed, success, error)


def run_sharpy(bench_dir: Path) -> BenchResult:
    """Compile and run the Sharpy benchmark."""
    spy_file = bench_dir / "bench.spy"
    elapsed, success, error = time_command(
        ["dotnet", "run", "--project", str(CLI_PROJECT), "-c", "Release", "--", "run", str(spy_file)]
    )
    return BenchResult(bench_dir.name, "Sharpy", elapsed, success, error)


def run_csharp(bench_dir: Path) -> BenchResult:
    """Compile and run the raw C# benchmark."""
    cs_file = bench_dir / "bench.cs"
    with tempfile.TemporaryDirectory() as tmpdir:
        # Create a minimal project
        proj_content = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Optimize>true</Optimize>
  </PropertyGroup>
</Project>"""
        proj_path = Path(tmpdir) / "bench.csproj"
        proj_path.write_text(proj_content)

        # Copy the source
        (Path(tmpdir) / "bench.cs").write_text(cs_file.read_text())

        # Build
        build_elapsed, build_ok, build_err = time_command(
            ["dotnet", "build", "-c", "Release", "--nologo", "-v", "q"],
            cwd=Path(tmpdir)
        )
        if not build_ok:
            return BenchResult(bench_dir.name, "C#", 0, False, f"Build failed: {build_err}")

        # Find the built executable
        exe_dir = Path(tmpdir) / "bin" / "Release" / "net10.0"
        exe = exe_dir / "bench"
        if not exe.exists():
            # Try .dll fallback
            dll = exe_dir / "bench.dll"
            if dll.exists():
                elapsed, success, error = time_command(["dotnet", str(dll)])
            else:
                return BenchResult(bench_dir.name, "C#", 0, False, "No executable found")
        else:
            elapsed, success, error = time_command([str(exe)])

        return BenchResult(bench_dir.name, "C#", elapsed, success, error)


def format_time(seconds: float) -> str:
    if seconds < 0.001:
        return f"{seconds * 1_000_000:.0f}us"
    if seconds < 1.0:
        return f"{seconds * 1000:.1f}ms"
    return f"{seconds:.2f}s"


def print_table(results: list[BenchResult]):
    """Print a formatted comparison table."""
    # Group by benchmark name
    benchmarks: dict[str, dict[str, BenchResult]] = {}
    for r in results:
        benchmarks.setdefault(r.name, {})[r.language] = r

    # Header
    print()
    print(f"{'Benchmark':<22} {'Python':<12} {'Sharpy':<12} {'C#':<12} {'Spy/Py':<8} {'Spy/C#':<8}")
    print("-" * 74)

    for name, langs in sorted(benchmarks.items()):
        py = langs.get("Python")
        spy = langs.get("Sharpy")
        cs = langs.get("C#")

        py_str = format_time(py.elapsed_seconds) if py and py.success else "FAIL"
        spy_str = format_time(spy.elapsed_seconds) if spy and spy.success else "FAIL"
        cs_str = format_time(cs.elapsed_seconds) if cs and cs.success else "FAIL"

        # Compute ratios
        ratio_spy_py = ""
        if py and spy and py.success and spy.success and py.elapsed_seconds > 0:
            r = spy.elapsed_seconds / py.elapsed_seconds
            ratio_spy_py = f"{r:.2f}x"

        ratio_spy_cs = ""
        if cs and spy and cs.success and spy.success and cs.elapsed_seconds > 0:
            r = spy.elapsed_seconds / cs.elapsed_seconds
            ratio_spy_cs = f"{r:.2f}x"

        print(f"{name:<22} {py_str:<12} {spy_str:<12} {cs_str:<12} {ratio_spy_py:<8} {ratio_spy_cs:<8}")

    print()
    print("Spy/Py < 1.0 = Sharpy faster than Python")
    print("Spy/C# ~ 1.0 = Sharpy matches raw C# (minimal overhead)")
    print()


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    json_output = "--json" in sys.argv

    bench_dirs = find_benchmarks(args if args else None)
    if not bench_dirs:
        print("No benchmarks found.", file=sys.stderr)
        sys.exit(1)

    if not json_output:
        print(f"Running {len(bench_dirs)} benchmarks: {', '.join(d.name for d in bench_dirs)}")
        print()

    results: list[BenchResult] = []
    for bench_dir in bench_dirs:
        if not json_output:
            print(f"  {bench_dir.name}...", end=" ", flush=True)

        py_result = run_python(bench_dir)
        results.append(py_result)

        spy_result = run_sharpy(bench_dir)
        results.append(spy_result)

        cs_result = run_csharp(bench_dir)
        results.append(cs_result)

        if not json_output:
            statuses = []
            for r in [py_result, spy_result, cs_result]:
                statuses.append(f"{r.language}={'ok' if r.success else 'FAIL'}")
            print(" | ".join(statuses))

    if json_output:
        output = [
            {
                "name": r.name,
                "language": r.language,
                "elapsed_seconds": r.elapsed_seconds,
                "success": r.success,
                "error": r.error,
            }
            for r in results
        ]
        print(json.dumps(output, indent=2))
    else:
        print_table(results)


if __name__ == "__main__":
    main()
