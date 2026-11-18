#!/usr/bin/env python3
"""Parse the `sharpy-issues` block of a pull request description: the issues it closes and files.

GitHub closes an issue when a commit or a default-branch PR description names it after a closing
keyword; that closes on a sentence, before any evidence. Instead (owner, 2026-11-18: "yes, option
1"), a PR description lists its issues in one fenced block, and `/close-issues pr:<N>` reads it
after the PR merges into `dev` and closes each issue on its usual evidence checks. No workflow
closes issues; CI only checks that the block parses (`.github/workflows/review-line.yml`).

The block:

    ```yaml
    sharpy-issues:
      closes: [2680]
      files: [2741, 2742]
    ```

Rules (each one a ValueError when broken):
  - a fence whose info string's first word is `yaml` (``` or ~~~, at most three spaces of indent;
    it closes on a bare fence of the same character at least as long, or at the end of the body)
    and whose text holds `sharpy-issues:` anywhere is THE block; other yaml fences are ignored, and
    so are fences with any other info string (an example of the block in a `text` fence is not one)
    and a fence inside an HTML comment that starts a line (`<!--` at most three spaces in);
  - at most one block per description;
  - the YAML (safe_load, no duplicate keys) is a mapping with the one key `sharpy-issues`, whose
    value is a mapping with keys from {closes, files}; a key may be omitted;
  - each list holds positive decimal integers written bare: `"#2680"` (a `#` lets GitHub link it),
    `012` (YAML 1.1 octal), `2_680`, `1:20` (sexagesimal), `0x1f`, `2680.0` and `true` are errors;
  - no number twice in a list, nor in both lists.
CRLF and CR line endings (bodies edited in the GitHub UI) read as LF.

Usage:
    python3 build_tools/issue_block.py --check     # the description from env PR_BODY
    python3 build_tools/issue_block.py --pr <N>    # fetch antonsynd/sharpy PR <N> with `gh api`

--check exits 0 when there is no block or a valid one (and says which), 1 when the block is
malformed (one `::error::` line). --pr prints one JSON object
{"pr", "block", "url", "base", "merged", "merge_commit_sha", "closes", "files"} ("block" is false
when the description has no block, and both lists are then empty) and exits 0, 1 when the block is
malformed, 2 when `gh` fails.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from dataclasses import dataclass
from typing import Callable, Dict, List, Mapping, Optional, Sequence, Tuple

import yaml

REPO = "antonsynd/sharpy"
KEY = "sharpy-issues"
LISTS = ("closes", "files")

_FENCE_OPEN = re.compile(r"^( {0,3})(`{3,}|~{3,})(.*)$")
_KEY = re.compile(re.escape(KEY) + r"['\"]?\s*:")
_COMMENT_OPEN = re.compile(r"^ {0,3}<!--")
_DECIMAL = re.compile(r"^(?:0|[1-9][0-9]*)$")


@dataclass(frozen=True)
class IssueBlock:
    closes: Tuple[int, ...] = ()
    files: Tuple[int, ...] = ()


class _Loader(yaml.SafeLoader):
    """safe_load with decimal-only integers and no duplicate mapping keys."""

    def construct_mapping(self, node, deep=False):  # type: ignore[override]
        seen = set()
        for key_node, _ in node.value:
            key = self.construct_object(key_node, deep=deep)
            if not isinstance(key, (str, int, float, bool, type(None))):
                continue  # unhashable: SafeLoader refuses it below
            if key in seen:
                raise ValueError(
                    f"duplicate key {key!r} (line {key_node.start_mark.line + 1})"
                )
            seen.add(key)
        return super().construct_mapping(node, deep=deep)


# Drop YAML 1.1's implicit int (octal `012`, `0x1f`, `2_680`, sexagesimal `1:20`) and add a decimal
# one, so anything but a plain decimal reads as a string and is refused below.
_Loader.yaml_implicit_resolvers = {
    ch: [(tag, rx) for tag, rx in resolvers if tag != "tag:yaml.org,2002:int"]
    for ch, resolvers in yaml.SafeLoader.yaml_implicit_resolvers.items()
}
_Loader.add_implicit_resolver("tag:yaml.org,2002:int", _DECIMAL, list("0123456789"))


def _decimal_int(loader: yaml.SafeLoader, node: yaml.Node) -> int:
    """An explicit `!!int` tag takes the same decimal-only rule (`!!int "012"` would read as 10)."""
    text = loader.construct_scalar(node)  # type: ignore[arg-type]
    if not isinstance(text, str) or not _DECIMAL.match(text):
        raise ValueError(f"{text!r} is not a plain decimal issue number")
    return int(text)


_Loader.add_constructor("tag:yaml.org,2002:int", _decimal_int)


def _yaml_fences(body: str) -> List[str]:
    """The text of every fenced code block whose info string's first word is `yaml`."""
    lines = body.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    fences: List[str] = []
    i = 0
    while i < len(lines):
        line = lines[i]
        if _COMMENT_OPEN.match(line):
            while i < len(lines) and "-->" not in lines[i]:
                i += 1
            i += 1
            continue
        m = _FENCE_OPEN.match(line)
        if not m or (m.group(2)[0] == "`" and "`" in m.group(3)):
            i += 1
            continue
        indent, fence, info = len(m.group(1)), m.group(2), m.group(3).strip()
        close = re.compile(
            r"^ {0,3}" + re.escape(fence[0]) + "{" + str(len(fence)) + r",}\s*$"
        )
        content: List[str] = []
        i += 1
        while i < len(lines) and not close.match(lines[i]):
            text = lines[i]
            strip = len(text) - len(text.lstrip(" "))
            content.append(text[min(indent, strip) :])
            i += 1
        i += 1
        if info.split()[:1] == ["yaml"]:
            fences.append("\n".join(content))
    return fences


def _numbers(name: str, value: object) -> Tuple[int, ...]:
    if not isinstance(value, list):
        raise ValueError(
            f"`{name}` must be a list of issue numbers, e.g. [2680]; got {value!r}"
        )
    for item in value:
        if isinstance(item, bool) or not isinstance(item, int) or item <= 0:
            raise ValueError(
                f"`{name}` holds {item!r}: write each issue as a bare positive decimal number "
                '(2680, not "#2680")'
            )
    dupes = sorted({n for n in value if value.count(n) > 1})
    if dupes:
        raise ValueError(f"`{name}` lists {', '.join(map(str, dupes))} more than once")
    return tuple(value)


def parse(body: Optional[str]) -> Optional[IssueBlock]:
    """The description's `sharpy-issues` block; None when it has none. ValueError when malformed."""
    blocks = [text for text in _yaml_fences(body or "") if _KEY.search(text)]
    if not blocks:
        return None
    if len(blocks) > 1:
        raise ValueError(
            f"{len(blocks)} `{KEY}` blocks; a description holds at most one"
        )
    try:
        data = yaml.load(blocks[0], Loader=_Loader)  # _Loader is a SafeLoader
    except yaml.YAMLError as e:
        raise ValueError(
            f"the `{KEY}` block is not valid YAML: {e}".replace("\n", " ")
        ) from None
    if not isinstance(data, dict) or list(data) != [KEY]:
        keys = list(data) if isinstance(data, dict) else type(data).__name__
        raise ValueError(
            f"the block must be a mapping with the one key `{KEY}`; got {keys}"
        )
    inner = data[KEY]
    if inner is None:
        inner = {}
    if not isinstance(inner, dict):
        raise ValueError(f"`{KEY}` must map `closes`/`files` to lists; got {inner!r}")
    unknown = [k for k in inner if k not in LISTS]
    if unknown:
        raise ValueError(
            f"`{KEY}` has unknown key(s) {unknown}; allowed: closes, files"
        )
    closes = _numbers("closes", inner.get("closes", []))
    files = _numbers("files", inner.get("files", []))
    both = sorted(set(closes) & set(files))
    if both:
        raise ValueError(
            f"{', '.join(map(str, both))} listed in both `closes` and `files`"
        )
    return IssueBlock(closes=closes, files=files)


class FetchError(Exception):
    pass


def fetch_pr(number: int) -> Mapping[str, object]:
    """The pull request's JSON from `gh api`; FetchError when gh fails or answers non-JSON."""
    try:
        out = subprocess.run(
            ["gh", "api", "-X", "GET", f"repos/{REPO}/pulls/{number}"],
            capture_output=True,
            text=True,
            check=False,
        )
    except OSError as e:
        raise FetchError(f"gh: {e}") from None
    if out.returncode != 0:
        raise FetchError(f"gh api exited {out.returncode}: {out.stderr.strip()}")
    try:
        data = json.loads(out.stdout)
    except json.JSONDecodeError as e:
        raise FetchError(f"gh api answered non-JSON: {e}") from None
    if not isinstance(data, dict):
        raise FetchError("gh api answered a non-object")
    return data


def _describe(block: Optional[IssueBlock]) -> str:
    if block is None:
        return f"issue-block: no `{KEY}` block"
    return f"issue-block: closes {list(block.closes)}; files {list(block.files)}"


def main(
    argv: Optional[Sequence[str]] = None,
    environ: Optional[Mapping[str, str]] = None,
    fetch: Callable[[int], Mapping[str, object]] = fetch_pr,
) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    mode = ap.add_mutually_exclusive_group(required=True)
    mode.add_argument("--check", action="store_true", help="validate env PR_BODY")
    mode.add_argument(
        "--pr", type=int, metavar="N", help=f"fetch {REPO} pull request N"
    )
    args = ap.parse_args(argv)
    env = os.environ if environ is None else environ

    if args.check:
        try:
            block = parse(env.get("PR_BODY", ""))
        except ValueError as e:
            print(f"::error::issue-block: {e}")
            return 1
        print(_describe(block))
        return 0

    try:
        pr = fetch(args.pr)
    except FetchError as e:
        print(f"issue-block: {e}", file=sys.stderr)
        return 2
    try:
        block = parse(pr.get("body") or "")  # type: ignore[arg-type]
    except ValueError as e:
        print(f"issue-block: PR #{args.pr}: {e}", file=sys.stderr)
        return 1
    found = block is not None
    block = block or IssueBlock()
    base = pr.get("base")
    out: Dict[str, object] = {
        "pr": args.pr,
        "block": found,
        "url": pr.get("html_url"),
        "base": base.get("ref") if isinstance(base, dict) else None,
        "merged": bool(pr.get("merged")),
        "merge_commit_sha": pr.get("merge_commit_sha"),
        "closes": list(block.closes),
        "files": list(block.files),
    }
    print(json.dumps(out))
    return 0


if __name__ == "__main__":
    sys.exit(main())
