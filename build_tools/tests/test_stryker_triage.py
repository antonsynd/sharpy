"""Tests for build_tools/stryker/triage.py — Stryker survivor triage with the R-GB ignore list as data (#2300, P68).

The fixture report is hand-built: five small C# sources shaped like the real Lexer partials, and one
mutant per bucket kind (each Stryker status, each ignore entry, each fieldOf arm, each C1–C7 class rule
and the unclassified fallback). Locations are computed here from the needle text, 1-based, independently
of the script's own offset arithmetic.
"""
from __future__ import annotations

import copy
import importlib.util
import json
import subprocess
import sys
from pathlib import Path

import pytest

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / "build_tools" / "stryker" / "triage.py"
IGNORE_FILE = REPO / "build_tools" / "stryker" / "lexer-ignore.json"
LEXER_DIR = REPO / "src" / "Sharpy.Compiler" / "Lexer"

_spec = importlib.util.spec_from_file_location("stryker_triage", SCRIPT)
triage = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = triage  # dataclasses resolve annotations through sys.modules
_spec.loader.exec_module(triage)


# --------------------------------------------------------------------------- #
# fixture sources
# --------------------------------------------------------------------------- #
LEXER_CS = """namespace Sharpy.Compiler.Lexer;

public partial class Lexer
{
    private static readonly Dictionary<string, TokenType> Keywords = new()
    {
        { "union", TokenType.Union },
        { "def", TokenType.Def },
    };

    private static readonly IReadOnlySet<string> _keywordNames = new HashSet<string>(Keywords.Keys) { "self" };

    public Lexer(string source, ILogger logger)
    {
        _source = source;
        _logger.LogInfo($"Lexer initialized, source length: {source.Length}");
    }

    private LexerAbortException ReportError(string message, int line, int column, string code)
    {
        _diagnostics.AddError(message, line, column, code);
        return new LexerAbortException();
    }

    public List<Token> TokenizeAll()
    {
        if (_diagnostics.ErrorCount >= MaxErrors)
            _diagnostics.AddWarning($"Too many errors ({MaxErrors}); further errors suppressed.", _line, _column);
        return _tokens;
    }

    public Token NextToken()
    {
        if (_position >= _source.Length)
            return CreateToken(TokenType.Eof, "", _line, _column, _position);
        var current = _source[_position];
        if (current == '\\n')
        {
            ConsumeLineBreak();
            return CreateToken(TokenType.Newline, "\\n", _line, _column, _position);
        }
        return char.IsDigit(current) ? ReadNumber() : ReadOperatorOrDelimiter();
    }

    private void ConsumeLineBreak()
    {
        if (_source[_position] == '\\r' && _position + 1 < _source.Length && _source[_position + 1] == '\\n')
            _position++;
        _position++;
        _line++;
        _column = 1;
    }

    private Token ReadOperatorOrDelimiter()
    {
        var c = _source[_position];
        _position++;
        _column++;
        return c switch
        {
            '+' => CreateToken(TokenType.Plus, "+", _line, _column - 1, _position - 1),
            '{' => CreateToken(TokenType.LeftBrace, "{", _line, _column - 1, _position - 1),
            _ => throw ReportError($"Unexpected character: '{c}'", _line, _column, "SPY0001")
        };
    }

    private Token CreateToken(TokenType type, string value, int startLine, int startColumn, int startPosition,
        int? sourceLength = null)
    {
        return new Token(type, value, startLine, startColumn, startPosition) { SourceLength = sourceLength ?? value.Length };
    }
}
"""

LITERALS_CS = """namespace Sharpy.Compiler.Lexer;

public partial class Lexer
{
    private Token ReadNumber()
    {
        var sb = new StringBuilder();
        while (_position < _source.Length && (char.IsDigit(_source[_position]) || _source[_position] == '_'))
        {
            if (_source[_position] == '_' && _position + 1 < _source.Length && _source[_position + 1] == '_')
                throw ReportError("Consecutive underscores in number literal", _line, _column, "SPY0007");
            sb.Append(_source[_position]);
            _position++;
            _column++;
        }
        return CreateToken(TokenType.Integer, sb.ToString(), _line, _column, _position);
    }
}
"""

FSTRINGS_CS = """namespace Sharpy.Compiler.Lexer;

public partial class Lexer
{
    private class FStringContext
    {
        public bool IsTriple { get; init; }
        public int Depth { get; set; }
        public string Kind => IsTriple ? "f-string (triple)" : "f-string";
    }

    private bool IsPreCloseNewline(FStringContext context)
    {
        return context.IsTriple && _source[_position] == '\\n';
    }

    private void SkipHoleTrivia(FStringField field)
    {
        while (_source[_position] == ' ')
            _position++;
    }

    private Token NextFStringToken(FStringContext context)
    {
        if (context.Depth < 0)
            throw ReportUnclosedField(context);
        if (_source[_position] == '}' && context.Depth == 0)
            throw ReportFieldEndedByQuote(context, $"{context.Kind}: expecting '}}'");
        return CreateToken(TokenType.FStringExprEnd, "}", _line, _column, _position);
    }
}
"""

LITERAL_LOSS_CS = """namespace Sharpy.Compiler.Lexer;

public partial class Lexer
{
    private static bool DroppedLineContinues(string text)
    {
        var depth = 0;
        foreach (var ch in text)
        {
            if (ch == '(') depth++;
        }
        return depth > 0;
    }
}
"""

INDENTATION_CS = """namespace Sharpy.Compiler.Lexer;

public partial class Lexer
{
    private int MeasureIndentation()
    {
        var indent = 0;
        while (_source[_position] == ' ') { indent++; _position++; }
        if (indent % 4 != 0)
            throw ReportError($"Indentation must be multiple of 4 spaces (found {indent})", _line, 1, "SPY0003");
        return indent;
    }
}
"""

ROOT = "/repo/src/Sharpy.Compiler/Lexer/"
SOURCES = {
    "Lexer.cs": LEXER_CS,
    "Lexer.Literals.cs": LITERALS_CS,
    "Lexer.FStrings.cs": FSTRINGS_CS,
    "Lexer.LiteralLoss.cs": LITERAL_LOSS_CS,
    "Lexer.Indentation.cs": INDENTATION_CS,
}


def _loc(src: str, needle: str, nth: int = 0) -> dict:
    at = -1
    for _ in range(nth + 1):
        at = src.index(needle, at + 1)
    end = at + len(needle)

    def lc(off: int) -> dict:
        line = src.count("\n", 0, off) + 1
        col = off - (src.rfind("\n", 0, off) + 1) + 1
        return {"line": line, "column": col}

    return {"start": lc(at), "end": lc(end)}


# (id, file, needle, nth, mutatorName, status, expected bucket status, expected invocation, expected class)
MUTANTS = [
    ("killed", "Lexer.cs", "_line++;", 0, "Statement mutation", "Killed", "killed", "-", None),
    ("timeout", "Lexer.FStrings.cs", "_position++;", 0, "Statement mutation", "Timeout", "timeout", "-", None),
    ("nocov", "Lexer.LiteralLoss.cs", "depth++", 0,
     "PostIncrementExpression to PostDecrementExpression mutation", "NoCoverage", "noCoverage", "-", "C5"),
    ("ignored-by-stryker", "Lexer.Indentation.cs", "return indent;", 0, "Statement mutation", "Ignored",
     "ignoredByStryker", "-", None),
    ("compile-error", "Lexer.cs", "_column = 1;", 0, "Statement mutation", "CompileError", "compileError", "-", None),
    ("runtime-error", "Lexer.cs", "_position++;", 1, "Statement mutation", "RuntimeError", "runtimeError", "-", None),
    # ignore entry 0: message text (a Survived and a Killed one — both leave the score)
    ("report-error-msg", "Lexer.Literals.cs", '"Consecutive underscores in number literal"', 0, "String mutation",
     "Survived", "ignoredByList", "ReportError", None),
    ("loginfo-msg", "Lexer.cs", '$"Lexer initialized, source length: {source.Length}"', 0, "String mutation",
     "Killed", "ignoredByList", "LogInfo", None),
    ("field-ended-msg", "Lexer.FStrings.cs", '$"{context.Kind}: expecting \'}}\'"', 0, "String mutation",
     "Survived", "ignoredByList", "ReportFieldEndedByQuote", None),
    # ignore entry 1: operator Value — and the two fieldOf arms that must NOT match
    ("op-value", "Lexer.cs", '"+"', 0, "String mutation", "Survived", "ignoredByList", "CreateToken", None),
    ("newline-value", "Lexer.cs", '"\\n"', 0, "String mutation", "Survived", "survived", "CreateToken", "C3"),
    ("fstring-value", "Lexer.FStrings.cs", '"}"', 0, "String mutation", "Survived", "survived", "CreateToken", "C4"),
    # classes
    ("keyword", "Lexer.cs", '"union"', 0, "String mutation", "Survived", "survived", "-", "C7"),
    ("column", "Lexer.Literals.cs", "_column++;", 0, "Statement mutation", "Survived", "survived", "-", "C2"),
    ("eof-bound", "Lexer.Literals.cs", "_position < _source.Length", 0, "Equality mutation", "Survived",
     "survived", "-", "C6"),
    ("number", "Lexer.Literals.cs", "sb.Append(_source[_position]);", 0, "Statement mutation", "Survived",
     "survived", "-", "C1"),
    ("preclose", "Lexer.FStrings.cs", "context.IsTriple && _source[_position] == '\\n'", 0, "Logical mutation",
     "Survived", "survived", "-", "C3"),
    ("fstring-arm", "Lexer.FStrings.cs", "context.Depth == 0", 0, "Equality mutation", "Survived", "survived", "-",
     "C4"),
    ("literal-loss", "Lexer.LiteralLoss.cs", "depth > 0", 0, "Equality mutation", "Survived", "survived", "-", "C5"),
    ("indent", "Lexer.Indentation.cs", "indent % 4", 0, "Arithmetic mutation", "Survived", "survived", "-",
     "unclassified"),
]


def _report() -> dict:
    files = {ROOT + name: {"language": "cs", "source": src, "mutants": []} for name, src in SOURCES.items()}
    for mid, fname, needle, nth, mutator, status, *_ in MUTANTS:
        files[ROOT + fname]["mutants"].append({
            "id": mid, "mutatorName": mutator, "replacement": "?",
            "location": _loc(SOURCES[fname], needle, nth), "status": status,
        })
    return {"schemaVersion": "2", "thresholds": {"high": 80, "low": 60}, "files": files}


def _ignore() -> dict:
    return json.loads(IGNORE_FILE.read_text())


def _by_id(result: dict) -> dict:
    return {row["id"]: row for row in result["mutants"]}


# --------------------------------------------------------------------------- #
# the fixture report
# --------------------------------------------------------------------------- #
@pytest.mark.parametrize("mid,fname,needle,nth,mutator,status,bucket,invocation,cls", MUTANTS,
                         ids=[m[0] for m in MUTANTS])
def test_bucket_invocation_and_class(mid, fname, needle, nth, mutator, status, bucket, invocation, cls):
    row = _by_id(triage.triage(_report(), _ignore()))[mid]
    assert row["status"] == bucket
    assert row["invocation"] == invocation
    assert row.get("class") == cls
    key = f"{fname}|{triage.mutator_id(mutator)}|{invocation}|{bucket}"
    assert triage.triage(_report(), _ignore())["buckets"][key] >= 1


def test_every_bucket_kind_is_covered_by_the_fixture():
    statuses = {m[6] for m in MUTANTS}
    assert statuses == {"killed", "survived", "timeout", "noCoverage", "ignoredByStryker", "compileError",
                        "runtimeError", "ignoredByList"}
    assert {m[8] for m in MUTANTS} - {None} == set(triage.CLASS_NAMES)


def test_counts_and_score_arithmetic():
    r = triage.triage(_report(), _ignore())
    assert r["total"] == len(MUTANTS) == 20
    assert (r["killed"], r["survived"], r["timeout"], r["noCoverage"]) == (2, 13, 1, 1)
    assert (r["compileError"], r["runtimeError"], r["ignoredByStryker"], r["pending"]) == (1, 1, 1, 0)
    assert r["ignoredByList"] == 4
    assert r["afterList"] == {"killed": 1, "survived": 10, "timeout": 1, "noCoverage": 1}
    # Stryker: (killed + timeout) / (killed + timeout + survived + noCoverage)
    assert r["score"] == round(3 * 100 / 17, 2) == 17.65
    assert r["scoreIgnoringListed"] == round(2 * 100 / 13, 2) == 15.38
    assert sum(r["buckets"].values()) == r["total"]
    assert [e["matched"] for e in r["listedByEntry"]] == [3, 1]


def test_classes_count_every_undetected_mutant_once():
    r = triage.triage(_report(), _ignore())
    assert r["classes"] == {"C1": 1, "C2": 1, "C3": 2, "C4": 2, "C5": 2, "C6": 1, "C7": 1, "unclassified": 1}
    assert sum(r["classes"].values()) == r["afterList"]["survived"] + r["afterList"]["noCoverage"]


def test_classes_flag_lists_the_mutants_per_class():
    r = triage.triage(_report(), _ignore(), with_classes=True)
    assert [m["id"] for m in r["classMutants"]["C7"]] == ["keyword"]
    assert sorted(m["id"] for m in r["classMutants"]["C3"]) == ["newline-value", "preclose"]
    assert "classMutants" not in triage.triage(_report(), _ignore())


# --------------------------------------------------------------------------- #
# the guard: `within` names the CALL, both directions
# --------------------------------------------------------------------------- #
def test_within_report_error_lists_the_message_mutant_and_within_read_number_does_not():
    listed = _by_id(triage.triage(_report(), _ignore()))["report-error-msg"]
    assert listed["status"] == "ignoredByList"
    assert "class" not in listed

    mutated = _ignore()
    mutated["entries"][0]["within"] = "ReadNumber"  # ReadNumber is invoked (in NextToken), so the entry loads
    row = _by_id(triage.triage(_report(), mutated))["report-error-msg"]
    assert row["status"] == "survived"
    assert row["class"] == "C1"  # the mutant sits in ReadNumber's body, inside the ReportError call


def test_field_of_is_required_for_the_create_token_entry():
    unrestricted = _ignore()
    del unrestricted["entries"][1]["fieldOf"]
    rows = _by_id(triage.triage(_report(), unrestricted))
    assert rows["newline-value"]["status"] == "ignoredByList"
    assert rows["fstring-value"]["status"] == "ignoredByList"


# --------------------------------------------------------------------------- #
# loud failures
# --------------------------------------------------------------------------- #
@pytest.mark.parametrize("within,absent", [("ReadNumbr", "ReadNumbr"), ("ReportError|LogDebug", "LogDebug")])
def test_within_naming_no_invoked_method_fails_loudly(within, absent):
    bad = _ignore()
    bad["entries"][0]["within"] = within
    with pytest.raises(triage.TriageError, match=f"'{absent}' names no method invoked"):
        triage.triage(_report(), bad)


def test_cli_exits_nonzero_with_a_message_on_an_absent_within(tmp_path):
    bad = _ignore()
    bad["entries"][0]["within"] = "ReadNumbr"
    (tmp_path / "r.json").write_text(json.dumps(_report()))
    (tmp_path / "i.json").write_text(json.dumps(bad))
    proc = subprocess.run([sys.executable, str(SCRIPT), "--report", str(tmp_path / "r.json"),
                           "--ignore", str(tmp_path / "i.json")], capture_output=True, text=True)
    assert proc.returncode == 2
    assert "ReadNumbr" in proc.stderr
    assert proc.stdout == ""


def test_cli_prints_the_contract_keys(tmp_path):
    (tmp_path / "r.json").write_text(json.dumps(_report()))
    proc = subprocess.run([sys.executable, str(SCRIPT), "--report", str(tmp_path / "r.json"),
                           "--ignore", str(IGNORE_FILE), "--classes"], capture_output=True, text=True)
    assert proc.returncode == 0, proc.stderr
    out = json.loads(proc.stdout)
    for key in ("score", "scoreIgnoringListed", "total", "killed", "survived", "timeout", "ignoredByStryker",
                "ignoredByList", "buckets", "classes", "classMutants"):
        assert key in out
    assert out["score"] == 17.65


@pytest.mark.parametrize("mutate,message", [
    (lambda e: e.pop("reason"), "'reason' must be a non-empty string"),
    (lambda e: e.update(mutator="strings"), "not a Stryker identifier"),
    (lambda e: e.update(fieldOf="operator Text"), "fieldOf 'operator Text'"),
    (lambda e: e.update(within_methods="x"), "unknown keys"),
])
def test_malformed_ignore_entry_fails_loudly(mutate, message):
    bad = _ignore()
    mutate(bad["entries"][1])
    with pytest.raises(triage.TriageError, match=message):
        triage.triage(_report(), bad)


def test_unknown_status_and_out_of_range_location_fail_loudly():
    r = _report()
    r["files"][ROOT + "Lexer.cs"]["mutants"][0]["status"] = "Survivd"
    with pytest.raises(triage.TriageError, match="unknown status"):
        triage.triage(r, _ignore())
    r = _report()
    r["files"][ROOT + "Lexer.cs"]["mutants"][0]["location"]["start"]["line"] = 999
    with pytest.raises(triage.TriageError, match="line 999 is outside"):
        triage.triage(r, _ignore())


def test_unknown_mutator_name_is_counted_not_dropped():
    r = _report()
    r["files"][ROOT + "Lexer.cs"]["mutants"][0]["mutatorName"] = "Brand new mutation"
    out = triage.triage(r, _ignore())
    assert out["total"] == 20
    assert "Lexer.cs|unknown:Brand new mutation|-|killed" in out["buckets"]


def test_missing_source_is_read_from_disk(tmp_path):
    r = _report()
    path = tmp_path / "Lexer.Indentation.cs"
    path.write_text(INDENTATION_CS)
    entry = r["files"].pop(ROOT + "Lexer.Indentation.cs")
    del entry["source"]
    r["files"][str(path)] = entry
    rows = _by_id(triage.triage(r, _ignore()))
    assert rows["indent"]["class"] == "unclassified"


# --------------------------------------------------------------------------- #
# mutator names and the committed data against the real Lexer sources
# --------------------------------------------------------------------------- #
@pytest.mark.parametrize("display,ident", [
    ("String mutation", "string"),
    ("String Method Mutation (Replace Trim() with TrimStart())", "stringmethod"),
    ("Equality mutation", "equality"),
    ("Negate expression", "boolean"),
    ("Block removal mutation", "block"),
    ("Conditional (true) mutation", "conditional"),
    ("Null coalescing mutation (remove left)", "nullcoalescing"),
    ("AddAssignmentExpression to SubtractAssignmentExpression mutation", "assignment"),
    ("PostIncrementExpression to PostDecrementExpression mutation", "update"),
    ("PreDecrementExpression to PreIncrementExpression mutation", "update"),
    ("UnaryMinusExpression to UnaryPlusExpression mutation", "unary"),
    ("LogicalNotExpression to un-LogicalNotExpression mutation", "unary"),
    ("Linq method mutation (First() to Last())", "linq"),
])
def test_mutator_display_names_map_to_config_identifiers(display, ident):
    assert triage.mutator_id(display) == ident


def _string_mutants_over_real_lexer() -> dict:
    """A synthetic report: one Survived String mutation at every string literal in src/Sharpy.Compiler/Lexer."""
    import bisect
    files = {}
    for f in sorted(LEXER_DIR.glob("*.cs")):
        src = f.read_text(encoding="utf-8-sig")
        model = triage.analyze(src)

        def lc(off: int, starts=model.line_starts) -> dict:
            i = bisect.bisect_right(starts, off) - 1
            return {"line": i + 1, "column": off - starts[i] + 1}

        mutants = [{"id": f"{f.name}:{k}", "mutatorName": "String mutation", "status": "Survived",
                    "location": {"start": lc(t.start), "end": lc(t.end)}}
                   for k, t in enumerate(model.toks) if t.kind == "str"]
        files[str(f)] = {"language": "cs", "source": src, "mutants": mutants}
    return {"files": files}


@pytest.mark.skipif(not LEXER_DIR.is_dir(), reason="needs the Sharpy.Compiler sources")
def test_committed_ignore_list_against_the_real_lexer():
    data = _ignore()
    assert len(data["entries"]) == 2
    assert all(e["reason"].strip() for e in data["entries"])
    report = _string_mutants_over_real_lexer()
    out = triage.triage(report, data)  # every within alternative names a real callee, or this raises
    rows = out["mutants"]
    src = {Path(p).name: e["source"].splitlines() for p, e in report["files"].items()}

    def text(row):
        return src[row["file"]][row["line"] - 1]

    listed = [r for r in rows if r["status"] == "ignoredByList"]
    kept = [r for r in rows if r["status"] == "survived"]
    # positive controls: an operator Value, a ReportError message, a LogInfo message
    assert any("TokenType.Plus, \"+\"" in text(r) for r in listed)
    assert any(r["invocation"] == "ReportError" for r in listed)
    assert any(r["invocation"] == "LogInfo" for r in listed)
    # negative controls: Eof's "", Newline's "\n", the f-string family's "}" and the keyword table stay scored
    assert any("TokenType.Eof, \"\"" in text(r) for r in kept)
    assert any("TokenType.Newline, \"\\n\"" in text(r) for r in kept)
    assert any("TokenType.FStringExprEnd, \"}\"" in text(r) for r in kept)
    assert any("{ \"union\", TokenType.Union }" in text(r) for r in kept)
    assert all(r["invocation"] != "ReportError" for r in kept)


# --------------------------------------------------------------------------- #
# the guard: `within` is a FULL match over the callee name, and the list applies only to scored mutants
# --------------------------------------------------------------------------- #
_PREFIX_SRC = """namespace Sharpy.Compiler.Lexer;

public partial class Lexer
{
    private void Trace()
    {
        LogInfo("listed message");
        LogInfoVerbose("not a LogInfo call");
        ReportError("compile-error message", 1, 1, "SPY0001");
    }
}
"""


def _single_file_report(mutants: list[tuple[str, str, str, str]]) -> dict:
    """(id, needle, mutatorName, status) over _PREFIX_SRC."""
    rows = [{"id": mid, "mutatorName": mutator, "replacement": "?", "location": _loc(_PREFIX_SRC, needle),
             "status": status} for mid, needle, mutator, status in mutants]
    return {"schemaVersion": "2", "files": {ROOT + "Lexer.cs": {"language": "cs", "source": _PREFIX_SRC,
                                                                 "mutants": rows}}}


def test_within_matches_the_whole_callee_name_never_a_prefix():
    report = _single_file_report([
        ("listed", '"listed message"', "String mutation", "Survived"),
        ("prefix", '"not a LogInfo call"', "String mutation", "Survived"),
    ])
    ignore = {"entries": [{"mutator": "string", "within": "LogInfo", "reason": "message text"}]}
    rows = _by_id(triage.triage(report, ignore))
    assert rows["listed"]["status"] == "ignoredByList"
    assert rows["prefix"]["status"] == "survived", "LogInfoVerbose is not LogInfo: `within` must fullmatch the callee"
    assert rows["prefix"]["invocation"] == "LogInfoVerbose"


def test_ignore_list_never_reclassifies_an_unscored_mutant():
    report = _single_file_report([
        ("compile-error", '"compile-error message"', "String mutation", "CompileError"),
        ("scored", '"listed message"', "String mutation", "Killed"),
    ])
    ignore = {"entries": [{"mutator": "string", "within": "ReportError|LogInfo", "reason": "message text"}]}
    r = triage.triage(report, ignore)
    rows = _by_id(r)
    assert rows["compile-error"]["status"] == "compileError", "a CompileError mutant is outside the score either way"
    assert rows["scored"]["status"] == "ignoredByList"
    assert (r["compileError"], r["ignoredByList"]) == (1, 1)
    assert [e["matched"] for e in r["listedByEntry"]] == [1]
