"""Unit tests for the cross-language benchmark harness metrics parsing.

Run with: pytest benchmarks/cross-language/test_run_benchmarks.py
"""
import importlib.util
import json
from pathlib import Path

_MODULE_PATH = Path(__file__).parent / "run_benchmarks.py"
_spec = importlib.util.spec_from_file_location("run_benchmarks", _MODULE_PATH)
assert _spec is not None and _spec.loader is not None
run_benchmarks = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(run_benchmarks)


def _write(tmp_path: Path, data) -> Path:
    metrics_file = tmp_path / "metrics.json"
    metrics_file.write_text(json.dumps(data))
    return metrics_file


def test_parse_combined_frontend_and_assembly_phases(tmp_path):
    metrics = {
        "frontend": {
            "phases": [
                {"phase": "Module Discovery", "duration_ms": 60.0},
                {"phase": "Lexical Analysis", "duration_ms": 5.0},
                {"phase": "Code Generation", "duration_ms": 88.0},
            ]
        },
        "assembly": {
            "phases": [
                {"phase": "IL Emission", "duration_ms": 420.0},
            ]
        },
    }
    phases = run_benchmarks.parse_sharpy_compile_phases(_write(tmp_path, metrics))

    # Durations are converted from milliseconds to seconds, both sections merged.
    assert phases["Module Discovery"] == 0.06
    assert phases["Lexical Analysis"] == 0.005
    assert phases["Code Generation"] == 0.088
    assert phases["IL Emission"] == 0.42
    assert set(phases) == {
        "Module Discovery", "Lexical Analysis", "Code Generation", "IL Emission"
    }


def test_parse_missing_file_returns_empty(tmp_path):
    assert run_benchmarks.parse_sharpy_compile_phases(tmp_path / "nope.json") == {}


def test_parse_malformed_json_returns_empty(tmp_path):
    metrics_file = tmp_path / "metrics.json"
    metrics_file.write_text("{ not valid json")
    assert run_benchmarks.parse_sharpy_compile_phases(metrics_file) == {}


def test_parse_ignores_entries_missing_keys(tmp_path):
    metrics = {
        "frontend": {
            "phases": [
                {"phase": "Lexical Analysis", "duration_ms": 5.0},
                {"phase": "No Duration"},
                {"duration_ms": 3.0},
            ]
        }
    }
    phases = run_benchmarks.parse_sharpy_compile_phases(_write(tmp_path, metrics))
    assert phases == {"Lexical Analysis": 0.005}


def test_results_to_json_includes_compile_phases_only_for_sharpy():
    sharpy = run_benchmarks.BenchResult(
        "fib", "Sharpy", 0.5, 0.1, 0.6, True, "",
        compile_phases={"Lexical Analysis": 0.005},
    )
    python = run_benchmarks.BenchResult("fib", "Python", 0.1, 0.2, 0.3, True, "")
    out = run_benchmarks.results_to_json({"fib": {"Sharpy": sharpy, "Python": python}})

    by_lang = {e["language"]: e for e in out}
    assert by_lang["Sharpy"]["compile_phases"] == {"Lexical Analysis": 0.005}
    assert "compile_phases" not in by_lang["Python"]
