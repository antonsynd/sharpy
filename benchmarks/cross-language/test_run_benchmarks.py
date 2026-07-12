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


def test_generate_spyproj_writes_entry_point_and_project(tmp_path):
    spy = tmp_path / "bench.spy"
    spy.write_text("def main():\n    print(1)\n")
    project_dir = tmp_path / "proj"

    project_file = run_benchmarks.generate_spyproj(spy, project_dir)

    # The .spy is copied in as main.spy (the default Exe entry point).
    assert (project_dir / "main.spy").read_text() == "def main():\n    print(1)\n"
    contents = project_file.read_text()
    assert project_file.name == "bench.spyproj"
    assert "<OutputType>exe</OutputType>" in contents
    assert '<SourceFile Include="main.spy" />' in contents
    # No features requested -> no <Features> element.
    assert "<Features>" not in contents


def test_generate_spyproj_emits_features_when_requested(tmp_path):
    spy = tmp_path / "bench.spy"
    spy.write_text("def main():\n    pass\n")
    project_file = run_benchmarks.generate_spyproj(
        spy, tmp_path / "proj", features=["matmul", "defer"]
    )
    assert "<Features>matmul;defer</Features>" in project_file.read_text()


def test_results_to_json_includes_cold_warm_only_for_sharpy():
    sharpy = run_benchmarks.BenchResult(
        "fib", "Sharpy", 0.5, 0.1, 0.6, True, "",
        cold_compile_seconds=0.8, warm_compile_seconds=0.3,
    )
    python = run_benchmarks.BenchResult("fib", "Python", 0.1, 0.2, 0.3, True, "")
    out = run_benchmarks.results_to_json({"fib": {"Sharpy": sharpy, "Python": python}})

    by_lang = {e["language"]: e for e in out}
    assert by_lang["Sharpy"]["cold_compile_seconds"] == 0.8
    assert by_lang["Sharpy"]["warm_compile_seconds"] == 0.3
    assert "cold_compile_seconds" not in by_lang["Python"]
    assert "warm_compile_seconds" not in by_lang["Python"]


def test_read_features_parses_names_and_comments(tmp_path):
    (tmp_path / "bench.features").write_text(
        "# leading comment\nmatmul\n\ndefer  # trailing comment\n"
    )
    assert run_benchmarks.read_features(tmp_path) == ["matmul", "defer"]


def test_read_features_absent_returns_empty(tmp_path):
    assert run_benchmarks.read_features(tmp_path) == []


def test_read_languages_preserves_csharp_hash(tmp_path):
    # The "#" comment convention must not truncate the "C#" language token.
    (tmp_path / "bench.languages").write_text(
        "# only the numeric-path languages\nPython\nC#\n"
    )
    assert run_benchmarks.read_languages(tmp_path) == {"Python", "C#"}


def test_read_languages_absent_returns_none(tmp_path):
    # Absent sidecar means "all languages" (None), not "no languages".
    assert run_benchmarks.read_languages(tmp_path) is None


def test_read_languages_ignores_unknown_names(tmp_path):
    (tmp_path / "bench.languages").write_text("Python\nRust\n")
    assert run_benchmarks.read_languages(tmp_path) == {"Python"}


def test_compile_sharpy_threads_enable_feature(monkeypatch, tmp_path):
    # compile_sharpy must pass each feature as --enable-feature <name>.
    captured = {}

    class _Result:
        returncode = 0
        stderr = ""
        stdout = ""

    def fake_run(cmd, *args, **kwargs):
        captured["cmd"] = cmd
        return _Result()

    monkeypatch.setattr(run_benchmarks.subprocess, "run", fake_run)
    run_benchmarks.compile_sharpy(
        Path("cli.dll"), tmp_path / "bench.spy", tmp_path, features=["matmul"]
    )
    cmd = captured["cmd"]
    assert "--enable-feature" in cmd
    assert cmd[cmd.index("--enable-feature") + 1] == "matmul"


def test_merge_results_round_trips_cold_warm(tmp_path):
    sharpy = run_benchmarks.BenchResult(
        "fib", "Sharpy", 0.5, 0.1, 0.6, True, "",
        cold_compile_seconds=0.8, warm_compile_seconds=0.3,
    )
    serialized = run_benchmarks.results_to_json({"fib": {"Sharpy": sharpy}})
    merge_file = tmp_path / "partial.json"
    merge_file.write_text(json.dumps(serialized))

    merged = run_benchmarks.merge_results({}, merge_file)
    r = merged["fib"]["Sharpy"]
    assert r.cold_compile_seconds == 0.8
    assert r.warm_compile_seconds == 0.3
