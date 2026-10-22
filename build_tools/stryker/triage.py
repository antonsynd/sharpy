#!/usr/bin/env python3
"""Triage a Stryker.NET ``mutation-report.json`` against an ignore list kept as data (#2289 R-GB, #2300 P68).

Usage::

    python3 build_tools/stryker/triage.py --report <mutation-report.json> \\
        --ignore build_tools/stryker/lexer-ignore.json [--classes] [--universe <dir> ...]

The report follows the mutation-testing-elements schema: ``files: {<path>: {language, source,
mutants: [{id, mutatorName, replacement, location: {start: {line, column}, end: {line, column}},
status, statusReason?}]}}``. Lines and columns are 1-based (Roslyn's 0-based line span + 1); the
column counts UTF-16 code units, as Roslyn does. Each file entry carries its own ``source``; when it
is absent the path is read from disk.

Every mutant is bucketed by (file, mutator, enclosing invocation, status):

- ``mutator`` is Stryker's config identifier (``string``, ``equality``, ``statement``, …), derived
  from the report's display name (``String mutation``, ``Equality mutation``,
  ``PostIncrementExpression to PostDecrementExpression mutation``, …). A display name no rule knows
  is kept as ``unknown:<name>`` — counted, never dropped.
- ``enclosing invocation`` is the callee name of the innermost call whose argument list holds the
  mutant (``-`` when none). It is read from the C# source with a small tokenizer (strings, verbatim,
  interpolated and raw strings, char literals and comments are skipped, so ``'{'`` is not a brace).
- ``status`` is one of ``killed``, ``survived``, ``timeout``, ``noCoverage``, ``ignoredByStryker``,
  ``compileError``, ``runtimeError``, ``pending`` — or ``ignoredByList`` when an ignore entry
  matches a mutant Stryker scored (Killed/Survived/Timeout/NoCoverage). Any other status fails.

Ignore list (``lexer-ignore.json``)::

    {"entries": [{"mutator": "string", "within": "<regex over callee names>",
                  "fieldOf": "<predicate name, optional>", "reason": "<why, required>"}]}

An entry matches a mutant when its ``mutator`` equals the mutant's identifier and some enclosing
invocation's callee name fully matches ``within`` (and, when ``fieldOf`` is given, that invocation
satisfies the named predicate — see ``FIELD_PREDICATES``). ``within`` names a CALL the mutant sits
in, never the method declaring it: a message literal passed to ``ReportError`` inside ``ReadNumber``
is ``within: ReportError``, and ``within: ReadNumber`` does not match it. Each top-level ``|``
alternative of ``within`` must name a callee invoked somewhere in the report's sources (plus every
``*.cs`` under each ``--universe`` directory), otherwise the script exits 2: an entry that can
match nothing is a silent zero, not an ignore.

Score (Stryker's definition, in percent, two decimals; ``null`` when nothing was scored)::

    score = (killed + timeout) / (killed + timeout + survived + noCoverage) * 100

CompileError, RuntimeError, Ignored and Pending mutants are outside both terms. ``scoreIgnoringListed``
is the same formula after removing every ``ignoredByList`` mutant from the four counts.

Classes (#2300 rows C1–C7) are assigned to every mutant left UNDETECTED after the ignore list
(survived + noCoverage), first matching rule wins:

    C7  enclosing member is ``Keywords`` or ``_keywordNames``
    C2  a non-block mutant whose original text names ``_column`` or ``SourceLength``/``sourceLength``
    C6  an equality mutant whose original text compares against a ``Length`` bound (``< _source.Length``)
    C1  enclosing member in ReadNumber / ReadHexNumber / ReadBinaryNumber / ReadOctalNumber /
        ReadPrefixedIntegerSuffix
    C3  enclosing member is NextToken / ConsumeLineBreak / IsPreCloseNewline or a ``*HoleTrivia*`` method
    C5  file ``Lexer.LiteralLoss.cs``
    C4  file ``Lexer.FStrings.cs`` (the rest of it)
    unclassified  everything else (counted, never dropped)

``--classes`` adds ``classMutants``: per class, the mutants (file, line, column, mutator, member, status).
Exit codes: 0 ok, 2 bad input (unreadable report, unknown status, malformed ignore entry, a ``within``
alternative that names no invoked method, a location outside its source).
"""

from __future__ import annotations

import argparse
import json
import os
import re
import string
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Optional


class TriageError(Exception):
    """Bad input: the script refuses rather than report a number it cannot stand behind."""


# --------------------------------------------------------------------------- #
# mutator display name -> Stryker config identifier
# --------------------------------------------------------------------------- #
# Display names read from dotnet-stryker 5.0.0's Stryker.Core.dll user strings; the composed ones
# ("{Kind} to {Kind} mutation", "{Kind} to un-{Kind} mutation") are matched by shape.
_MUTATOR_RULES: list[tuple[str, str]] = [
    (r"String mutation", "string"),
    (r"String Method Mutation.*", "stringmethod"),
    (r"Arithmetic mutation", "arithmetic"),
    (r"Equality mutation", "equality"),
    (r"Logical mutation", "logical"),
    (r"Bitwise mutation", "bitwise"),
    (r"Boolean mutation|Negate expression", "boolean"),
    (r"Statement mutation", "statement"),
    (r"Block removal mutation", "block"),
    (r"(Array|Collection|Object) initializer mutation", "initializer"),
    (r"Collection expression mutation", "collectionexpression"),
    (r"Conditional \((true|false)\) mutation", "conditional"),
    (r"Null coalescing mutation.*", "nullcoalescing"),
    (r"Remove checked expression", "checked"),
    (r"Linq method mutation.*", "linq"),
    (r"Math method mutation.*", "math"),
    (r"Regex.*", "regex"),
    (r"\w*Assignment\w* to \w+ mutation", "assignment"),
    (r"(Pre|Post)(Increment|Decrement)Expression to \w+ mutation", "update"),
    (r"\w+Expression to (un-)?\w+ mutation", "unary"),
]

KNOWN_MUTATORS = frozenset(ident for _, ident in _MUTATOR_RULES)


def mutator_id(display_name: str) -> str:
    """Map a report ``mutatorName`` to its Stryker config identifier (``unknown:<name>`` if none)."""
    for pattern, ident in _MUTATOR_RULES:
        if re.fullmatch(pattern, display_name):
            return ident
    if display_name in KNOWN_MUTATORS:  # a report already spelled with identifiers
        return display_name
    return f"unknown:{display_name}"


# --------------------------------------------------------------------------- #
# status mapping
# --------------------------------------------------------------------------- #
_STATUS = {
    "Killed": "killed",
    "Survived": "survived",
    "Timeout": "timeout",
    "NoCoverage": "noCoverage",
    "Ignored": "ignoredByStryker",
    "CompileError": "compileError",
    "RuntimeError": "runtimeError",
    "Pending": "pending",
}
SCORED = frozenset({"killed", "survived", "timeout", "noCoverage"})
UNDETECTED = frozenset({"survived", "noCoverage"})


def score_of(counts: dict[str, int]) -> Optional[float]:
    detected = counts.get("killed", 0) + counts.get("timeout", 0)
    valid = detected + counts.get("survived", 0) + counts.get("noCoverage", 0)
    if valid == 0:
        return None
    return round(detected * 100.0 / valid, 2)


# --------------------------------------------------------------------------- #
# C# tokenizer (enough structure for members and invocations; not a parser)
# --------------------------------------------------------------------------- #
@dataclass
class Tok:
    kind: str  # "id" | "str" | "char" | "num" | "punct"
    text: str
    start: int
    end: int
    interpolated: bool = False
    value: Optional[str] = None  # decoded content of a non-interpolated string


_TWO_CHAR = {"=>", "==", "!=", "<=", ">=", "&&", "||", "??", "::", "++", "--", "+=", "-=", "*=",
             "/=", "%=", "&=", "|=", "^=", "?.", "->"}
_SIMPLE_ESCAPES = {"\\": "\\", '"': '"', "'": "'", "0": "\0", "a": "\a", "b": "\b", "f": "\f",
                   "n": "\n", "r": "\r", "t": "\t", "v": "\v", "e": "\x1b"}


class _Scanner:
    def __init__(self, src: str):
        self.src = src
        self.toks: list[Tok] = []

    def scan(self, pos: int, in_hole: bool = False) -> int:
        """Tokenize code from ``pos``. In a hole, stop at a depth-0 ``}`` or ``:`` (format spec)
        or ``,`` (alignment, scanned as code anyway) and return its index."""
        src, n = self.src, len(self.src)
        depth = 0
        while pos < n:
            c = src[pos]
            if c.isspace():
                pos += 1
                continue
            if src.startswith("//", pos):
                nl = src.find("\n", pos)
                pos = n if nl < 0 else nl
                continue
            if src.startswith("/*", pos):
                close = src.find("*/", pos + 2)
                pos = n if close < 0 else close + 2
                continue
            m = re.match(r"[$@]*\"", src[pos:pos + 8]) if c in "$@\"" else None
            if m:
                pos = self._string(pos, m.group()[:-1])
                continue
            if c == "'":
                pos = self._char(pos)
                continue
            if c.isdigit():
                m2 = re.compile(r"[0-9][0-9A-Za-z_.]*").match(src, pos)
                self.toks.append(Tok("num", m2.group(), pos, m2.end()))
                pos = m2.end()
                continue
            if c.isalpha() or c == "_" or (c == "@" and pos + 1 < n and (src[pos + 1].isalpha() or src[pos + 1] == "_")):
                m2 = re.compile(r"@?[A-Za-z_][A-Za-z0-9_]*").match(src, pos)
                self.toks.append(Tok("id", m2.group().lstrip("@"), pos, m2.end()))
                pos = m2.end()
                continue
            if in_hole:
                if c in "([{":
                    depth += 1
                elif c in ")]}":
                    if depth == 0 and c == "}":
                        return pos
                    depth -= 1
                elif depth == 0 and c == ":" and not src.startswith("::", pos) and not src.startswith("::", pos - 1):
                    return pos
            two = src[pos:pos + 2]
            if two in _TWO_CHAR:
                self.toks.append(Tok("punct", two, pos, pos + 2))
                pos += 2
            else:
                self.toks.append(Tok("punct", c, pos, pos + 1))
                pos += 1
        if in_hole:
            raise TriageError(f"unterminated interpolation hole at offset {pos}")
        return pos

    def _char(self, pos: int) -> int:
        src, i = self.src, pos + 1
        while i < len(src) and src[i] != "'":
            i += 2 if src[i] == "\\" else 1
        self.toks.append(Tok("char", src[pos:i + 1], pos, i + 1))
        return i + 1

    def _string(self, pos: int, prefix: str) -> int:
        src, n = self.src, len(self.src)
        dollars = prefix.count("$")
        verbatim = "@" in prefix
        q = pos + len(prefix)
        quotes = 0
        while q + quotes < n and src[q + quotes] == '"':
            quotes += 1
        holes_start = len(self.toks)
        if quotes >= 3:  # raw string literal: closes at the same run of quotes; holes use `dollars` braces
            i = q + quotes
            closer = '"' * quotes
            while i < n and not src.startswith(closer, i):
                if dollars and src.startswith("{" * dollars, i):
                    run = len(src[i:]) - len(src[i:].lstrip("{"))
                    i = self._hole(i + run, dollars)
                    continue
                i += 1
            end = min(n, i + quotes)
            self._emit_string(pos, end, bool(dollars), None, holes_start)
            return end
        i = q + 1
        content: list[str] = []
        while i < n:
            ch = src[i]
            if ch == '"':
                if verbatim and src.startswith('""', i):
                    content.append('"')
                    i += 2
                    continue
                i += 1
                break
            if ch == "\\" and not verbatim:
                esc = src[i + 1] if i + 1 < n else ""
                content.append(_SIMPLE_ESCAPES.get(esc, "\\" + esc))
                i += 2
                continue
            if dollars and ch == "{":
                if src.startswith("{{", i):
                    content.append("{")
                    i += 2
                    continue
                i = self._hole(i + 1, 1)
                continue
            if dollars and src.startswith("}}", i):
                content.append("}")
                i += 2
                continue
            if ch == "\n" and not verbatim:
                raise TriageError(f"newline inside a regular string literal at offset {pos}")
            content.append(ch)
            i += 1
        self._emit_string(pos, i, bool(dollars), None if dollars else "".join(content), holes_start)
        return i

    def _hole(self, pos: int, braces: int) -> int:
        stop = self.scan(pos, in_hole=True)
        src = self.src
        if src[stop] == ":":  # format spec: literal text up to the closing brace(s)
            close = src.find("}" * braces, stop)
            if close < 0:
                raise TriageError(f"unterminated format spec at offset {stop}")
            stop = close
        return stop + braces

    def _emit_string(self, start: int, end: int, interpolated: bool, value: Optional[str], at: int) -> None:
        # The string precedes its hole tokens so the list stays in source order.
        self.toks.insert(at, Tok("str", self.src[start:end], start, end, interpolated, value))


def tokenize(src: str) -> list[Tok]:
    sc = _Scanner(src)
    sc.scan(0)
    return sc.toks


# --------------------------------------------------------------------------- #
# structure: invocations and members
# --------------------------------------------------------------------------- #
_NOT_CALLEES = frozenset({
    "if", "while", "for", "foreach", "switch", "catch", "using", "lock", "typeof", "nameof", "sizeof",
    "default", "checked", "unchecked", "fixed", "when", "return", "throw", "new", "is", "as", "in",
    "await", "else", "case", "out", "ref", "var", "stackalloc", "not", "and", "or",
})
_MODIFIERS = frozenset({"public", "private", "protected", "internal", "static", "readonly", "const", "volatile",
                        "override", "virtual", "abstract", "sealed", "async", "extern", "unsafe", "new",
                        "partial", "required", "ref", "file"})
_EXPR_PREFIX_KEYWORDS = frozenset({"return", "throw", "new", "await", "in", "is", "as", "case", "when",
                                   "yield", "else", "out", "ref", "typeof", "nameof", "not", "and", "or"})
_TYPE_KEYWORDS = frozenset({"class", "struct", "interface", "record", "namespace"})


@dataclass
class Invocation:
    name: str
    open_idx: int
    close_idx: int
    args_start: int  # offset just after '('
    args_end: int  # offset of ')'


@dataclass
class Member:
    name: str
    qualified: str
    start: int
    end: int


@dataclass
class SourceModel:
    src: str
    toks: list[Tok]
    invocations: list[Invocation]
    members: list[Member]
    line_starts: list[int] = field(default_factory=list)

    @property
    def callee_names(self) -> set[str]:
        return {inv.name for inv in self.invocations}


def _match_brackets(toks: list[Tok]) -> dict[int, int]:
    pairs: dict[int, int] = {}
    stack: list[int] = []
    opens, closes = {"(": ")", "[": "]", "{": "}"}, {")", "]", "}"}
    for i, t in enumerate(toks):
        if t.kind != "punct":
            continue
        if t.text in opens:
            stack.append(i)
        elif t.text in closes:
            if not stack or opens[toks[stack[-1]].text] != t.text:
                raise TriageError(f"unbalanced '{t.text}' at offset {t.start}")
            pairs[stack.pop()] = i
    if stack:
        raise TriageError(f"unclosed '{toks[stack[-1]].text}' at offset {toks[stack[-1]].start}")
    return pairs


def _skip_generic_back(toks: list[Tok], i: int) -> int:
    """``toks[i]`` is ``>``: return the index of its ``<`` (or ``i`` when it is not a generic list)."""
    depth = 0
    for j in range(i, -1, -1):
        t = toks[j]
        if t.kind == "punct" and t.text == ">":
            depth += 1
        elif t.kind == "punct" and t.text == "<":
            depth -= 1
            if depth == 0:
                return j
        elif t.kind == "punct" and t.text in (";", "{", "}", "(", ")", "=>", "&&", "||"):
            return i
    return i


def _invocations(toks: list[Tok], pairs: dict[int, int]) -> list[Invocation]:
    out: list[Invocation] = []
    for i, t in enumerate(toks):
        if not (t.kind == "punct" and t.text == "(") or i == 0:
            continue
        j = i - 1
        if toks[j].kind == "punct" and toks[j].text == ">":
            k = _skip_generic_back(toks, j)
            if k == j or k == 0:
                continue
            j = k - 1
        name_tok = toks[j]
        if name_tok.kind != "id" or name_tok.text in _NOT_CALLEES:
            continue
        close = pairs[i]
        # A declaration (method, constructor, local function) is a type-ish token before the name and a
        # body / expression body / constraint / ctor initializer / ';' after the parameter list.
        prev = toks[j - 1] if j > 0 else None
        after = toks[close + 1] if close + 1 < len(toks) else None
        typeish_prev = prev is not None and (
            (prev.kind == "id" and prev.text not in _EXPR_PREFIX_KEYWORDS)
            or (prev.kind == "punct" and prev.text in ("?", "]", ">")))
        bodyish_after = after is not None and (
            (after.kind == "punct" and after.text in ("{", "=>", ";"))
            or (after.kind == "id" and after.text == "where")
            or (after.kind == "punct" and after.text == ":" and close + 2 < len(toks)
                and toks[close + 2].text in ("base", "this")))
        if typeish_prev and bodyish_after:
            continue
        out.append(Invocation(name_tok.text, i, close, t.end, toks[close].start))
    return out


def _members(toks: list[Tok], pairs: dict[int, int], lo: int, hi: int, prefix: str, out: list[Member]) -> None:
    """Members of the container whose body tokens are ``toks[lo:hi]`` (exclusive of its braces)."""
    i = lo
    while i < hi:
        t = toks[i]
        if t.kind == "punct" and t.text == ";":
            i += 1
            continue
        # attributes
        while i < hi and toks[i].kind == "punct" and toks[i].text == "[":
            i = pairs[i] + 1
        if i >= hi:
            break
        start_idx = i
        header_ids: list[Tok] = []
        name: Optional[str] = None
        kind = "member"
        end_idx = None
        j = i
        while j < hi:
            u = toks[j]
            if u.kind == "id":
                header_ids.append(u)
                if u.text in _TYPE_KEYWORDS:
                    kind = "container"
                elif u.text == "enum":
                    kind = "enum"
            if u.kind == "punct":
                if u.text == "(" and name is None and kind == "member":
                    # `Name(` or `Name<T>(` is a parameter list; any other `(` is a tuple type
                    # (`List<(int, int)> Of(…)`, `public (int, int) F()`) and names nothing.
                    k = j - 1
                    if toks[k].kind == "punct" and toks[k].text == ">":
                        k = _skip_generic_back(toks, k) - 1
                    if k >= start_idx and toks[k].kind == "id" and toks[k].text not in _MODIFIERS:
                        name = toks[k].text
                    j = pairs[j] + 1
                    continue
                if u.text in ("=", "=>"):
                    name = name or (header_ids[-1].text if header_ids else "?")
                    while j < hi and not (toks[j].kind == "punct" and toks[j].text == ";"):
                        j = pairs[j] + 1 if toks[j].kind == "punct" and toks[j].text in "([{" else j + 1
                    end_idx = j
                    break
                if u.text == ";":
                    name = name or (header_ids[-1].text if header_ids else "?")
                    end_idx = j
                    break
                if u.text == "{":
                    close = pairs[j]
                    if kind == "container":
                        names = [h.text for h in header_ids]
                        kw = next(k for k, h in enumerate(names) if h in _TYPE_KEYWORDS)
                        cname = names[kw + 1] if kw + 1 < len(names) else "?"
                        if names[kw] == "record" and cname in ("struct", "class") and kw + 2 < len(names):
                            cname = names[kw + 2]
                        inner_prefix = prefix if names[kw] == "namespace" else f"{prefix}{cname}."
                        _members(toks, pairs, j + 1, close, inner_prefix, out)
                        end_idx = None
                        i = close + 1
                        break
                    name = name or (header_ids[-1].text if header_ids else "?")
                    if kind == "enum":
                        names = [h.text for h in header_ids]
                        name = names[names.index("enum") + 1] if "enum" in names[:-1] else name
                    end_idx = close
                    # property with an initializer: `{ get; set; } = value;`
                    if close + 1 < hi and toks[close + 1].kind == "punct" and toks[close + 1].text == "=":
                        k = close + 1
                        while k < hi and not (toks[k].kind == "punct" and toks[k].text == ";"):
                            k = pairs[k] + 1 if toks[k].kind == "punct" and toks[k].text in "([{" else k + 1
                        end_idx = k
                    break
                if u.text in "([" and u.text in "([{":
                    j = pairs[j] + 1
                    continue
            j += 1
        else:
            # ran off the container (file-scoped namespace `namespace X;` handled by ';' above)
            end_idx = hi - 1
        if end_idx is None:
            continue
        if kind == "container" and end_idx is not None and name is None:
            name = "?"
        end_idx = min(end_idx, len(toks) - 1)
        out.append(Member(name or "?", f"{prefix}{name or '?'}", toks[start_idx].start, toks[end_idx].end))
        i = end_idx + 1


_LINE_BREAK = re.compile(r"\r\n|\r|\n| | |\u0085")


def analyze(src: str) -> SourceModel:
    toks = tokenize(src)
    pairs = _match_brackets(toks)
    members: list[Member] = []
    _members(toks, pairs, 0, len(toks), "", members)
    starts = [0] + [m.end() for m in _LINE_BREAK.finditer(src)]
    return SourceModel(src, toks, _invocations(toks, pairs), members, starts)


def offset_of(model: SourceModel, line: int, column: int, what: str) -> int:
    """1-based (line, UTF-16 column) -> string offset."""
    if line < 1 or line > len(model.line_starts):
        raise TriageError(f"{what}: line {line} is outside the source ({len(model.line_starts)} lines)")
    i = model.line_starts[line - 1]
    units = 0
    src = model.src
    while units < column - 1:
        if i >= len(src):
            raise TriageError(f"{what}: column {column} runs past the end of the source")
        units += 2 if ord(src[i]) > 0xFFFF else 1
        i += 1
    return i


# --------------------------------------------------------------------------- #
# ignore list
# --------------------------------------------------------------------------- #
def _args(model: SourceModel, inv: Invocation) -> list[list[Tok]]:
    """The invocation's arguments as token lists split at depth-0 commas."""
    toks = model.toks
    args: list[list[Tok]] = [[]]
    depth = 0
    for t in toks[inv.open_idx + 1:inv.close_idx]:
        if t.kind == "punct" and t.text in "([{":
            depth += 1
        elif t.kind == "punct" and t.text in ")]}":
            depth -= 1
        if depth == 0 and t.kind == "punct" and t.text == ",":
            args.append([])
            continue
        args[-1].append(t)
    return args


def _operator_value(model: SourceModel, inv: Invocation, start: int, end: int) -> bool:
    """``operator Value``: the mutant IS the ``value`` argument (index 1) of the call, that argument is a
    single non-interpolated string literal spelling an operator or delimiter (non-empty, punctuation
    only), and the token type (index 0) is a ``TokenType`` member outside the f-string family."""
    args = _args(model, inv)
    if len(args) < 2 or len(args[1]) != 1:
        return False
    lit = args[1][0]
    if lit.kind != "str" or lit.interpolated or not lit.value:
        return False
    if not (lit.start <= start and end <= lit.end):
        return False
    if any(ch not in string.punctuation for ch in lit.value):
        return False
    type_text = "".join(t.text for t in args[0])
    return re.fullmatch(r"TokenType\.(?!FString)\w+", type_text) is not None


FIELD_PREDICATES: dict[str, Callable[[SourceModel, Invocation, int, int], bool]] = {
    "operator Value": _operator_value,
}


@dataclass
class IgnoreEntry:
    mutator: str
    within: str
    reason: str
    field_of: Optional[str]
    regex: re.Pattern


def _top_level_alternatives(pattern: str) -> list[str]:
    alts, depth, cur, i = [], 0, [], 0
    while i < len(pattern):
        ch = pattern[i]
        if ch == "\\" and i + 1 < len(pattern):
            cur.append(pattern[i:i + 2])
            i += 2
            continue
        if ch in "([":
            depth += 1
        elif ch in ")]":
            depth -= 1
        if ch == "|" and depth == 0:
            alts.append("".join(cur))
            cur = []
        else:
            cur.append(ch)
        i += 1
    alts.append("".join(cur))
    return alts


def load_ignore(data: dict, universe: set[str]) -> list[IgnoreEntry]:
    entries = data.get("entries") if isinstance(data, dict) else None
    if not isinstance(entries, list):
        raise TriageError("ignore list: expected an object with an 'entries' array")
    out = []
    for n, e in enumerate(entries):
        where = f"ignore entry {n}"
        if not isinstance(e, dict):
            raise TriageError(f"{where}: not an object")
        unknown = set(e) - {"mutator", "within", "fieldOf", "reason"}
        if unknown:
            raise TriageError(f"{where}: unknown keys {sorted(unknown)}")
        for key in ("mutator", "within", "reason"):
            if not isinstance(e.get(key), str) or not e[key].strip():
                raise TriageError(f"{where}: '{key}' must be a non-empty string")
        if e["mutator"] not in KNOWN_MUTATORS:
            raise TriageError(f"{where}: mutator '{e['mutator']}' is not a Stryker identifier {sorted(KNOWN_MUTATORS)}")
        field_of = e.get("fieldOf")
        if field_of is not None and field_of not in FIELD_PREDICATES:
            raise TriageError(f"{where}: fieldOf '{field_of}' is not one of {sorted(FIELD_PREDICATES)}")
        try:
            regex = re.compile(e["within"])
        except re.error as ex:
            raise TriageError(f"{where}: within is not a regex: {ex}") from ex
        for alt in _top_level_alternatives(e["within"]):
            alt_re = re.compile(alt)
            if not any(alt_re.fullmatch(name) for name in universe):
                raise TriageError(
                    f"{where}: within alternative '{alt}' names no method invoked in any source "
                    f"({len(universe)} callee names scanned) — the entry would match nothing")
        out.append(IgnoreEntry(e["mutator"], e["within"], e["reason"], field_of, regex))
    return out


# --------------------------------------------------------------------------- #
# classes
# --------------------------------------------------------------------------- #
_C1 = frozenset({"ReadNumber", "ReadHexNumber", "ReadBinaryNumber", "ReadOctalNumber", "ReadPrefixedIntegerSuffix"})
_C3 = frozenset({"NextToken", "ConsumeLineBreak", "IsPreCloseNewline"})
_C7 = frozenset({"Keywords", "_keywordNames"})
CLASS_NAMES = ("C1", "C2", "C3", "C4", "C5", "C6", "C7", "unclassified")


def classify(file_name: str, member: str, mutator: str, original: str) -> str:
    if member in _C7:
        return "C7"
    if mutator != "block" and re.search(r"\b(_column|[Ss]ourceLength)\b", original):
        return "C2"
    if mutator == "equality" and re.search(r"[<>]=?\s*[\w.]*\bLength\b", original):
        return "C6"
    if member in _C1:
        return "C1"
    if member in _C3 or "HoleTrivia" in member:
        return "C3"
    if file_name == "Lexer.LiteralLoss.cs":
        return "C5"
    if file_name == "Lexer.FStrings.cs":
        return "C4"
    return "unclassified"


# --------------------------------------------------------------------------- #
# triage
# --------------------------------------------------------------------------- #
def _cs_files(dirs: list[str]) -> list[Path]:
    files: list[Path] = []
    for d in dirs:
        p = Path(d)
        if not p.is_dir():
            raise TriageError(f"--universe {d}: not a directory")
        files.extend(sorted(p.rglob("*.cs")))
    return files


def _display_names(paths: list[str]) -> dict[str, str]:
    if len(paths) <= 1:
        return {p: Path(p).name for p in paths}
    base = os.path.commonpath([str(Path(p).parent) for p in paths]) if paths else ""
    return {p: os.path.relpath(p, base) if base else p for p in paths}


def triage(report: dict, ignore: dict, universe_dirs: Optional[list[str]] = None, with_classes: bool = False) -> dict:
    files = report.get("files") if isinstance(report, dict) else None
    if not isinstance(files, dict) or not files:
        raise TriageError("report: no 'files' object (is this a mutation-testing-elements report?)")

    models: dict[str, SourceModel] = {}
    for path, entry in files.items():
        src = entry.get("source")
        if src is None:
            try:
                src = Path(path).read_text(encoding="utf-8-sig")
            except OSError as ex:
                raise TriageError(f"report: '{path}' has no source and cannot be read: {ex}") from ex
        try:
            models[path] = analyze(src)
        except TriageError as ex:
            raise TriageError(f"{path}: {ex}") from ex

    universe: set[str] = set()
    for m in models.values():
        universe |= m.callee_names
    for f in _cs_files(universe_dirs or []):
        universe |= analyze(f.read_text(encoding="utf-8-sig")).callee_names
    entries = load_ignore(ignore, universe)

    names = _display_names(list(files))
    raw = {s: 0 for s in _STATUS.values()}
    listed = {s: 0 for s in SCORED}
    buckets: dict[str, int] = {}
    classes = {c: 0 for c in CLASS_NAMES}
    class_mutants: dict[str, list[dict]] = {c: [] for c in CLASS_NAMES}
    by_entry = [0] * len(entries)
    mutants_out: list[dict] = []

    for path, entry in files.items():
        model, fname = models[path], names[path]
        for mut in entry.get("mutants", []):
            mid = mut.get("id", "?")
            what = f"{fname} mutant {mid}"
            status = _STATUS.get(mut.get("status"))
            if status is None:
                raise TriageError(f"{what}: unknown status {mut.get('status')!r}")
            raw[status] += 1
            try:
                loc = mut["location"]
                start = offset_of(model, int(loc["start"]["line"]), int(loc["start"]["column"]), what)
                end = offset_of(model, int(loc["end"]["line"]), int(loc["end"]["column"]), what)
            except (KeyError, TypeError, ValueError) as ex:
                raise TriageError(f"{what}: malformed location: {ex}") from ex
            mut_id = mutator_id(str(mut.get("mutatorName", "")))
            enclosing = sorted(
                (inv for inv in model.invocations if inv.args_start <= start and end <= inv.args_end),
                key=lambda inv: inv.args_end - inv.args_start)
            member_hits = sorted((mb for mb in model.members if mb.start <= start and end <= mb.end),
                                 key=lambda mb: mb.end - mb.start)
            member = member_hits[0].name if member_hits else "-"
            bucket_status = status
            if status in SCORED:
                for n, ig in enumerate(entries):
                    if ig.mutator != mut_id:
                        continue
                    hit = next((inv for inv in enclosing if ig.regex.fullmatch(inv.name)), None)
                    if hit is None:
                        continue
                    if ig.field_of and not FIELD_PREDICATES[ig.field_of](model, hit, start, end):
                        continue
                    bucket_status = "ignoredByList"
                    listed[status] += 1
                    by_entry[n] += 1
                    break
            invocation = enclosing[0].name if enclosing else "-"
            key = f"{fname}|{mut_id}|{invocation}|{bucket_status}"
            buckets[key] = buckets.get(key, 0) + 1
            row = {"id": mid, "file": fname, "line": loc["start"]["line"], "column": loc["start"]["column"],
                   "mutator": mut_id, "member": member, "invocation": invocation, "status": bucket_status}
            if bucket_status in UNDETECTED:
                cls = classify(Path(fname).name, member, mut_id, model.src[start:end])
                classes[cls] += 1
                row["class"] = cls
                if with_classes:
                    class_mutants[cls].append({k: row[k] for k in ("id", "file", "line", "column", "mutator", "member", "status")})
            mutants_out.append(row)

    total = sum(raw.values())
    after = {s: raw[s] - listed[s] for s in SCORED}
    result = {
        "score": score_of(raw),
        "scoreIgnoringListed": score_of(after),
        "total": total,
        "killed": raw["killed"],
        "survived": raw["survived"],
        "timeout": raw["timeout"],
        "noCoverage": raw["noCoverage"],
        "compileError": raw["compileError"],
        "runtimeError": raw["runtimeError"],
        "pending": raw["pending"],
        "ignoredByStryker": raw["ignoredByStryker"],
        "ignoredByList": sum(listed.values()),
        "afterList": after,
        "listedByEntry": [{"within": e.within, "fieldOf": e.field_of, "matched": by_entry[n]}
                          for n, e in enumerate(entries)],
        "buckets": dict(sorted(buckets.items())),
        "classes": classes,
        "mutants": mutants_out,
    }
    if with_classes:
        result["classMutants"] = class_mutants
    return result


def main(argv: Optional[list[str]] = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--report", required=True, help="Stryker mutation-report.json")
    ap.add_argument("--ignore", required=True, help="ignore list (e.g. build_tools/stryker/lexer-ignore.json)")
    ap.add_argument("--classes", action="store_true", help="also list the undetected mutants of each class")
    ap.add_argument("--universe", action="append", default=[],
                    help="extra directory whose *.cs files count as sources for the within-name check")
    args = ap.parse_args(argv)
    try:
        report = json.loads(Path(args.report).read_text(encoding="utf-8-sig"))
        ignore = json.loads(Path(args.ignore).read_text(encoding="utf-8-sig"))
        result = triage(report, ignore, args.universe, args.classes)
    except (OSError, json.JSONDecodeError, TriageError) as ex:
        print(f"triage: error: {ex}", file=sys.stderr)
        return 2
    result.pop("mutants")
    json.dump(result, sys.stdout, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
