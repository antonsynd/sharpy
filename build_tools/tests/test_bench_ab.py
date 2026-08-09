"""
Tests for the interleaved A/B benchmark orchestrator (#1318).

The pooling and sign gate are what make the tool trustworthy, so they are what is
tested: worktree preparation and the BenchmarkDotNet invocation are subprocess plumbing
whose failure is loud and immediate. The three cases that matter are the ones where a
naive comparison gives a confident wrong answer:

* the **null control** — identical arms must report nothing;
* the **position-dominated** case — an ordering artifact must be refused, not averaged
  into a number;
* the **real effect** — a difference that survives both positions must be reported.
"""

import json

import pytest

from build_tools.bench_ab import (
    Measurement,
    evaluate,
    parse_bdn_means,
    pool_by_position,
    report,
)


def measurements(**cells: float) -> list[Measurement]:
    """
    Build a measurement set from ``arm_position=mean_ns`` keys, e.g.
    ``a_0=100.0`` meaning arm "a" running first measured 100 ns.
    """
    out = []
    for key, mean in cells.items():
        arm, position = key.rsplit("_", 1)
        out.append(Measurement(arm=arm, position=int(position), benchmark="Bench", mean_ns=mean))
    return out


class TestSignGate:
    def test_identical_arms_on_a_quiet_machine_report_nothing(self):
        """The null control. An instrument that cannot say 'nothing' cannot say 'something'."""
        verdicts = evaluate(measurements(a_0=100.0, a_1=100.0, b_0=100.0, b_1=100.0), "a", "b")

        assert len(verdicts) == 1
        assert not verdicts[0].measured
        assert "floor" in verdicts[0].reason
        assert verdicts[0].pooled_delta_percent == pytest.approx(0.0)

    def test_position_effect_alone_is_refused(self):
        """
        Whichever arm runs second looks ~8% faster, and the arms are otherwise identical —
        the real measured artifact (#1318). Comparing same-position cells would produce a
        confident 8% delta; the sign gate must refuse it, and geometric pooling must put
        the magnitude at exactly zero rather than the +0.35% arithmetic averaging leaves.
        """
        verdicts = evaluate(measurements(a_0=100.0, a_1=92.0, b_0=100.0, b_1=92.0), "a", "b")

        assert not verdicts[0].measured
        assert "position-dominated" in verdicts[0].reason
        assert verdicts[0].pooled_delta_percent == pytest.approx(0.0, abs=1e-9)

    def test_disagreeing_signs_are_reported_as_position_dominated(self):
        """
        B looks faster in one ordering and slower in the other — the signature of an
        artifact, not a code effect. The tool must name it rather than average it away.
        """
        verdicts = evaluate(measurements(a_0=100.0, a_1=100.0, b_0=70.0, b_1=140.0), "a", "b")

        assert not verdicts[0].measured
        assert "position-dominated" in verdicts[0].reason
        assert "disagree on the SIGN" in verdicts[0].reason

    def test_real_slowdown_survives_both_positions(self):
        """B is 50% slower whichever way round it runs, so the delta is real."""
        verdicts = evaluate(measurements(a_0=100.0, a_1=100.0, b_0=150.0, b_1=150.0), "a", "b")

        assert verdicts[0].measured
        assert verdicts[0].pooled_delta_percent == pytest.approx(50.0)
        assert "slower" in verdicts[0].describe("a", "b")

    def test_real_speedup_survives_both_positions(self):
        verdicts = evaluate(measurements(a_0=200.0, a_1=200.0, b_0=100.0, b_1=100.0), "a", "b")

        assert verdicts[0].measured
        assert verdicts[0].pooled_delta_percent == pytest.approx(-50.0)
        assert "faster" in verdicts[0].describe("a", "b")

    def test_agreeing_but_small_delta_is_below_the_floor(self):
        """
        A consistent 5% is inside the range the position artifact alone produces, so it
        is unmeasured rather than a small confirmed win.
        """
        verdicts = evaluate(measurements(a_0=100.0, a_1=100.0, b_0=105.0, b_1=105.0), "a", "b")

        assert not verdicts[0].measured
        assert "floor" in verdicts[0].reason

    def test_missing_cell_is_refused_not_pooled(self):
        """An incomplete interleave must not silently produce a one-sided comparison."""
        verdicts = evaluate(measurements(a_0=100.0, b_1=150.0), "a", "b")

        assert not verdicts[0].measured
        assert "interleaving incomplete" in verdicts[0].reason
        assert "a@position2" in verdicts[0].reason
        assert "b@position1" in verdicts[0].reason


class TestPooling:
    def test_cells_pool_by_median(self):
        """Median, so one outlier round cannot move a cell."""
        pooled = pool_by_position([
            Measurement("a", 0, "Bench", 100.0),
            Measurement("a", 0, "Bench", 101.0),
            Measurement("a", 0, "Bench", 900.0),
        ])

        assert pooled[("Bench", "a", 0)] == 101.0

    def test_arms_and_positions_do_not_share_a_bucket(self):
        pooled = pool_by_position([
            Measurement("a", 0, "Bench", 10.0),
            Measurement("a", 1, "Bench", 20.0),
            Measurement("b", 0, "Bench", 30.0),
            Measurement("b", 1, "Bench", 40.0),
        ])

        assert pooled == {
            ("Bench", "a", 0): 10.0,
            ("Bench", "a", 1): 20.0,
            ("Bench", "b", 0): 30.0,
            ("Bench", "b", 1): 40.0,
        }


class TestParsing:
    def _write_report(self, tmp_path, benchmarks):
        results = tmp_path / "results"
        results.mkdir()
        (results / "x-report-full.json").write_text(
            json.dumps({"Benchmarks": benchmarks}), encoding="utf-8")
        return str(tmp_path)

    def test_reads_means_by_full_name(self, tmp_path):
        directory = self._write_report(tmp_path, [
            {"FullName": "Ns.Class.Fib", "Statistics": {"Mean": 1234.5}},
            {"FullName": "Ns.Class.Classes", "Statistics": {"Mean": 99.0}},
        ])

        assert parse_bdn_means(directory) == {"Ns.Class.Fib": 1234.5, "Ns.Class.Classes": 99.0}

    def test_missing_report_raises(self, tmp_path):
        with pytest.raises(FileNotFoundError):
            parse_bdn_means(str(tmp_path))

    def test_report_without_means_raises_rather_than_returning_empty(self, tmp_path):
        """
        The failure this module exists to prevent: a run that measured nothing must not
        flow through as "no difference between the arms".
        """
        directory = self._write_report(tmp_path, [{"FullName": "Ns.Class.Fib"}])

        with pytest.raises(ValueError, match="no benchmark means"):
            parse_bdn_means(directory)


class TestReport:
    def test_states_the_artifact_and_the_counts(self):
        verdicts = evaluate(measurements(a_0=100.0, a_1=100.0, b_0=150.0, b_1=150.0), "a", "b")
        text = report(verdicts, "a", "b", rounds=4)

        assert "1 of 1 benchmark(s) produced a measurable delta" in text
        assert "7-10%" in text
        assert "4 round(s)" in text
