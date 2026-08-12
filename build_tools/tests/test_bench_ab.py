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
    split_discarded,
    warmup_split_lines,
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


def one_round(round_index: int, first_arm: str, first_ns: float,
              second_arm: str, second_ns: float) -> list[Measurement]:
    """One round: *first_arm* ran in position 0, *second_arm* in position 1."""
    return [
        Measurement(first_arm, 0, "Bench", first_ns, round_index),
        Measurement(second_arm, 1, "Bench", second_ns, round_index),
    ]


def unsettled_then_settled() -> list[Measurement]:
    """
    A run whose opening rounds carry the #1418 session transient and whose settled tail
    contains a real 30% regression in arm "b".

    Rounds 0-1 are inflated and, because the arms alternate position, inflated UNEQUALLY —
    which is the property that makes the transient survive interleaving. Rounds 2-3 are the
    steady state: b is 30% slower in both orderings.
    """
    return (
        one_round(0, "a", 170.0, "b", 130.0)
        + one_round(1, "b", 160.0, "a", 100.0)
        + one_round(2, "a", 100.0, "b", 130.0)
        + one_round(3, "b", 130.0, "a", 100.0)
    )


class TestDiscardWindow:
    def test_pooling_the_settled_tail_finds_the_real_regression(self):
        _, pooled = split_discarded(unsettled_then_settled(), discard_rounds=2)
        verdicts = evaluate(pooled, "a", "b")

        assert verdicts[0].measured
        assert verdicts[0].pooled_delta_percent == pytest.approx(30.0)

    def test_without_the_window_the_same_regression_is_lost(self):
        """
        The mutation check for this feature. Same data, discard window disabled: the unsettled
        opening rounds push the two orderings to opposite signs, the sign gate refuses the
        comparison, and a real 30% regression is reported as position-dominated. If this test
        ever agrees with the one above, the window has stopped doing anything.
        """
        _, pooled = split_discarded(unsettled_then_settled(), discard_rounds=0)
        verdicts = evaluate(pooled, "a", "b")

        assert not verdicts[0].measured
        assert "position-dominated" in verdicts[0].reason

    def test_whole_rounds_are_dropped_so_positions_stay_balanced(self):
        _, pooled = split_discarded(unsettled_then_settled(), discard_rounds=2)

        cells: dict[tuple[str, int], int] = {}
        for m in pooled:
            cells[(m.arm, m.position)] = cells.get((m.arm, m.position), 0) + 1

        assert cells == {("a", 0): 1, ("a", 1): 1, ("b", 0): 1, ("b", 1): 1}

    def test_split_report_shows_the_gap_the_pooled_delta_cannot(self):
        lines = warmup_split_lines(unsettled_then_settled(), discard_rounds=2)
        text = "\n".join(lines)

        assert "2 discarded round(s)" in text
        # Discarded median 145 ns vs pooled median 115 ns: the transient is visible as a
        # positive gap even though both arms carry it and the pooled delta cannot show it.
        assert "discarded 145 ns" in text
        assert "pooled 115 ns" in text
        assert "+26.1%" in text

    def test_split_report_is_printed_even_when_nothing_is_discarded(self):
        """An unconditional report: a reader must never have to wonder whether it was omitted
        because the run was settled or because the window was off."""
        lines = warmup_split_lines(unsettled_then_settled(), discard_rounds=0)

        assert lines == ["Warm-up split: no rounds discarded (--discard-rounds 0)."]


class TestReport:
    def test_states_the_artifact_and_the_counts(self):
        verdicts = evaluate(measurements(a_0=100.0, a_1=100.0, b_0=150.0, b_1=150.0), "a", "b")
        text = report(verdicts, "a", "b", rounds=4, discard_rounds=4)

        assert "1 of 1 benchmark(s) produced a measurable delta" in text
        assert "7-10%" in text
        # 4 pooled of 8 run: the invocation count must follow the discard window, or the
        # header understates what the machine actually did.
        assert "4 pooled round(s) of 8 run" in text
        assert "16 benchmark invocations" in text
        assert "(8 discarded as unsettled)" in text

    def test_report_carries_the_split_lines(self):
        verdicts = evaluate(measurements(a_0=100.0, a_1=100.0, b_0=150.0, b_1=150.0), "a", "b")
        text = report(verdicts, "a", "b", rounds=4, discard_rounds=4,
                      split_lines=["Warm-up split: MARKER"])

        assert "Warm-up split: MARKER" in text

    def test_report_says_the_threshold_was_not_re_derived(self):
        """
        The floor is unchanged after #1418 and the reason is deferral, not a measurement.
        Stating that in the output is what stops the next reader from assuming the number
        was re-validated against the settled rounds.
        """
        verdicts = evaluate(measurements(a_0=100.0, a_1=100.0, b_0=150.0, b_1=150.0), "a", "b")
        text = report(verdicts, "a", "b", rounds=4, discard_rounds=4)

        assert "has NOT been re-derived" in text
        assert "BASELINE.md" in text
