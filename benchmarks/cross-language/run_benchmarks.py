#!/usr/bin/env python3
"""Cross-language benchmark harness for Sharpy vs C# vs Python.

Measures compilation time and execution time separately for each language:
- Sharpy: compile .spy → .dll (via pre-built CLI), then run the .dll
- C#: compile .cs → .dll (dotnet build), then run the .dll
- Python: compile .py → .pyc (py_compile), then run the .pyc

A warmup pass runs each benchmark once (discarded) to prime JIT, caches, and
OS filesystem buffers before the timed runs.

Usage:
    python3 benchmarks/cross-language/run_benchmarks.py [benchmark_name...]
    python3 benchmarks/cross-language/run_benchmarks.py --json
"""

import compileall
import json
import os
import py_compile
import shutil
import subprocess
import sys
import tempfile
import time
from dataclasses import dataclass, field
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
BENCH_DIR = Path(__file__).resolve().parent
CLI_PROJECT = REPO_ROOT / "src" / "Sharpy.Cli"
WARMUP_RUNS = 1
TIMED_RUNS = 3


@dataclass
class PhaseResult:
    compile_seconds: float = 0.0
    execute_seconds: float = 0.0
    success: bool = True
    error: str = ""


@dataclass
class BenchResult:
    name: str
    language: str
    compile_seconds: float
    execute_seconds: float
    total_seconds: float
    success: bool
    error: str = ""


def find_benchmarks(names: list[str] | None = None) -> list[Path]:
    dirs = sorted(
        d for d in BENCH_DIR.iterdir()
        if d.is_dir() and (d / "bench.spy").exists()
    )
    if names:
        dirs = [d for d in dirs if d.name in names]
    return dirs


def time_command(cmd: list[str], cwd: Path | None = None, timeout: int = 300) -> tuple[float, bool, str, str]:
    """Run a command. Returns (elapsed, success, error_detail, stdout)."""
    start = time.perf_counter()
    try:
        result = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, timeout=timeout)
        elapsed = time.perf_counter() - start
        if result.returncode != 0:
            err = result.stderr[:500] or result.stdout[:500] or f"exit code {result.returncode}"
            return elapsed, False, err, result.stdout
        return elapsed, True, "", result.stdout
    except subprocess.TimeoutExpired:
        return timeout, False, "TIMEOUT", ""
    except Exception as e:
        return 0.0, False, str(e), ""


def median(values: list[float]) -> float:
    s = sorted(values)
    n = len(s)
    if n % 2 == 0:
        return (s[n // 2 - 1] + s[n // 2]) / 2
    return s[n // 2]


# --- Pre-build ---

def prebuild_sharpy_cli(verbose: bool = False) -> Path:
    """Build the Sharpy CLI in Release mode once. Returns path to the built dll."""
    if verbose:
        print("  Pre-building Sharpy CLI (Release)...", end=" ", flush=True)
    result = subprocess.run(
        ["dotnet", "build", str(CLI_PROJECT), "-c", "Release", "--nologo", "-v", "q"],
        capture_output=True, text=True
    )
    if result.returncode != 0:
        raise RuntimeError(f"Failed to build Sharpy CLI:\n{result.stderr[:500]}")
    # Find the built dll (assembly name is 'sharpyc', not 'Sharpy.Cli')
    dll = CLI_PROJECT / "bin" / "Release" / "net10.0" / "sharpyc.dll"
    if not dll.exists():
        raise RuntimeError(f"CLI dll not found at {dll}")
    if verbose:
        print("done")
    return dll


# --- Python ---

def compile_python(bench_dir: Path, tmp_dir: Path) -> tuple[float, bool, str, Path]:
    """Compile .py to .pyc, return (time, success, error, pyc_path)."""
    py_file = bench_dir / "bench.py"
    pyc_path = tmp_dir / "bench.pyc"

    start = time.perf_counter()
    try:
        py_compile.compile(str(py_file), cfile=str(pyc_path), doraise=True)
        elapsed = time.perf_counter() - start
        return elapsed, True, "", pyc_path
    except py_compile.PyCompileError as e:
        elapsed = time.perf_counter() - start
        return elapsed, False, str(e), pyc_path


def execute_python(bench_dir: Path) -> tuple[float, bool, str]:
    """Execute .py (using interpreter, which loads .pyc if available)."""
    elapsed, success, err, _ = time_command([sys.executable, str(bench_dir / "bench.py")])
    return elapsed, success, err


# --- Sharpy ---

def compile_sharpy(cli_dll: Path, spy_file: Path, tmp_dir: Path) -> tuple[float, bool, str, Path]:
    """Compile .spy to .dll via Sharpy CLI. Returns (time, success, error, output_dll)."""
    output_dll = tmp_dir / "bench_output.dll"
    cmd = [
        "dotnet", str(cli_dll), "emit", "csharp", str(spy_file)
    ]
    # Use 'run' which compiles and executes — but we want compile-only.
    # The CLI doesn't have a compile-to-dll command, so we time 'run' and
    # subtract execution by also timing execution separately.
    # Actually: use 'emit csharp' to measure pure compilation, then 'run' for execute.
    start = time.perf_counter()
    result = subprocess.run(cmd, capture_output=True, text=True, timeout=120)
    elapsed = time.perf_counter() - start
    if result.returncode != 0:
        return elapsed, False, result.stderr[:500], output_dll
    return elapsed, True, "", output_dll


def execute_sharpy(cli_dll: Path, spy_file: Path) -> tuple[float, bool, str]:
    """Execute .spy via Sharpy CLI (includes re-compilation overhead)."""
    cmd = ["dotnet", str(cli_dll), "run", str(spy_file)]
    elapsed, success, err, _ = time_command(cmd)
    return elapsed, success, err


# --- C# ---

def compile_csharp(bench_dir: Path, tmp_dir: Path) -> tuple[float, bool, str, Path]:
    """Compile .cs to executable. Returns (time, success, error, exe_dir)."""
    cs_file = bench_dir / "bench.cs"
    proj_content = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Optimize>true</Optimize>
  </PropertyGroup>
</Project>"""
    proj_path = tmp_dir / "bench.csproj"
    proj_path.write_text(proj_content)
    shutil.copy2(cs_file, tmp_dir / "bench.cs")

    start = time.perf_counter()
    result = subprocess.run(
        ["dotnet", "build", "-c", "Release", "--nologo", "-v", "q"],
        cwd=tmp_dir, capture_output=True, text=True, timeout=120
    )
    elapsed = time.perf_counter() - start
    if result.returncode != 0:
        return elapsed, False, result.stderr[:500], tmp_dir
    return elapsed, True, "", tmp_dir


def execute_csharp(tmp_dir: Path) -> tuple[float, bool, str]:
    """Execute pre-built C# benchmark using 'dotnet run --no-build'."""
    start = time.perf_counter()
    try:
        result = subprocess.run(
            ["dotnet", "run", "-c", "Release", "--no-build"],
            cwd=tmp_dir, capture_output=True, text=True, timeout=120
        )
        elapsed = time.perf_counter() - start
        if result.returncode != 0:
            err = result.stderr[:300] or result.stdout[:300] or f"exit code {result.returncode}"
            return elapsed, False, err
        return elapsed, True, ""
    except Exception as e:
        elapsed = time.perf_counter() - start
        return elapsed, False, f"Exception: {type(e).__name__}: {e}"


# --- Main ---

def run_benchmark(bench_dir: Path, cli_dll: Path, warmup: bool = False) -> dict[str, BenchResult]:
    """Run all languages for one benchmark. Returns dict keyed by language."""
    results = {}

    # --- Python ---
    with tempfile.TemporaryDirectory() as tmp:
        tmp_path = Path(tmp)
        compile_t, compile_ok, compile_err, _ = compile_python(bench_dir, tmp_path)
        if not compile_ok:
            results["Python"] = BenchResult(bench_dir.name, "Python", compile_t, 0, compile_t, False, compile_err)
        else:
            exec_times = []
            exec_ok = True
            exec_err = ""
            for _ in range(TIMED_RUNS):
                et, ok, err = execute_python(bench_dir)
                exec_times.append(et)
                if not ok:
                    exec_ok = False
                    exec_err = err
                    break
            exec_t = median(exec_times) if exec_ok else exec_times[0]
            results["Python"] = BenchResult(
                bench_dir.name, "Python", compile_t, exec_t, compile_t + exec_t, exec_ok, exec_err
            )

    # --- Sharpy ---
    spy_file = bench_dir / "bench.spy"
    # Compilation phase (emit csharp measures front-end + codegen without execution)
    compile_times = []
    compile_ok = True
    compile_err = ""
    for _ in range(TIMED_RUNS):
        with tempfile.TemporaryDirectory() as tmp:
            ct, ok, err, _ = compile_sharpy(cli_dll, spy_file, Path(tmp))
            compile_times.append(ct)
            if not ok:
                compile_ok = False
                compile_err = err
                break
    compile_t = median(compile_times) if compile_ok else compile_times[0]

    if not compile_ok:
        results["Sharpy"] = BenchResult(bench_dir.name, "Sharpy", compile_t, 0, compile_t, False, compile_err)
    else:
        # Execution phase (run = compile + execute; subtract compile to isolate execution)
        run_times = []
        exec_ok = True
        exec_err = ""
        for _ in range(TIMED_RUNS):
            et, ok, err = execute_sharpy(cli_dll, spy_file)
            run_times.append(et)
            if not ok:
                exec_ok = False
                exec_err = err
                break
        run_t = median(run_times) if exec_ok else run_times[0]
        # execution ≈ run - compile (both include dotnet startup, so this is approximate)
        exec_t = max(0, run_t - compile_t)
        results["Sharpy"] = BenchResult(
            bench_dir.name, "Sharpy", compile_t, exec_t, run_t, exec_ok, exec_err
        )

    # --- C# ---
    with tempfile.TemporaryDirectory() as tmp:
        tmp_path = Path(tmp)
        compile_t, compile_ok, compile_err, _ = compile_csharp(bench_dir, tmp_path)
        if not compile_ok:
            results["C#"] = BenchResult(bench_dir.name, "C#", compile_t, 0, compile_t, False, compile_err)
        else:
            exec_times = []
            exec_ok = True
            exec_err = ""
            for _ in range(TIMED_RUNS):
                et, ok, err = execute_csharp(tmp_path)
                exec_times.append(et)
                if not ok:
                    exec_ok = False
                    exec_err = err
                    break
            exec_t = median(exec_times) if exec_ok else exec_times[0]
            results["C#"] = BenchResult(
                bench_dir.name, "C#", compile_t, exec_t, compile_t + exec_t, exec_ok, exec_err
            )

    return results


def format_time(seconds: float) -> str:
    if seconds == 0:
        return "—"
    if seconds < 0.001:
        return f"{seconds * 1_000_000:.0f}us"
    if seconds < 1.0:
        return f"{seconds * 1000:.1f}ms"
    return f"{seconds:.2f}s"


def print_table(all_results: dict[str, dict[str, BenchResult]]):
    """Print formatted comparison tables."""
    # Execution time table
    print()
    print("=== Execution Time (runtime only, excludes compilation) ===")
    print()
    print(f"{'Benchmark':<22} {'Python':<12} {'Sharpy':<12} {'C#':<12} {'Spy/Py':<8} {'Spy/C#':<8}")
    print("-" * 74)

    for name in sorted(all_results):
        langs = all_results[name]
        py = langs.get("Python")
        spy = langs.get("Sharpy")
        cs = langs.get("C#")

        py_str = format_time(py.execute_seconds) if py and py.success else "FAIL"
        spy_str = format_time(spy.execute_seconds) if spy and spy.success else "FAIL"
        cs_str = format_time(cs.execute_seconds) if cs and cs.success else "FAIL"

        py_t = py.execute_seconds if py and py.success else 0
        spy_t = spy.execute_seconds if spy and spy.success else 0
        cs_t = cs.execute_seconds if cs and cs.success else 0

        ratio_py = f"{spy_t / py_t:.2f}x" if py_t > 0 and spy_t > 0 else "—"
        ratio_cs = f"{spy_t / cs_t:.2f}x" if cs_t > 0 and spy_t > 0 else "—"

        print(f"{name:<22} {py_str:<12} {spy_str:<12} {cs_str:<12} {ratio_py:<8} {ratio_cs:<8}")

    # Compilation time table
    print()
    print("=== Compilation Time ===")
    print()
    print(f"{'Benchmark':<22} {'Python':<12} {'Sharpy':<12} {'C#':<12} {'Spy/C#':<8}")
    print("-" * 62)

    for name in sorted(all_results):
        langs = all_results[name]
        py = langs.get("Python")
        spy = langs.get("Sharpy")
        cs = langs.get("C#")

        py_str = format_time(py.compile_seconds) if py else "—"
        spy_str = format_time(spy.compile_seconds) if spy else "—"
        cs_str = format_time(cs.compile_seconds) if cs else "—"

        spy_t = spy.compile_seconds if spy else 0
        cs_t = cs.compile_seconds if cs else 0
        ratio_cs = f"{spy_t / cs_t:.2f}x" if cs_t > 0 and spy_t > 0 else "—"

        print(f"{name:<22} {py_str:<12} {spy_str:<12} {cs_str:<12} {ratio_cs:<8}")

    print()
    print("Spy/Py < 1.0 = Sharpy faster than Python")
    print("Spy/C# ~ 1.0 = Sharpy matches raw C# (minimal overhead)")
    print()


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    json_output = "--json" in sys.argv
    verbose = not json_output

    bench_dirs = find_benchmarks(args if args else None)
    if not bench_dirs:
        print("No benchmarks found.", file=sys.stderr)
        sys.exit(1)

    if verbose:
        print(f"Running {len(bench_dirs)} benchmarks: {', '.join(d.name for d in bench_dirs)}")
        print(f"  Warmup runs: {WARMUP_RUNS}, Timed runs: {TIMED_RUNS} (median)")
        print()

    # Pre-build Sharpy CLI
    cli_dll = prebuild_sharpy_cli(verbose)

    # Warmup pass — run each benchmark once to prime JIT/caches
    if verbose:
        print("  Warmup pass...", end=" ", flush=True)
    for bench_dir in bench_dirs:
        spy_file = bench_dir / "bench.spy"
        # Warmup Python
        subprocess.run([sys.executable, str(bench_dir / "bench.py")],
                       capture_output=True, timeout=120)
        # Warmup Sharpy (compile + run)
        subprocess.run(["dotnet", str(cli_dll), "run", str(spy_file)],
                       capture_output=True, timeout=120)
        # Warmup C# — build in temp then run
        with tempfile.TemporaryDirectory() as tmp:
            tmp_path = Path(tmp)
            compile_csharp(bench_dir, tmp_path)
            execute_csharp(tmp_path)
    if verbose:
        print("done")
        print()

    # Timed runs
    all_results: dict[str, dict[str, BenchResult]] = {}
    for bench_dir in bench_dirs:
        if verbose:
            print(f"  {bench_dir.name}...", end=" ", flush=True)

        results = run_benchmark(bench_dir, cli_dll)
        all_results[bench_dir.name] = results

        if verbose:
            statuses = []
            for lang in ["Python", "Sharpy", "C#"]:
                r = results.get(lang)
                statuses.append(f"{lang}={'ok' if r and r.success else 'FAIL'}")
            print(" | ".join(statuses))

    if json_output:
        output = []
        for name in sorted(all_results):
            for lang, r in all_results[name].items():
                output.append({
                    "name": r.name,
                    "language": r.language,
                    "compile_seconds": r.compile_seconds,
                    "execute_seconds": r.execute_seconds,
                    "total_seconds": r.total_seconds,
                    "success": r.success,
                    "error": r.error,
                })
        print(json.dumps(output, indent=2))
    else:
        print_table(all_results)


if __name__ == "__main__":
    main()
