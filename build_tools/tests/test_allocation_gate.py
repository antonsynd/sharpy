"""Tests for the compiler allocation-regression gate (build_tools/allocation_gate.py)."""

import json
from pathlib import Path

import pytest

from build_tools.allocation_gate import (
    DEFAULT_THRESHOLD,
    build_baseline_document,
    describe_coverage,
    find_regressions,
    load_baseline,
    main,
    parse_bdn_results,
)


def _write_bdn_report(results_dir: Path, allocations: dict[str, int]) -> None:
    """Write a minimal BenchmarkDotNet ``*-report-full.json`` under results_dir/results/."""
    results = results_dir / "results"
    results.mkdir(parents=True, exist_ok=True)
    document = {
        "Benchmarks": [
            {
                "FullName": name,
                "Method": name.split(".")[-1],
                "MethodTitle": f"{name.split('.')[-1]} desc",
                "Memory": {
                    "Gen0Collections": 0,
                    "BytesAllocatedPerOperation": allocated,
                },
            }
            for name, allocated in allocations.items()
        ]
    }
    (results / "Sharpy.Compiler.Benchmarks.CompilerBenchmarks-report-full.json").write_text(
        json.dumps(document), encoding="utf-8"
    )


def _write_baseline(path: Path, allocations: dict[str, int], threshold: float = DEFAULT_THRESHOLD) -> None:
    path.write_text(
        json.dumps(
            {
                "threshold": threshold,
                "benchmarks": {
                    name: {"allocated_bytes": value} for name, value in allocations.items()
                },
            }
        ),
        encoding="utf-8",
    )


class TestParseBdnResults:
    def test_extracts_allocated_bytes(self, tmp_path):
        _write_bdn_report(tmp_path, {"A.b.C": 1000, "A.b.D": 2000})
        result = parse_bdn_results(str(tmp_path))
        assert result == {"A.b.C": 1000, "A.b.D": 2000}

    def test_skips_benchmarks_without_memory_section(self, tmp_path):
        results = tmp_path / "results"
        results.mkdir(parents=True)
        (results / "x-report-full.json").write_text(
            json.dumps({"Benchmarks": [{"FullName": "A.b.C"}]}), encoding="utf-8"
        )
        assert parse_bdn_results(str(tmp_path)) == {}

    def test_raises_when_no_reports_found(self, tmp_path):
        with pytest.raises(FileNotFoundError):
            parse_bdn_results(str(tmp_path))


class TestFindRegressions:
    def test_no_regression_when_equal(self):
        base = {"A": 1000}
        assert find_regressions(base, {"A": 1000}) == []

    def test_no_regression_when_below_threshold(self):
        # +9% is under the default 10% threshold.
        assert find_regressions({"A": 1000}, {"A": 1090}) == []

    def test_exactly_at_threshold_is_tolerated(self):
        # +10.0% is within tolerance; only strictly greater fails.
        assert find_regressions({"A": 1000}, {"A": 1100}) == []

    def test_just_over_threshold_regresses(self):
        regs = find_regressions({"A": 1000}, {"A": 1101})
        assert len(regs) == 1
        assert regs[0].name == "A"
        assert regs[0].current_bytes == 1101
        assert regs[0].baseline_bytes == 1000

    def test_improvement_is_not_a_regression(self):
        assert find_regressions({"A": 1000}, {"A": 500}) == []

    def test_zero_baseline_with_allocation_regresses(self):
        regs = find_regressions({"A": 0}, {"A": 8})
        assert len(regs) == 1
        assert regs[0].ratio == float("inf")

    def test_only_checks_benchmarks_in_both(self):
        # 'B' is missing from current; not checked here (coverage handles it).
        assert find_regressions({"A": 100, "B": 100}, {"A": 100}) == []

    def test_custom_threshold(self):
        # Under a 50% threshold, +40% passes; +60% fails.
        assert find_regressions({"A": 1000}, {"A": 1400}, threshold=0.50) == []
        assert len(find_regressions({"A": 1000}, {"A": 1600}, threshold=0.50)) == 1


class TestDescribeCoverage:
    def test_reports_missing_and_new(self):
        missing, new = describe_coverage({"A": 1, "B": 2}, {"A": 1, "C": 3})
        assert missing == ["B"]
        assert new == ["C"]


class TestBaselineRoundTrip:
    def test_build_and_load(self, tmp_path):
        doc = build_baseline_document({"A.b.C": 1234}, DEFAULT_THRESHOLD, {"A.b.C": "hello"})
        assert doc["threshold"] == DEFAULT_THRESHOLD
        assert doc["benchmarks"]["A.b.C"]["allocated_bytes"] == 1234
        assert doc["benchmarks"]["A.b.C"]["description"] == "hello"

        path = tmp_path / "baseline.json"
        path.write_text(json.dumps(doc), encoding="utf-8")
        assert load_baseline(str(path)) == {"A.b.C": 1234}


class TestMainCommand:
    def test_check_passes_when_within_threshold(self, tmp_path, capsys):
        baseline = tmp_path / "baseline.json"
        _write_baseline(baseline, {"A.b.C": 1000})
        _write_bdn_report(tmp_path, {"A.b.C": 1050})  # +5%

        exit_code = main(["check", "--baseline", str(baseline), "--results", str(tmp_path)])
        assert exit_code == 0
        assert "within" in capsys.readouterr().out

    def test_check_fails_on_regression(self, tmp_path, capsys):
        # Deliberate regression simulation: the gate must fail (non-zero exit).
        baseline = tmp_path / "baseline.json"
        _write_baseline(baseline, {"A.b.C": 1000})
        _write_bdn_report(tmp_path, {"A.b.C": 1500})  # +50%

        exit_code = main(["check", "--baseline", str(baseline), "--results", str(tmp_path)])
        assert exit_code == 1
        out = capsys.readouterr().out
        assert "ALLOCATION REGRESSION" in out
        assert "A.b.C" in out

    def test_check_does_not_fail_on_new_benchmark(self, tmp_path, capsys):
        baseline = tmp_path / "baseline.json"
        _write_baseline(baseline, {"A.b.C": 1000})
        _write_bdn_report(tmp_path, {"A.b.C": 1000, "A.b.NEW": 999999})

        exit_code = main(["check", "--baseline", str(baseline), "--results", str(tmp_path)])
        assert exit_code == 0
        assert "new benchmark" in capsys.readouterr().out

    def test_update_writes_baseline_from_results(self, tmp_path):
        baseline = tmp_path / "baseline.json"
        _write_bdn_report(tmp_path, {"A.b.C": 4242})

        exit_code = main(["update", "--baseline", str(baseline), "--results", str(tmp_path)])
        assert exit_code == 0
        assert load_baseline(str(baseline)) == {"A.b.C": 4242}
