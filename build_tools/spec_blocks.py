#!/usr/bin/env python3
"""Spec-block sweep: every fenced Sharpy block in docs/language_specification/ must
compile, carry a marker, or be in the allowlist.

docs/design/ is excluded — its ```python fences are plain Python, not Sharpy.
"""

import argparse
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
SPY_CODE_PATTERN = re.compile(r'SPY\d{4}')


@dataclass
class Block:
    relpath: str
    line: int
    text: str
    key: str
    marker: Optional[str] = None  # None, "fragment", or "error SPYnnnn"
    expected_error: Optional[str] = None


@dataclass
class CompileResult:
    success: bool
    first_error_code: Optional[str] = None
    stderr: str = ""
    stdout: str = ""


@dataclass
class BlockResult:
    block: Block
    classification: str  # compiled_as_is, compiled_wrapped, fragment, error_asserted, allowlisted, failing
    compile_result: Optional[CompileResult] = None
    wrapped: bool = False
    red: bool = False
    red_reason: str = ""


def block_key(relpath: str, text: str) -> str:
    sha = hashlib.sha1(text.encode()).hexdigest()[:10]
    return f"{relpath}::{sha}"


def extract_blocks(spec_dir: str) -> List[Block]:
    blocks = []
    spec_path = Path(spec_dir)
    for md_file in sorted(spec_path.rglob("*.md")):
        relpath = str(md_file.relative_to(spec_path))
        lines = md_file.read_text(encoding="utf-8").splitlines()
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
                mf = MARKER_FRAGMENT.search(prev_line)
                me = MARKER_ERROR.search(prev_line)
                if mf:
                    marker = "fragment"
                elif me:
                    marker = "error"
                    expected_error = me.group(1)

                blocks.append(Block(
                    relpath=relpath,
                    line=fence_line,
                    text=text,
                    key=key,
                    marker=marker,
                    expected_error=expected_error,
                ))
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


def _compile_block(args: Tuple[str, Block]) -> BlockResult:
    sharpyc, block = args
    if block.marker == "fragment":
        return BlockResult(block=block, classification="fragment")

    result = compile_one(sharpyc, block.text)

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
            wrapped_result = compile_one(sharpyc, wrap_in_main(block.text))
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

    if block.marker == "error":
        if br.classification in ("compiled_as_is", "compiled_wrapped"):
            br.red = True
            br.red_reason = f"error marker {block.expected_error} but block compiles"
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
        "error-asserted": 0, "allowlisted": 0, "failing": 0,
    }
    label_map = {
        "compiled_as_is": "compiled-as-is",
        "compiled_wrapped": "wrapped",
        "fragment": "fragment",
        "error_asserted": "error-asserted",
        "allowlisted": "allowlisted",
        "failing": "failing",
    }
    for r in results:
        key = label_map.get(r.classification, r.classification)
        counts[key] = counts.get(key, 0) + 1
    parts = [f"{k} {v}" for k, v in counts.items()]
    return " / ".join(parts)


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
    parser.add_argument("--report", action="store_true", dest="report_only",
                        help="Print class counts")
    parser.add_argument("--allowlist", default=None,
                        help="Path to allowlist file (default: build_tools/spec_blocks_allowlist.txt)")

    args = parser.parse_args()

    sharpyc = args.sharpyc or os.environ.get("SHARPYC") or "dotnet run --project src/Sharpy.Cli --"
    jobs = args.jobs or os.cpu_count() or 4
    script_dir = os.path.dirname(os.path.abspath(__file__))
    allowlist_path = args.allowlist or os.path.join(script_dir, "spec_blocks_allowlist.txt")

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

    if args.report_only:
        print(report(results))

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
