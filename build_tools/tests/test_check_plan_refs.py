"""check_plan_refs: paths and file:line are errors, symbols and codes are warnings."""
from pathlib import Path

from build_tools import check_plan_refs as cpr


def _repo(tmp_path: Path) -> Path:
    (tmp_path / "src/A").mkdir(parents=True)
    (tmp_path / "src/A/Foo.cs").write_text("\n".join(f"// line {i}  class FooBar {{}}" for i in range(1, 11)) + "\n")
    (tmp_path / "src/Sharpy.Cli/Commands").mkdir(parents=True)
    (tmp_path / "src/Sharpy.Cli/Commands/RunCommand.cs").write_text("a\nb\nc\n")
    (tmp_path / "src/A/TypeChecker.Expressions.Access.Calls.cs").write_text("x\n" * 40)
    (tmp_path / "src/Sharpy.Compiler/Diagnostics").mkdir(parents=True)
    (tmp_path / "src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs").write_text('public const string X = "SPY0001";\n')
    (tmp_path / "docs").mkdir()
    (tmp_path / "docs/x.md").write_text("# x\n")
    return tmp_path


PLAN = """# plan
See `src/A/Foo.cs` and `docs/x.md`; the seam is `Foo.cs:5`, not `Foo.cs:50`.
The missing one is `src/missing.cs`. Symbols: `FooBar` exists, `NoSuchType` does not.
Codes SPY0001 (known) and SPY9999 (new). Issue #1234. Shorthand: `Access.Calls.cs:30`, `Cli/Commands/RunCommand.cs:2`,
`Cli/Commands/RunCommand.cs:9`. Fixture layout `src/main.spy`; plan `.claude/plans/plan-abc123.md`.

```csharp
class InsideFence {}  // symbols in fences are not checked
```
"""


def test_classification(tmp_path):
    r = cpr.check(PLAN, _repo(tmp_path))
    assert r["paths"]["ok"] == ["docs/x.md", "src/A/Foo.cs"]
    assert r["paths"]["missing"] == ["src/missing.cs"]
    assert r["paths"]["unresolved"] == [".claude/plans/plan-abc123.md", "src/main.spy"]
    assert "Access.Calls.cs:30" in r["filelines"]["ok"]          # bare character-suffix shorthand
    assert "Cli/Commands/RunCommand.cs:2" in r["filelines"]["ok"]  # directory shorthand without src/Sharpy.
    assert any(s.startswith("Cli/Commands/RunCommand.cs:9") for s in r["filelines"]["past_eof"])
    assert "Foo.cs:5" in r["filelines"]["ok"]
    assert any(s.startswith("Foo.cs:50") for s in r["filelines"]["past_eof"])
    assert r["codes"]["ok"] == ["SPY0001"] and r["codes"]["unknown"] == ["SPY9999"]
    assert "FooBar" in r["symbols"]["ok"] and "NoSuchType" in r["symbols"]["unknown"]
    assert "InsideFence" not in r["symbols"]["ok"] + r["symbols"]["unknown"]
    assert r["issues"] == {"1234": {"state": "unchecked"}}


def test_exit_codes(tmp_path):
    repo = _repo(tmp_path)
    _, code = cpr.render(cpr.check(PLAN, repo), strict=False)
    assert code == 1  # a missing path and a past-eof line are errors
    clean = "Only `src/A/Foo.cs` and `Foo.cs:3`; `NoSuchType` is new; SPY9999 is new."
    _, code = cpr.render(cpr.check(clean, repo), strict=False)
    assert code == 0  # unknown symbol/code are warnings
    _, code = cpr.render(cpr.check(clean, repo), strict=True)
    assert code == 1  # ...unless --strict


def test_mutation_control(tmp_path):
    # positive control for the guard: the same plan against a repo where the file exists passes the path check
    repo = _repo(tmp_path)
    (repo / "src/missing.cs").write_text("x\n")
    r = cpr.check(PLAN, repo)
    assert r["paths"]["missing"] == []
