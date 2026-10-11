#!/usr/bin/env python3
"""Spec-block sweep: every fenced Sharpy block in docs/language_specification/ must
compile, carry a marker, or be in the allowlist.

docs/design/ is excluded — its ```python fences are plain Python, not Sharpy.

Markers sit on the line directly above a block's opening fence:

- ``<!-- spec-sweep: fragment -->`` — not a program; not compiled.
- ``<!-- spec-sweep: error SPYnnnn -->`` — must fail, and its first error must be SPYnnnn.
- ``<!-- spec-sweep: prelude -->`` — compiles, and its declarations are prepended to every later
  unmarked or output block in the same file.
- ``<!-- spec-sweep: output -->`` / ``<!-- spec-sweep: output "…" -->`` (#2286) — the block is RUN
  with ``sharpyc run`` (not emitted: SPY0908 shows only under run) and its stdout must equal the
  expected output, given by exactly one of three forms:

  1. inline — the marker's JSON string (``"3"``, ``"a\\nb"`` for two lines);
  2. fenced — an ```output (or ```text) block directly after the program, blank lines allowed between;
  3. comment — neither of the above: each ``print(...)  # value`` line in the block is one stdout
     line, in source order, and every ``print(`` line must carry a comment. A comment states the
     printed text verbatim: no ``Output:`` prefix, no quotes around a printed string.

  Comparison (stated once, here): CRLF is LF, trailing whitespace on each line is dropped, and
  trailing empty lines are dropped; everything else must match exactly.
"""

import argparse
import difflib
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
from concurrent.futures import ProcessPoolExecutor, as_completed
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Tuple

FENCE_PATTERN = re.compile(r'^```(python|spy|sharpy)\s*$')
FENCE_CLOSE = re.compile(r'^```\s*$')
MARKER_FRAGMENT = re.compile(r'<!--\s*spec-sweep:\s*fragment\s*-->')
MARKER_ERROR = re.compile(r'<!--\s*spec-sweep:\s*error\s+(SPY\d{4})\s*-->')
# #1939: a prelude block declares module-level names (types, functions) for every LATER unmarked
# block in the same file. The prelude's text is prepended to those blocks at compile time, but each
# block's allowlist KEY stays the sha1 of its OWN text, so adding a prelude does not churn keys. The
# prelude block itself is compiled standalone (with any earlier preludes) — it must compile, which is
# the positive control that a prelude cannot hide an error in the code it declares.
MARKER_PRELUDE = re.compile(r'<!--\s*spec-sweep:\s*prelude\s*-->')
MARKER_OUTPUT = re.compile(r'<!--\s*spec-sweep:\s*output(?:\s+("(?:[^"\\]|\\.)*"))?\s*-->')
OUTPUT_FENCE = re.compile(r'^```(output|text)\s*$')
PRINT_LINE = re.compile(r'^\s*print\(')
# A print with a trailing comment: the text after `#` (and one space) is the expected line. The call
# ends at the first `)` followed by two or more spaces and `#` — PEP 8's inline-comment spelling,
# which every spec example uses — so a `#` inside the printed string literal is not a comment.
PRINT_COMMENT = re.compile(r'^\s*print\(.*?\)\s{2,}#\s?(.*)$')
SPY_CODE_PATTERN = re.compile(r'SPY\d{4}')
RUN_BANNER = "=== Running Program ==="


@dataclass
class Block:
    relpath: str
    line: int
    text: str
    key: str
    marker: Optional[str] = None  # None, "fragment", "error", or "output"
    expected_error: Optional[str] = None
    prelude: Optional[str] = None  # module-level declarations prepended at compile time (#1939)
    expected_output: Optional[str] = None  # output marker: the stdout the run must print (#2286)
    output_form: Optional[str] = None  # output marker: "inline", "fenced", or "comment"
    output_error: Optional[str] = None  # output marker that states no usable expectation


@dataclass
class CompileResult:
    success: bool
    first_error_code: Optional[str] = None
    stderr: str = ""
    stdout: str = ""


@dataclass
class RunResult:
    ran: bool  # the program started (the run banner was printed)
    output: str = ""  # the program's stdout, banner stripped
    exit_code: int = 0
    stderr: str = ""


@dataclass
class BlockResult:
    block: Block
    # compiled_as_is, compiled_wrapped, fragment, error_asserted, allowlisted, failing,
    # output_asserted, output_mismatch
    classification: str
    compile_result: Optional[CompileResult] = None
    wrapped: bool = False
    red: bool = False
    red_reason: str = ""
    run_result: Optional[RunResult] = None


def block_key(relpath: str, text: str) -> str:
    sha = hashlib.sha1(text.encode()).hexdigest()[:10]
    return f"{relpath}::{sha}"


def _comment_form_output(text: str) -> Tuple[Optional[str], Optional[str]]:
    """The comment form: each `print(...)  # value` line is one expected stdout line. Returns
    (expected, None) or (None, why the block states no usable expectation)."""
    expected = []
    for n, line in enumerate(text.splitlines(), start=1):
        if not PRINT_LINE.match(line):
            continue
        m = PRINT_COMMENT.match(line)
        if not m:
            return None, f"output marker (comment form) but block line {n} prints without a `  # value` comment"
        expected.append(m.group(1).rstrip())
    if not expected:
        return None, "output marker but the block has no `print(...)  # value` line, inline string or ```output block"
    return "\n".join(expected), None


def normalize_output(text: str) -> List[str]:
    """The one comparison rule (module docstring): CRLF is LF, trailing whitespace on each line and
    trailing empty lines are dropped."""
    lines = [line.rstrip() for line in text.replace("\r\n", "\n").split("\n")]
    while lines and not lines[-1]:
        lines.pop()
    return lines


def extract_blocks(spec_dir: str) -> List[Block]:
    blocks = []
    spec_path = Path(spec_dir)
    for md_file in sorted(spec_path.rglob("*.md")):
        relpath = str(md_file.relative_to(spec_path))
        lines = md_file.read_text(encoding="utf-8").splitlines()
        # Preludes accumulate down the file — a later unmarked block gets every prelude declared
        # above it, joined in order. Reset per file so a prelude never leaks across pages.
        prelude_parts: List[str] = []
        i = 0
        while i < len(lines):
            m = FENCE_PATTERN.match(lines[i])
            if m:
                fence_line = i + 1  # 1-based
                prev_line = lines[i - 1] if i > 0 else ""
                block_lines = []
                i += 1
                while i < len(lines) and not FENCE_CLOSE.match(lines[i]):
                    block_lines.append(lines[i])
                    i += 1
                text = "\n".join(block_lines)
                key = block_key(relpath, text)

                marker = None
                expected_error = None
                expected_output = None
                output_form = None
                output_error = None
                mf = MARKER_FRAGMENT.search(prev_line)
                me = MARKER_ERROR.search(prev_line)
                mo = MARKER_OUTPUT.search(prev_line)
                is_prelude = bool(MARKER_PRELUDE.search(prev_line))
                if mf:
                    marker = "fragment"
                elif me:
                    marker = "error"
                    expected_error = me.group(1)
                elif mo:
                    marker = "output"
                    j = i + 1
                    while j < len(lines) and not lines[j].strip():
                        j += 1
                    follows = j < len(lines) and OUTPUT_FENCE.match(lines[j])
                    if mo.group(1) is not None and follows:
                        output_error = "output marker has both an inline string and a following ```output block"
                    elif mo.group(1) is not None:
                        output_form = "inline"
                        try:
                            expected_output = json.loads(mo.group(1))
                        except json.JSONDecodeError as e:
                            output_error = f"output marker's inline string is not a JSON string ({e.msg})"
                    elif follows:
                        output_form = "fenced"
                        out_lines = []
                        j += 1
                        while j < len(lines) and not FENCE_CLOSE.match(lines[j]):
                            out_lines.append(lines[j])
                            j += 1
                        expected_output = "\n".join(out_lines)
                    else:
                        output_form = "comment"
                        expected_output, output_error = _comment_form_output(text)

                # An unmarked block (including a prelude block itself) and an output block are compiled
                # with every prelude declared above them prepended. A fragment/error block is not — its
                # marker owns its verdict and a prelude must not alter it.
                takes_prelude = marker is None or marker == "output"
                prelude = "\n\n".join(prelude_parts) if (prelude_parts and takes_prelude) else None

                blocks.append(Block(
                    relpath=relpath,
                    line=fence_line,
                    text=text,
                    key=key,
                    marker=marker,
                    expected_error=expected_error,
                    prelude=prelude,
                    expected_output=expected_output,
                    output_form=output_form,
                    output_error=output_error,
                ))

                # Register this prelude's declarations for the blocks that follow it.
                if is_prelude:
                    prelude_parts.append(text)
            i += 1
    return blocks


def compile_one(sharpyc: str, source: str) -> CompileResult:
    with tempfile.NamedTemporaryFile(suffix=".spy", mode="w", delete=False, encoding="utf-8") as f:
        f.write(source)
        tmp = f.name
    try:
        proc = subprocess.run(
            sharpyc.split() + ["emit", "diagnostics", "--format", "json", tmp],
            capture_output=True, text=True, timeout=60,
        )
        first_error = None
        try:
            diags = json.loads(proc.stdout) if proc.stdout.strip() else []
            for d in diags:
                if d.get("severity") == "error":
                    first_error = d.get("code")
                    break
        except json.JSONDecodeError:
            pass

        if first_error is None and proc.returncode != 0:
            for line in (proc.stderr + proc.stdout).splitlines():
                m = SPY_CODE_PATTERN.search(line)
                if m:
                    first_error = m.group(0)
                    break

        return CompileResult(
            success=(proc.returncode == 0),
            first_error_code=first_error,
            stderr=proc.stderr,
            stdout=proc.stdout,
        )
    except subprocess.TimeoutExpired:
        return CompileResult(success=False, first_error_code=None, stderr="timeout")
    finally:
        os.unlink(tmp)


def run_one(sharpyc: str, source: str) -> RunResult:
    """`sharpyc run` the source; the program's stdout is what follows the run banner and the one
    blank line printed after it (RunCommand)."""
    with tempfile.NamedTemporaryFile(suffix=".spy", mode="w", delete=False, encoding="utf-8") as f:
        f.write(source)
        tmp = f.name
    try:
        proc = subprocess.run(
            sharpyc.split() + ["run", tmp],
            capture_output=True, text=True, timeout=120,
        )
        lines = proc.stdout.split("\n")
        if RUN_BANNER not in lines:
            return RunResult(ran=False, exit_code=proc.returncode, stderr=proc.stderr + proc.stdout)
        rest = lines[lines.index(RUN_BANNER) + 1:]
        if rest and rest[0] == "":
            rest = rest[1:]
        return RunResult(ran=True, output="\n".join(rest), exit_code=proc.returncode, stderr=proc.stderr)
    except subprocess.TimeoutExpired:
        return RunResult(ran=False, exit_code=-1, stderr="timeout")
    finally:
        os.unlink(tmp)


def wrap_in_main(source: str) -> str:
    indented = "\n".join("    " + line if line.strip() else line for line in source.splitlines())
    return f"def main() -> None:\n{indented}\n"


def load_allowlist(path: str) -> Dict[str, str]:
    entries = {}
    if not os.path.exists(path):
        return entries
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            parts = line.split("#", 1)
            key = parts[0].strip()
            comment = parts[1].strip() if len(parts) > 1 else ""
            entries[key] = comment
    return entries


def save_allowlist(path: str, entries: Dict[str, str]) -> None:
    with open(path, "w", encoding="utf-8") as f:
        f.write("# spec_blocks_allowlist.txt — blocks that fail to compile.\n")
        f.write("# Each entry: <relpath>::<sha1_10hex>  # SPYnnnn first-error-code\n")
        f.write("# Ratchet: entries must trend to empty; stale entries fail.\n\n")
        for key in sorted(entries):
            f.write(f"{key}  # {entries[key]}\n")


def _with_prelude(prelude: Optional[str], body: str) -> str:
    """Prepend the prelude's module-level declarations to a block body (#1939). Only the body is
    ever wrapped in main() — the prelude stays at module level."""
    return f"{prelude}\n\n{body}" if prelude else body


def _compile_block(args: Tuple[str, Block]) -> BlockResult:
    br = _compile_block_only(args)
    sharpyc, block = args
    # An output block that compiled is RUN in the shape that compiled (as-is or wrapped in main).
    if block.marker == "output" and block.output_error is None \
            and br.classification in ("compiled_as_is", "compiled_wrapped"):
        body = wrap_in_main(block.text) if br.wrapped else block.text
        br.run_result = run_one(sharpyc, _with_prelude(block.prelude, body))
    return br


def _compile_block_only(args: Tuple[str, Block]) -> BlockResult:
    sharpyc, block = args
    if block.marker == "fragment":
        return BlockResult(block=block, classification="fragment")

    result = compile_one(sharpyc, _with_prelude(block.prelude, block.text))

    if result.success:
        return BlockResult(block=block, classification="compiled_as_is", compile_result=result)

    if result.first_error_code == "SPY0340":
        all_spy0340 = True
        try:
            diags = json.loads(result.stdout) if result.stdout.strip() else []
            for d in diags:
                if d.get("severity") == "error" and d.get("code") != "SPY0340":
                    all_spy0340 = False
                    break
        except json.JSONDecodeError:
            all_spy0340 = False

        if all_spy0340:
            wrapped_result = compile_one(sharpyc, _with_prelude(block.prelude, wrap_in_main(block.text)))
            if wrapped_result.success:
                return BlockResult(
                    block=block, classification="compiled_wrapped",
                    compile_result=wrapped_result, wrapped=True,
                )
            result = wrapped_result

    return BlockResult(block=block, classification="failing", compile_result=result)


def _classify_result(
    br: BlockResult,
    allowlist: Dict[str, str],
    used_allowlist_keys: set,
    reds: List[str],
) -> None:
    block = br.block

    if block.marker == "fragment":
        return

    if block.marker == "output":
        _classify_output(br, allowlist, used_allowlist_keys, reds)
        return

    if block.marker == "error":
        if br.classification in ("compiled_as_is", "compiled_wrapped"):
            br.red = True
            br.red_reason = f"error marker {block.expected_error} but block compiles"
            br.classification = "failing"
            reds.append(f"RED: {block.relpath}:{block.line} — {br.red_reason}")
            return
        # The marker names a code, and the block must fail WITH it (#2026): an error marker
        # naming the wrong code used to pass as long as the block failed at all.
        actual = br.compile_result.first_error_code if br.compile_result else None
        if actual != block.expected_error:
            br.red = True
            br.red_reason = f"error marker {block.expected_error} but first error is {actual}"
            br.classification = "failing"
            reds.append(f"RED: {block.relpath}:{block.line} — {br.red_reason}")
        else:
            br.classification = "error_asserted"
        return

    if br.classification == "failing":
        if block.key in allowlist:
            br.classification = "allowlisted"
            used_allowlist_keys.add(block.key)
        else:
            br.red = True
            first_code = br.compile_result.first_error_code if br.compile_result else "unknown"
            br.red_reason = f"unmarked failing block (first error: {first_code})"
            reds.append(f"RED: {block.relpath}:{block.line} — {br.red_reason}")
    elif br.classification in ("compiled_as_is", "compiled_wrapped"):
        if block.key in allowlist:
            br.red = True
            br.red_reason = "allowlisted but now compiles (STALE)"
            reds.append(f"STALE: {block.relpath}:{block.line} — {br.red_reason}")
            used_allowlist_keys.add(block.key)


def _classify_output(
    br: BlockResult,
    allowlist: Dict[str, str],
    used_allowlist_keys: set,
    reds: List[str],
) -> None:
    """An output block's verdict is its marker's: it runs and prints the expected output, or it is
    red. The allowlist never answers for it."""
    block = br.block

    def red(reason: str) -> None:
        br.red = True
        br.red_reason = reason
        br.classification = "output_mismatch"
        reds.append(f"RED: {block.relpath}:{block.line} — {reason}")

    if block.key in allowlist:
        used_allowlist_keys.add(block.key)
        red("output-marked block is in the allowlist — the marker owns its verdict; drop the row")
        return
    if block.output_error:
        red(block.output_error)
        return
    if br.classification not in ("compiled_as_is", "compiled_wrapped"):
        first = br.compile_result.first_error_code if br.compile_result else None
        red(f"output marker but block does not compile (first error: {first})")
        return
    run = br.run_result
    if run is None or not run.ran:
        detail = (run.stderr.strip().splitlines() or ["no output"])[0] if run else "not run"
        red(f"output marker but the program did not run ({detail})")
        return
    if run.exit_code != 0:
        red(f"output marker but the program exited {run.exit_code}")
        return
    expected = normalize_output(block.expected_output or "")
    actual = normalize_output(run.output)
    if expected != actual:
        diff = "\n".join(difflib.unified_diff(expected, actual, "expected", "actual", lineterm=""))
        red(f"output mismatch ({block.output_form} form):\n{diff}")
        return
    br.classification = "output_asserted"


def _compile_blocks_sequential(sharpyc: str, blocks: List[Block]) -> List[BlockResult]:
    return [_compile_block((sharpyc, b)) for b in blocks]


def _compile_blocks_parallel(sharpyc: str, blocks: List[Block], jobs: int) -> List[BlockResult]:
    work = [(sharpyc, b) for b in blocks]
    compiled = []
    with ProcessPoolExecutor(max_workers=jobs) as pool:
        futures = {pool.submit(_compile_block, w): w[1] for w in work}
        for future in as_completed(futures):
            compiled.append(future.result())
    return compiled


def run_sweep(
    sharpyc: str,
    blocks: List[Block],
    allowlist: Dict[str, str],
    jobs: int,
) -> Tuple[List[BlockResult], List[str]]:
    reds: List[str] = []
    used_allowlist_keys: set = set()
    block_keys = {b.key for b in blocks}

    if jobs <= 1:
        compiled = _compile_blocks_sequential(sharpyc, blocks)
    else:
        try:
            compiled = _compile_blocks_parallel(sharpyc, blocks, jobs)
        except (PermissionError, NotImplementedError):
            compiled = _compile_blocks_sequential(sharpyc, blocks)

    for br in compiled:
        _classify_result(br, allowlist, used_allowlist_keys, reds)

    for akey in sorted(allowlist):
        if akey not in used_allowlist_keys and akey not in block_keys:
            reds.append(f"STALE: allowlist key {akey} — block no longer exists")

    return compiled, reds


def report(results: List[BlockResult]) -> str:
    counts = {
        "pool": len(results),
        "compiled-as-is": 0, "wrapped": 0, "fragment": 0,
        "error-asserted": 0, "output-asserted": 0, "output-mismatch": 0,
        "allowlisted": 0, "failing": 0,
    }
    label_map = {
        "compiled_as_is": "compiled-as-is",
        "compiled_wrapped": "wrapped",
        "fragment": "fragment",
        "error_asserted": "error-asserted",
        "output_asserted": "output-asserted",
        "output_mismatch": "output-mismatch",
        "allowlisted": "allowlisted",
        "failing": "failing",
    }
    for r in results:
        key = label_map.get(r.classification, r.classification)
        counts[key] = counts.get(key, 0) + 1
    parts = [f"{k} {v}" for k, v in counts.items()]
    return " / ".join(parts)


def files_without_output_assertions(blocks: List[Block]) -> List[str]:
    """Spec files with Sharpy blocks but no output-marked block. The absence of an output assertion
    is reported, never silent (#2286): a file leaves this list when one of its blocks asserts output."""
    with_blocks = {b.relpath for b in blocks}
    asserted = {b.relpath for b in blocks if b.marker == "output"}
    return sorted(with_blocks - asserted)


def seed_output_candidates(spec_dir: str) -> List[Tuple[str, int, str, str]]:
    """Blocks with no marker whose text or nearby prose states a result — the conversion worklist
    for R-BG (extended) and P20. Text-only: nothing is compiled. Each row is
    (relpath, fence line, form, excerpt); `comment` means every print carries a `  # value` comment,
    so the block converts by adding the marker once the comments are exact."""
    prose = re.compile(r'\b(prints|outputs|Output:|output is)\b|→')
    rows = []
    for block in extract_blocks(spec_dir):
        if block.marker is not None:
            continue
        lines = (Path(spec_dir) / block.relpath).read_text(encoding="utf-8").splitlines()
        body = block.text.split("\n") if block.text else []
        close = block.line + len(body)  # 0-based index of the closing fence
        prints = [l for l in body if PRINT_LINE.match(l)]
        commented = [l for l in prints if PRINT_COMMENT.match(l)]
        j = close + 1
        while j < len(lines) and not lines[j].strip():
            j += 1
        following = lines[j] if j < len(lines) else ""
        if prints and len(commented) == len(prints):
            form = "comment"
        elif commented:
            form = "comment-partial"
        elif re.match(r'^```(output|text)?\s*$', following):
            form = "fence-follows"
        elif any(re.search(r'#\s*(Output|Prints)\b', l, re.IGNORECASE) for l in body):
            form = "output-comment"
        elif prose.search(" ".join(lines[max(0, block.line - 4):block.line - 1] + lines[close + 1:close + 4])):
            form = "prose"
        else:
            continue
        excerpt = (commented or prints or body or [""])[0].strip()[:80]
        rows.append((block.relpath, block.line, form, excerpt))
    return rows


def main():
    parser = argparse.ArgumentParser(description="Spec-block sweep for Sharpy language specification")
    parser.add_argument("--spec-dir", default="docs/language_specification/",
                        help="Directory containing spec .md files")
    parser.add_argument("--sharpyc", default=None,
                        help="Path/command for sharpyc (default: SHARPYC env or dotnet run)")
    parser.add_argument("--jobs", type=int, default=None,
                        help="Parallel workers (default: cpu_count)")
    parser.add_argument("--seed", action="store_true",
                        help="Seed the allowlist from current failures")
    parser.add_argument("--seed-output", action="store_true",
                        help="List unmarked blocks that state a result (text only; no compiler)")
    parser.add_argument("--report", action="store_true", dest="report_only",
                        help="Print class counts")
    parser.add_argument("--allowlist", default=None,
                        help="Path to allowlist file (default: build_tools/spec_blocks_allowlist.txt)")

    args = parser.parse_args()

    sharpyc = args.sharpyc or os.environ.get("SHARPYC") or "dotnet run --project src/Sharpy.Cli --"
    jobs = args.jobs or os.cpu_count() or 4
    script_dir = os.path.dirname(os.path.abspath(__file__))
    allowlist_path = args.allowlist or os.path.join(script_dir, "spec_blocks_allowlist.txt")

    if args.seed_output:
        rows = seed_output_candidates(args.spec_dir)
        for relpath, line, form, excerpt in rows:
            print(f"{relpath}:{line}\t{form}\t{excerpt}")
        by_form: Dict[str, int] = {}
        for _, _, form, _ in rows:
            by_form[form] = by_form.get(form, 0) + 1
        summary = " / ".join(f"{k} {v}" for k, v in sorted(by_form.items()))
        print(f"{len(rows)} output-stating candidate(s): {summary}", file=sys.stderr)
        return

    blocks = extract_blocks(args.spec_dir)
    print(f"Extracted {len(blocks)} blocks from {args.spec_dir}", file=sys.stderr)

    if args.seed:
        seed_entries = {}
        with ProcessPoolExecutor(max_workers=jobs) as pool:
            futures = {pool.submit(_compile_block, (sharpyc, b)): b for b in blocks}
            done = 0
            for future in as_completed(futures):
                done += 1
                br = future.result()
                if done % 50 == 0:
                    print(f"  seed progress: {done}/{len(blocks)}", file=sys.stderr)
                if br.classification == "failing" and br.block.marker != "error":
                    first_code = br.compile_result.first_error_code if br.compile_result else "unknown"
                    seed_entries[br.block.key] = f"{first_code} first"
        save_allowlist(allowlist_path, seed_entries)
        print(f"Seeded {len(seed_entries)} entries to {allowlist_path}", file=sys.stderr)
        return

    allowlist = load_allowlist(allowlist_path)
    results, reds = run_sweep(sharpyc, blocks, allowlist, jobs)

    print(report(results), file=sys.stderr)
    without = files_without_output_assertions(blocks)
    files = len({b.relpath for b in blocks})
    print(f"no output assertions: {len(without)} of {files} files", file=sys.stderr)

    if args.report_only:
        print(report(results))
        print(f"no output assertions: {len(without)} of {files} files")
        for relpath in without:
            print(f"  {relpath}")

    if reds:
        print(f"\n{len(reds)} RED condition(s):", file=sys.stderr)
        for r in reds:
            print(f"  {r}", file=sys.stderr)
        sys.exit(1)
    else:
        print("Sweep clean.", file=sys.stderr)
        sys.exit(0)


if __name__ == "__main__":
    main()
