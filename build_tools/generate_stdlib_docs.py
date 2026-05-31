#!/usr/bin/env python3
"""
Generate Sharpy standard library documentation from Sharpy.Core C# source.

Parses XML doc comments, method signatures, and attributes from C# files
and produces Markdown pages suitable for MkDocs Material.
"""

import re
import os
import shutil
import tempfile
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional
from xml.etree import ElementTree


# ---------------------------------------------------------------------------
# Hand-authored pages
# ---------------------------------------------------------------------------

# Pages the generator does NOT own (their source has no introspectable members,
# or the docs are written by hand) but which must still appear in the index
# Modules table and the mkdocs nav. The generator must never overwrite or delete
# these pages, but it must merge them into the index and nav so they stay linked.
#
# Each entry maps the module name to:
#   "description" -- one-line cell for the index Modules table
#   "nav_title"   -- the title used in the mkdocs nav (usually the module name)
HAND_AUTHORED_MODULES: dict[str, dict[str, str]] = {
    "numpy": {
        "description": (
            "NumPy-style N-dimensional arrays and numerical routines, backed by "
            "MathNet.Numerics."
        ),
        "nav_title": "numpy",
    },
    "requests": {
        "description": (
            "Pythonic HTTP client for sending requests and handling responses, "
            "similar to Python's `requests` library."
        ),
        "nav_title": "requests",
    },
    "sqlite3": {
        "description": (
            "SQLite database access with a DB-API-style interface, similar to "
            "Python's `sqlite3` module."
        ),
        "nav_title": "sqlite3",
    },
}


# ---------------------------------------------------------------------------
# Name mangling: PascalCase -> snake_case
# ---------------------------------------------------------------------------

# Special-case names that don't follow simple PascalCase->snake_case rules
_SPECIAL_NAMES: dict[str, str] = {
    "ToString": "__str__",
    "GetHashCode": "__hash__",
    "Equals": "__eq__",
    "IsInstance": "isinstance",
    "Isinstance": "isinstance",
    "Issubclass": "issubclass",
    "Isfinite": "isfinite",
    "Isinf": "isinf",
    "Isnan": "isnan",
    "Log2": "log2",
    "Log10": "log10",
    "Log1P": "log1p",
    "Expm1": "expm1",
    "Atan2": "atan2",
    "Fabs": "fabs",
    "Ldexp": "ldexp",
    "Frexp": "frexp",
    "Modf": "modf",
    "Trunc": "trunc",
    "Gcd": "gcd",
    "Lcm": "lcm",
    "Comb": "comb",
    "Perm": "perm",
    "Fsum": "fsum",
    "Prod": "prod",
    "Isclose": "isclose",
    "Copysign": "copysign",
    "Remainder": "remainder",
    "Nextafter": "nextafter",
    "Factorial": "factorial",
}


def pascal_to_snake(name: str) -> str:
    """Convert PascalCase to snake_case with special case handling."""
    if name in _SPECIAL_NAMES:
        return _SPECIAL_NAMES[name]
    # Insert underscore before uppercase letters that follow lowercase letters or digits
    result = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", name)
    # Insert underscore before uppercase letters followed by lowercase (e.g., XMLParser -> XML_Parser)
    result = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", result)
    return result.lower()


# ---------------------------------------------------------------------------
# Type mapping: C# -> Sharpy
# ---------------------------------------------------------------------------

_TYPE_MAP: dict[str, str] = {
    "void": "None",
    "int": "int",
    "Int32": "int",
    "System.Int32": "int",
    "long": "long",
    "Int64": "long",
    "System.Int64": "long",
    "double": "float",
    "Double": "float",
    "System.Double": "float",
    "float": "float32",
    "Single": "float32",
    "System.Single": "float32",
    "string": "str",
    "String": "str",
    "System.String": "str",
    "bool": "bool",
    "Boolean": "bool",
    "System.Boolean": "bool",
    "object": "object",
    "Object": "object",
    "System.Object": "object",
}

# Generic type mappings (prefix match)
_GENERIC_TYPE_MAP: dict[str, str] = {
    "List": "list",
    "Sharpy.List": "list",
    "Dict": "dict",
    "Sharpy.Dict": "dict",
    "Set": "set",
    "Sharpy.Set": "set",
    "IEnumerable": "Iterable",
    "IEnumerator": "Iterator",
    "Optional": "Optional",
    "Result": "Result",
    "Tuple": "tuple",
    "ValueTuple": "tuple",
}


def map_type(cs_type: str) -> str:
    """Map a C# type string to Sharpy type notation."""
    cs_type = cs_type.strip()

    if not cs_type:
        return ""

    # Nullable suffix
    if cs_type.endswith("?"):
        inner = map_type(cs_type[:-1])
        return f"{inner}?"

    # Direct mapping
    if cs_type in _TYPE_MAP:
        return _TYPE_MAP[cs_type]

    # Generic types: Name<T1, T2>
    generic_match = re.match(r"^([\w.]+)<(.+)>$", cs_type)
    if generic_match:
        outer = generic_match.group(1)
        inner_raw = generic_match.group(2)
        # Split on top-level commas (respect nested generics)
        inners = _split_generic_args(inner_raw)
        mapped_inners = ", ".join(map_type(i) for i in inners)
        mapped_outer = _GENERIC_TYPE_MAP.get(outer, outer)
        return f"{mapped_outer}[{mapped_inners}]"

    # Array types
    if cs_type.endswith("[]"):
        inner = map_type(cs_type[:-2])
        return f"list[{inner}]"

    # params keyword prefix
    if cs_type.startswith("params "):
        inner = map_type(cs_type[7:])
        return f"*{inner}"

    # Single-letter type params (T, K, V, etc.)
    if len(cs_type) == 1 and cs_type.isupper():
        return cs_type

    # Check generic type map for non-generic usage
    if cs_type in _GENERIC_TYPE_MAP:
        return _GENERIC_TYPE_MAP[cs_type]

    return cs_type


def _split_generic_args(s: str) -> list[str]:
    """Split generic arguments respecting nested angle brackets."""
    parts = []
    depth = 0
    current = []
    for ch in s:
        if ch == "<":
            depth += 1
            current.append(ch)
        elif ch == ">":
            depth -= 1
            current.append(ch)
        elif ch == "," and depth == 0:
            parts.append("".join(current).strip())
            current = []
        else:
            current.append(ch)
    if current:
        parts.append("".join(current).strip())
    return parts


# ---------------------------------------------------------------------------
# XML doc comment parsing
# ---------------------------------------------------------------------------


def _strip_xml_tags(text: str) -> str:
    """Convert XML doc content to plain text."""
    if not text:
        return ""
    # Replace common XML doc tags
    text = re.sub(r"<see\s+cref=\"([^\"]+)\"\s*/>", r"`\1`", text)
    text = re.sub(r"<paramref\s+name=\"([^\"]+)\"\s*/>", r"*\1*", text)
    text = re.sub(r"<c>([^<]*)</c>", r"`\1`", text)
    text = re.sub(r"</?[a-zA-Z][^>]*>", "", text)
    return text.strip()


def _parse_xml_doc(lines: list[str]) -> dict:
    """Parse XML doc comment lines into structured data."""
    # Join all /// lines into one XML string
    xml_lines = []
    for line in lines:
        stripped = line.strip()
        if stripped.startswith("///"):
            content = stripped[3:]
            if content.startswith(" "):
                content = content[1:]
            xml_lines.append(content)

    xml_text = "\n".join(xml_lines)
    if not xml_text.strip():
        return {}

    # Wrap in root element for valid XML
    wrapped = f"<doc>{xml_text}</doc>"
    try:
        root = ElementTree.fromstring(wrapped)
    except ElementTree.ParseError:
        # Fallback: extract summary with regex
        summary_match = re.search(r"<summary>(.*?)</summary>", xml_text, re.DOTALL)
        return {
            "summary": _strip_xml_tags(summary_match.group(1)) if summary_match else ""
        }

    result: dict = {}

    def _inner_xml(el: ElementTree.Element) -> str:
        """Get inner XML content of an element, preserving child tags."""
        parts = [el.text or ""]
        for child in el:
            # tostring() includes the child's tail text automatically
            parts.append(ElementTree.tostring(child, encoding="unicode"))
        return "".join(parts)

    # Summary
    summary_el = root.find("summary")
    if summary_el is not None:
        result["summary"] = _strip_xml_tags(_inner_xml(summary_el)).strip()

    # Parameters
    params = []
    for param_el in root.findall("param"):
        name = param_el.get("name", "")
        desc = _strip_xml_tags(_inner_xml(param_el)).strip()
        params.append((name, desc))
    if params:
        result["params"] = params

    # Returns
    returns_el = root.find("returns")
    if returns_el is not None:
        result["returns"] = _strip_xml_tags(_inner_xml(returns_el)).strip()

    # Example
    example_el = root.find("example")
    if example_el is not None:
        code_el = example_el.find("code")
        if code_el is not None:
            code_text = (code_el.text or "").strip()
            if code_text:
                result["example"] = code_text

    # Remarks
    remarks_el = root.find("remarks")
    if remarks_el is not None:
        result["remarks"] = _strip_xml_tags(_inner_xml(remarks_el)).strip()

    # Exceptions
    exceptions = []
    for exc_el in root.findall("exception"):
        cref = exc_el.get("cref", "")
        desc = _strip_xml_tags(_inner_xml(exc_el)).strip()
        exceptions.append((cref, desc))
    if exceptions:
        result["exceptions"] = exceptions

    # Typeparam
    typeparams = []
    for tp_el in root.findall("typeparam"):
        name = tp_el.get("name", "")
        desc = _strip_xml_tags(_inner_xml(tp_el)).strip()
        typeparams.append((name, desc))
    if typeparams:
        result["typeparams"] = typeparams

    return result


# ---------------------------------------------------------------------------
# Data model
# ---------------------------------------------------------------------------


@dataclass
class DocParam:
    name: str
    type: str
    default: Optional[str] = None
    description: str = ""


@dataclass
class DocMember:
    """A documented method, property, or constant."""

    kind: str  # "method", "property", "constant"
    name: str  # Sharpy name (snake_case)
    cs_name: str  # Original C# name
    signature: str  # Full Sharpy-style signature
    summary: str = ""
    params: list[DocParam] = field(default_factory=list)
    returns: str = ""
    return_type: str = ""
    example: str = ""
    remarks: str = ""
    exceptions: list[tuple[str, str]] = field(default_factory=list)
    is_static: bool = False


@dataclass
class DocType:
    """A documented module type (e.g., ArgumentParser in argparse)."""

    name: str
    cs_name: str
    summary: str = ""
    members: list[DocMember] = field(default_factory=list)


@dataclass
class DocModule:
    """A documented module or core type."""

    name: str  # Sharpy module name
    kind: str  # "module", "type", "builtins"
    summary: str = ""
    members: list[DocMember] = field(default_factory=list)
    types: list[DocType] = field(default_factory=list)


# ---------------------------------------------------------------------------
# C# source parser
# ---------------------------------------------------------------------------

_EXTENSION_THIS_RE = re.compile(r"^this\s+\S+\s+\w+")

# Skip patterns
_SKIP_NAMES = {
    "GetEnumerator",
    "GetReverseEnumerator",
    "CompareTo",
    "GetHashCode",
    "Equals",
    "ToString",
    "Dispose",
    "TryGetValue",
    "ContainsKey",
}


def _is_operator(name: str) -> bool:
    return name.startswith("operator") and (len(name) <= 8 or not name[8].isalnum())


def _is_skippable(name: str, doc_lines: list[str]) -> bool:
    """Determine if a member should be skipped in docs."""
    if name in _SKIP_NAMES:
        return True
    if _is_operator(name):
        return True
    # Skip implicit/explicit conversion operators
    if name in ("implicit", "explicit"):
        return True
    # Skip inheritdoc
    doc_text = " ".join(doc_lines)
    if "<inheritdoc" in doc_text:
        return True
    return False


def _collect_doc_lines(lines: list[str], decl_index: int) -> list[str]:
    """Collect XML doc comment lines preceding a declaration."""
    doc_lines = []
    i = decl_index - 1
    while i >= 0:
        stripped = lines[i].strip()
        if stripped.startswith("///"):
            doc_lines.insert(0, stripped)
            i -= 1
        elif stripped.startswith("//"):
            # Skip regular (non-doc) comments between doc block and declaration
            i -= 1
        elif stripped.startswith("[") or stripped.startswith("#") or stripped == "":
            # Skip attributes, preprocessor directives, and blank lines
            if doc_lines:
                break
            i -= 1
        else:
            break
    return doc_lines


def _parse_params(param_str: str, is_extension: bool = False) -> list[DocParam]:
    """Parse a C# parameter list into DocParam objects."""
    # Normalize whitespace (multi-line params)
    param_str = " ".join(param_str.split())
    if not param_str.strip():
        return []

    params = []
    # Split on commas respecting generics
    parts = _split_generic_args(param_str)

    for i, part in enumerate(parts):
        part = part.strip()
        if not part:
            continue

        # Skip 'this Type name' for extension methods
        if i == 0 and is_extension and _EXTENSION_THIS_RE.match(part):
            continue

        # Handle params keyword
        part = part.replace("params ", "")

        # Handle default values
        default = None
        if "=" in part:
            part, default_raw = part.rsplit("=", 1)
            default = default_raw.strip()
            part = part.strip()

        # Split type and name
        tokens = part.rsplit(None, 1)
        if len(tokens) == 2:
            ptype, pname = tokens
            params.append(
                DocParam(
                    name=pascal_to_snake(pname),
                    type=map_type(ptype.strip()),
                    default=default,
                )
            )
        elif len(tokens) == 1:
            params.append(DocParam(name=tokens[0], type=""))

    return params


def _join_declaration(lines: list[str], start: int) -> tuple[str, int]:
    """Join a multi-line declaration into a single string.

    Returns (joined_line, end_index).
    Handles cases where ( ... ) spans multiple lines or { is on the next line.
    """
    result = lines[start].strip()
    j = start

    # If line has an opening paren, collect until closing paren
    if "(" in result and ")" not in result:
        depth = result.count("(") - result.count(")")
        while depth > 0 and j + 1 < len(lines):
            j += 1
            next_line = lines[j].strip()
            result += " " + next_line
            depth += next_line.count("(") - next_line.count(")")

    return result, j


# Regex patterns applied to joined (single-line) declarations
_METHOD_PATTERN = re.compile(
    r"^public\s+"
    r"((?:(?:static|override|virtual|sealed|new|async|unsafe)\s+)*)"  # modifiers
    r"([\w<>\[\],\s\?\.]+?)\s+"  # return type
    r"(\w+)"  # method name
    r"(?:<([^>]+)>)?"  # optional type params
    r"\(([^)]*)\)"  # parameters
)

_CONST_PATTERN = re.compile(
    r"^public\s+(?:const|static\s+readonly)\s+"
    r"([\w<>\[\],\s\?\.]+?)\s+"  # type
    r"(\w+)\s*=\s*(.+?)\s*;"
)

_PROPERTY_PATTERN = re.compile(
    r"^public\s+"
    r"((?:(?:static|override|virtual|sealed|new)\s+)*)"  # modifiers
    r"([\w<>\[\],\s\?\.]+?)\s+"  # type
    r"(\w+)\s*"  # name
    r"(?:=>|{\s*get)"
)


_NON_PUBLIC_CLASS_RE = re.compile(
    r"(?:internal|private|protected)\s+(?:(?:sealed|abstract|static|partial)\s+)*"
    r"(?:class|struct)\s"
)


def _find_nonpublic_class_ranges(lines: list[str]) -> list[tuple[int, int]]:
    """Find line ranges of internal/private/protected classes in a C# file.

    Returns a list of (start, end) tuples (0-based inclusive) covering each
    non-public class or struct body. Brace counting is performed with a state
    machine that ignores braces inside line comments, block comments, regular
    strings, verbatim strings, and interpolated strings — so hazards like
    ``"{"`` in a string literal or ``/* { */`` in a comment are handled.
    """
    ranges: list[tuple[int, int]] = []
    brace_depth = 0
    in_non_public = False
    seen_open_brace = False
    non_public_start = -1

    # Persistent state across lines: inside a /* ... */ block comment
    in_block_comment = False

    for idx, line in enumerate(lines):
        stripped = line.strip()
        if not in_non_public and not in_block_comment and _NON_PUBLIC_CLASS_RE.match(stripped):
            in_non_public = True
            non_public_start = idx
            brace_depth = 0
            seen_open_brace = False

        if in_non_public:
            # Scan the raw line char-by-char with a state machine
            line_open, line_close, in_block_comment = _count_code_braces(line, in_block_comment)
            brace_depth += line_open - line_close
            if line_open > 0:
                seen_open_brace = True
            if seen_open_brace and brace_depth <= 0 and non_public_start >= 0:
                ranges.append((non_public_start, idx))
                in_non_public = False
                non_public_start = -1
        else:
            # Even outside a non-public class we must track block comments
            # so that a class declaration inside a comment is not misread.
            _, _, in_block_comment = _count_code_braces(line, in_block_comment)

    return ranges


def _count_code_braces(line: str, in_block_comment: bool) -> tuple[int, int, bool]:
    """Count ``{`` and ``}`` in a C# source line, ignoring non-code contexts.

    Returns ``(open_count, close_count, in_block_comment_after)`` where the
    third element reflects whether a ``/* ... */`` comment is still open after
    processing this line. Handles:

    - ``//`` line comments (rest of line ignored)
    - ``/* ... */`` block comments (may span multiple lines)
    - Regular strings ``"..."`` (with ``\\`` escapes)
    - Verbatim strings ``@"..."`` (``""`` escapes a quote)
    - Interpolated strings ``$"..."`` — ``{{``/``}}`` are escaped braces, a
      single ``{`` opens an interpolation hole whose contents are code again
    - Verbatim interpolated strings ``$@"..."`` / ``@$"..."``

    Nested interpolation holes are tracked via a simple brace-depth counter
    per string; this is sufficient for our stdlib sources, which do not use
    nested interpolated strings inside holes.
    """
    open_count = 0
    close_count = 0

    # String state: None when not in a string, otherwise a dict describing it
    # Fields: verbatim (bool), interpolated (bool), hole_depth (int, 0 when not in a hole)
    string_state: dict | None = None

    i = 0
    n = len(line)
    while i < n:
        c = line[i]
        nxt = line[i + 1] if i + 1 < n else ""

        if in_block_comment:
            if c == "*" and nxt == "/":
                in_block_comment = False
                i += 2
                continue
            i += 1
            continue

        if string_state is not None:
            if string_state["interpolated"] and string_state["hole_depth"] > 0:
                # Inside an interpolation hole — treat as code, but track braces
                # so we know when the hole closes. Also look for nested strings.
                if c == "{":
                    string_state["hole_depth"] += 1
                    open_count += 1
                    i += 1
                    continue
                if c == "}":
                    string_state["hole_depth"] -= 1
                    close_count += 1
                    i += 1
                    continue
                # Nested string inside a hole — fall through to string-start logic below
                # by temporarily clearing string_state.
                saved = string_state
                string_state = None
                # Re-dispatch this character as if in code
                if c == '"':
                    string_state = {"verbatim": False, "interpolated": False, "hole_depth": 0}
                    i += 1
                    # restore outer state by stacking via saved → but we only need
                    # to remember to return to `saved` when this inner string ends.
                    # Use a simple approach: process the inner string inline.
                    while i < n:
                        cc = line[i]
                        if cc == "\\" and i + 1 < n:
                            i += 2
                            continue
                        if cc == '"':
                            i += 1
                            string_state = saved
                            break
                        i += 1
                    else:
                        # Unterminated — drop back to saved state
                        string_state = saved
                    continue
                # Not a string, not a brace — restore and advance
                string_state = saved
                i += 1
                continue

            # Inside a string literal (not in a hole)
            if string_state["verbatim"]:
                if c == '"':
                    if nxt == '"':
                        i += 2  # escaped quote
                        continue
                    if string_state["interpolated"]:
                        # End of verbatim interpolated string
                        string_state = None
                        i += 1
                        continue
                    string_state = None
                    i += 1
                    continue
                if string_state["interpolated"]:
                    if c == "{":
                        if nxt == "{":
                            i += 2  # escaped brace
                            continue
                        string_state["hole_depth"] = 1
                        open_count += 1
                        i += 1
                        continue
                    if c == "}":
                        if nxt == "}":
                            i += 2
                            continue
                        # Stray } in verbatim interpolated — treat as literal
                        i += 1
                        continue
                i += 1
                continue
            else:
                if c == "\\" and i + 1 < n:
                    i += 2
                    continue
                if c == '"':
                    string_state = None
                    i += 1
                    continue
                if string_state["interpolated"]:
                    if c == "{":
                        if nxt == "{":
                            i += 2
                            continue
                        string_state["hole_depth"] = 1
                        open_count += 1
                        i += 1
                        continue
                    if c == "}":
                        if nxt == "}":
                            i += 2
                            continue
                        i += 1
                        continue
                i += 1
                continue

        # Plain code context
        if c == "/" and nxt == "/":
            break  # rest of line is a comment
        if c == "/" and nxt == "*":
            in_block_comment = True
            i += 2
            continue
        if c == "'":
            # Character literal — skip until closing '
            i += 1
            while i < n:
                if line[i] == "\\" and i + 1 < n:
                    i += 2
                    continue
                if line[i] == "'":
                    i += 1
                    break
                i += 1
            continue
        if c == '"':
            string_state = {"verbatim": False, "interpolated": False, "hole_depth": 0}
            i += 1
            continue
        if c == "@" and nxt == '"':
            string_state = {"verbatim": True, "interpolated": False, "hole_depth": 0}
            i += 2
            continue
        if c == "$" and nxt == '"':
            string_state = {"verbatim": False, "interpolated": True, "hole_depth": 0}
            i += 2
            continue
        if c == "$" and nxt == "@" and i + 2 < n and line[i + 2] == '"':
            string_state = {"verbatim": True, "interpolated": True, "hole_depth": 0}
            i += 3
            continue
        if c == "@" and nxt == "$" and i + 2 < n and line[i + 2] == '"':
            string_state = {"verbatim": True, "interpolated": True, "hole_depth": 0}
            i += 3
            continue
        if c == "{":
            open_count += 1
        elif c == "}":
            close_count += 1
        i += 1

    return open_count, close_count, in_block_comment


def parse_cs_file(
    filepath: Path,
    is_extension: bool = False,
    is_builtins: bool = False,
    line_range: tuple[int, int] | None = None,
) -> list[DocMember]:
    """Parse a C# file and extract documented public members.

    If *line_range* is given as ``(start, end)`` (0-based inclusive), only
    declarations within that line range are considered.
    """
    text = filepath.read_text(encoding="utf-8")
    lines = text.split("\n")
    members: list[DocMember] = []

    _non_public_ranges = _find_nonpublic_class_ranges(lines)

    i = 0

    while i < len(lines):
        stripped = lines[i].strip()

        # Skip lines outside the requested range
        if line_range is not None and not (line_range[0] <= i <= line_range[1]):
            i += 1
            continue

        # Skip members inside internal/private classes
        if any(start <= i <= end for start, end in _non_public_ranges):
            i += 1
            continue

        # Only process lines starting with 'public'
        if not stripped.startswith("public "):
            i += 1
            continue

        # Join multi-line declarations
        joined, end_i = _join_declaration(lines, i)

        # Constants
        const_match = _CONST_PATTERN.match(joined)
        if const_match:
            ctype, cname, cval = const_match.groups()
            doc_lines = _collect_doc_lines(lines, i)
            if not _is_skippable(cname, doc_lines):
                doc = _parse_xml_doc(doc_lines)
                members.append(
                    DocMember(
                        kind="constant",
                        name=pascal_to_snake(cname),
                        cs_name=cname,
                        signature="",
                        summary=doc.get("summary", ""),
                        return_type=map_type(ctype.strip()),
                        is_static=True,
                    )
                )
            i = end_i + 1
            continue

        # Skip class/struct/interface/enum declarations
        if re.match(
            r"public\s+(?:(?:static|sealed|abstract|partial|readonly)\s+)*(?:class|struct|interface|enum)\s",
            joined,
        ):
            i = end_i + 1
            continue

        # Skip any operator declaration (implicit, explicit, true, false, +, -, etc.)
        pre_paren = joined.split("(")[0] if "(" in joined else joined
        if " operator " in pre_paren:
            i = end_i + 1
            continue

        # Methods (must have parentheses)
        method_match = _METHOD_PATTERN.match(joined)
        if method_match and "(" in joined:
            modifiers, ret_type, mname, type_params, param_str = method_match.groups()

            if _is_operator(mname) or mname in ("implicit", "explicit"):
                i = end_i + 1
                continue

            doc_lines = _collect_doc_lines(lines, i)
            if _is_skippable(mname, doc_lines):
                i = end_i + 1
                continue

            doc = _parse_xml_doc(doc_lines)
            params = _parse_params(param_str or "", is_extension=is_extension)

            # Merge doc param descriptions
            doc_params = {p[0]: p[1] for p in doc.get("params", [])}
            for p in params:
                p.description = doc_params.get(p.name, "")

            sharpy_name = pascal_to_snake(mname)
            mapped_ret = map_type(ret_type.strip())
            is_static = "static" in modifiers

            # Build signature
            param_strs = []
            for p in params:
                s = f"{p.name}: {p.type}" if p.type else p.name
                if p.default is not None:
                    s += f" = {p.default}"
                param_strs.append(s)
            sig = f"{sharpy_name}({', '.join(param_strs)})"
            if mapped_ret and mapped_ret != "None":
                sig += f" -> {mapped_ret}"

            members.append(
                DocMember(
                    kind="method",
                    name=sharpy_name,
                    cs_name=mname,
                    signature=sig,
                    summary=doc.get("summary", ""),
                    params=params,
                    returns=doc.get("returns", ""),
                    return_type=mapped_ret,
                    example=doc.get("example", ""),
                    remarks=doc.get("remarks", ""),
                    exceptions=doc.get("exceptions", []),
                    is_static=is_static,
                )
            )
            i = end_i + 1
            continue

        # Properties (no parentheses in the declaration)
        if "(" not in joined:
            prop_match = _PROPERTY_PATTERN.match(joined)
            if prop_match:
                modifiers, ptype, pname = prop_match.groups()
                doc_lines = _collect_doc_lines(lines, i)
                if not _is_skippable(pname, doc_lines):
                    doc = _parse_xml_doc(doc_lines)
                    members.append(
                        DocMember(
                            kind="property",
                            name=pascal_to_snake(pname),
                            cs_name=pname,
                            signature="",
                            summary=doc.get("summary", ""),
                            return_type=map_type(ptype.strip()),
                            is_static="static" in modifiers,
                        )
                    )

        i = end_i + 1

    return members


def _get_class_summary(filepath: Path) -> str:
    """Extract the class-level XML doc summary from a file."""
    text = filepath.read_text(encoding="utf-8")
    lines = text.split("\n")

    for i, line in enumerate(lines):
        stripped = line.strip()
        if re.match(
            r"public\s+(?:(?:static|sealed|abstract|partial|readonly)\s+)*"
            r"(?:class|struct)\s+",
            stripped,
        ):
            doc_lines = _collect_doc_lines(lines, i)
            doc = _parse_xml_doc(doc_lines)
            summary = doc.get("summary", "")
            if summary:
                return summary

    return ""


# ---------------------------------------------------------------------------
# Source discovery
# ---------------------------------------------------------------------------


def discover_modules(core_dir: Path) -> list[DocModule]:
    """Discover all stdlib modules from Sharpy.Core source."""
    modules: list[DocModule] = []

    for subdir in sorted(core_dir.iterdir()):
        if not subdir.is_dir():
            continue
        init_file = subdir / "__Init__.cs"
        if not init_file.exists():
            continue

        # Read module name from [SharpyModule("...")] attribute
        init_text = init_file.read_text(encoding="utf-8")
        mod_match = re.search(r'\[SharpyModule\("([^"]+)"\)\]', init_text)
        if not mod_match:
            continue

        mod_name = mod_match.group(1)

        # Skip sub-modules (os.path) and builtins (handled separately)
        if "." in mod_name or mod_name == "builtins":
            continue

        # Parse all .cs files in the module directory
        summary = ""
        all_members: list[DocMember] = []
        all_types: list[DocType] = []

        for cs_file in sorted(subdir.glob("*.cs")):
            if cs_file.name == "__Init__.cs":
                members = parse_cs_file(cs_file)
                all_members.extend(members)
                continue

            # Get module summary from the main implementation file
            if not summary:
                file_summary = _get_class_summary(cs_file)
                if file_summary:
                    summary = file_summary

            # Check if this file contains SharpyModuleType-annotated classes.
            # Supports both 1-arg form `[SharpyModuleType("mod")]` and 2-arg
            # form `[SharpyModuleType("mod", "DisplayName")]` where the second
            # argument overrides the rendered type name (e.g. Python-lowercase
            # names like `date`, `timedelta`).
            file_text = cs_file.read_text(encoding="utf-8")
            type_annotations = list(
                re.finditer(
                    r'\[SharpyModuleType\("([^"]+)"(?:\s*,\s*"([^"]+)")?\)\]',
                    file_text,
                )
            )
            if type_annotations:
                # Find all annotated class names and their line positions
                file_lines = file_text.split("\n")
                annotated_classes: list[tuple[str, str, int, int]] = []
                for ta in type_annotations:
                    display_name_override = ta.group(2)
                    after = file_text[ta.end() :]
                    cm = re.search(
                        r"public\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*"
                        r"class\s+(\w+)",
                        after,
                    )
                    if cm:
                        class_name = cm.group(1)
                        display_name = display_name_override or class_name
                        class_pos = ta.end() + cm.start()
                        class_line = file_text[:class_pos].count("\n")
                        annotated_classes.append(
                            (display_name, class_name, class_line, 0)
                        )

                # Compute end lines (start of next class or EOF)
                for ci in range(len(annotated_classes)):
                    display_name, class_name, start, _ = annotated_classes[ci]
                    end = (
                        annotated_classes[ci + 1][2] - 1
                        if ci + 1 < len(annotated_classes)
                        else len(file_lines) - 1
                    )
                    annotated_classes[ci] = (display_name, class_name, start, end)

                # Parse each class range separately
                for display_name, class_name, start, end in annotated_classes:
                    type_summary = _get_class_summary(cs_file)
                    type_members = parse_cs_file(
                        cs_file,
                        line_range=(start, end),
                    )
                    all_types.append(
                        DocType(
                            name=display_name,
                            cs_name=cs_file.stem,
                            summary=type_summary,
                            members=type_members,
                        )
                    )
            else:
                members = parse_cs_file(cs_file)
                all_members.extend(members)

        modules.append(
            DocModule(
                name=mod_name,
                kind="module",
                summary=summary,
                members=all_members,
                types=all_types,
            )
        )

    return modules


def discover_core_types(core_dir: Path) -> list[DocModule]:
    """Discover core types (list, dict, set, str, complex)."""
    type_dirs = {
        "Partial.List": ("list", False),
        "Partial.Dict": ("dict", False),
        "Partial.Set": ("set", False),
        "Partial.String": ("str", True),  # extension methods
        "Partial.Complex": ("complex", False),
    }

    types: list[DocModule] = []

    for dirname, (type_name, is_extension) in type_dirs.items():
        subdir = core_dir / dirname
        if not subdir.exists():
            continue

        all_members: list[DocMember] = []
        summary = ""

        # Sort files so the main type file comes first (shorter name = main file)
        cs_files = sorted(subdir.glob("*.cs"), key=lambda f: (len(f.name), f.name))
        for cs_file in cs_files:
            if not summary:
                file_summary = _get_class_summary(cs_file)
                if file_summary:
                    summary = file_summary
            members = parse_cs_file(cs_file, is_extension=is_extension)
            all_members.extend(members)

        types.append(
            DocModule(
                name=type_name,
                kind="type",
                summary=summary,
                members=all_members,
            )
        )

    return types


def discover_builtins(core_dir: Path) -> DocModule:
    """Discover built-in functions from Builtins partial class."""
    all_members: list[DocMember] = []

    # Root-level files containing partial class Builtins
    for cs_file in sorted(core_dir.glob("*.cs")):
        text = cs_file.read_text(encoding="utf-8")
        if "partial class Builtins" in text:
            members = parse_cs_file(cs_file, is_builtins=True)
            all_members.extend(members)

    # Builtins/ subdirectory — only files containing partial class Builtins
    builtins_dir = core_dir / "Builtins"
    if builtins_dir.exists():
        for cs_file in sorted(builtins_dir.glob("*.cs")):
            if cs_file.name == "__Init__.cs":
                continue
            text = cs_file.read_text(encoding="utf-8")
            if "partial class Builtins" not in text:
                continue
            members = parse_cs_file(cs_file, is_builtins=True)
            all_members.extend(members)

    return DocModule(
        name="builtins",
        kind="builtins",
        summary="Functions available without any import.",
        members=all_members,
    )


# ---------------------------------------------------------------------------
# Markdown rendering
# ---------------------------------------------------------------------------


def _one_line(text: str) -> str:
    """Collapse multi-line text into a single line for table cells."""
    return " ".join(text.split()).strip()


def _escape_table_cell(text: str) -> str:
    """Escape text for use inside a markdown table cell.

    Collapses to a single line, then escapes pipe characters and backticks
    so they don't break the table structure or inline code formatting.
    """
    text = _one_line(text)
    text = text.replace("\\", "\\\\")
    text = text.replace("|", "\\|")
    text = text.replace("`", "\\`")
    return text


def _render_member(member: DocMember, prefix: str = "") -> str:
    """Render a single member to markdown."""
    lines = []

    if member.kind == "constant":
        # Constants are rendered in a table, not individually
        return ""

    heading = f"### `{prefix}{member.signature}`"
    lines.append(heading)
    lines.append("")

    if member.summary:
        lines.append(member.summary)
        lines.append("")

    if member.params:
        has_descriptions = any(p.description for p in member.params)
        if has_descriptions:
            lines.append("**Parameters:**")
            lines.append("")
            for p in member.params:
                desc = f" -- {p.description}" if p.description else ""
                lines.append(f"- `{p.name}` ({p.type}){desc}")
            lines.append("")

    if member.returns:
        lines.append(f"**Returns:** {member.returns}")
        lines.append("")

    if member.example:
        lines.append("```python")
        lines.append(member.example)
        lines.append("```")
        lines.append("")

    if member.remarks:
        lines.append(f"!!! note")
        # Indent all lines of the remark for admonition formatting
        for remark_line in member.remarks.split("\n"):
            lines.append(f"    {remark_line.strip()}")
        lines.append("")

    if member.exceptions:
        lines.append("**Raises:**")
        lines.append("")
        for exc_type, desc in member.exceptions:
            lines.append(f"- `{exc_type}` -- {desc}")
        lines.append("")

    return "\n".join(lines)


def _render_constants_table(constants: list[DocMember]) -> str:
    """Render constants as a markdown table."""
    if not constants:
        return ""

    lines = [
        "## Constants",
        "",
        "| Name | Type | Description |",
        "|------|------|-------------|",
    ]
    for c in constants:
        lines.append(
            f"| `{c.name}` | `{c.return_type}` | {_escape_table_cell(c.summary)} |"
        )
    lines.append("")
    return "\n".join(lines)


def render_module_page(module: DocModule) -> str:
    """Render a module documentation page."""
    lines = [f"# {module.name}", ""]

    if module.summary:
        lines.append(module.summary)
        lines.append("")

    if module.kind == "module":
        lines.append(f"```python")
        lines.append(f"import {module.name}")
        lines.append(f"```")
        lines.append("")

    # Constants
    constants = [m for m in module.members if m.kind == "constant"]
    if constants:
        lines.append(_render_constants_table(constants))

    # Properties (deduplicated by name — multiple classes may define the same property)
    properties = [m for m in module.members if m.kind == "property"]
    if properties:
        seen_props: set[str] = set()
        unique_props: list[DocMember] = []
        for p in properties:
            if p.name not in seen_props:
                seen_props.add(p.name)
                unique_props.append(p)
        lines.append("## Properties")
        lines.append("")
        lines.append("| Name | Type | Description |")
        lines.append("|------|------|-------------|")
        for p in unique_props:
            lines.append(
                f"| `{p.name}` | `{p.return_type}` | {_escape_table_cell(p.summary)} |"
            )
        lines.append("")

    # Methods/Functions
    methods = [m for m in module.members if m.kind == "method"]
    if methods:
        section_title = (
            "Functions" if module.kind in ("module", "builtins") else "Methods"
        )
        lines.append(f"## {section_title}")
        lines.append("")

        prefix = ""
        if module.kind == "module":
            prefix = f"{module.name}."

        for m in methods:
            lines.append(_render_member(m, prefix=prefix))

    # Module types (e.g., ArgumentParser in argparse, ChainMap/Deque/Counter
    # in collections). Each type gets its own H2 section containing its
    # constants, properties, and methods.
    for doc_type in module.types:
        lines.append(f"## {doc_type.name}")
        lines.append("")
        if doc_type.summary:
            lines.append(doc_type.summary)
            lines.append("")

        type_constants = [m for m in doc_type.members if m.kind == "constant"]
        if type_constants:
            lines.append("### Constants")
            lines.append("")
            lines.append("| Name | Type | Description |")
            lines.append("|------|------|-------------|")
            for c in type_constants:
                lines.append(
                    f"| `{c.name}` | `{c.return_type}` | {_escape_table_cell(c.summary)} |"
                )
            lines.append("")

        type_properties = [m for m in doc_type.members if m.kind == "property"]
        if type_properties:
            seen_type_props: set[str] = set()
            unique_type_props: list[DocMember] = []
            for p in type_properties:
                if p.name not in seen_type_props:
                    seen_type_props.add(p.name)
                    unique_type_props.append(p)
            lines.append("### Properties")
            lines.append("")
            lines.append("| Name | Type | Description |")
            lines.append("|------|------|-------------|")
            for p in unique_type_props:
                lines.append(
                    f"| `{p.name}` | `{p.return_type}` | {_escape_table_cell(p.summary)} |"
                )
            lines.append("")

        type_methods = [m for m in doc_type.members if m.kind == "method"]
        for m in type_methods:
            lines.append(_render_member(m))

    return "\n".join(lines)


def render_index_page(
    builtins: DocModule,
    core_types: list[DocModule],
    modules: list[DocModule],
) -> str:
    """Render the stdlib index page."""
    lines = [
        "# Standard Library Reference",
        "",
        "Sharpy's standard library provides Python-familiar APIs backed by .NET implementations.",
        "",
        "## Built-in Functions",
        "",
        f"[Built-in functions](builtins.md) available without any import: ",
    ]

    builtin_names = sorted(set(m.name for m in builtins.members if m.kind == "method"))
    if builtin_names:
        lines[-1] += ", ".join(f"`{n}()`" for n in builtin_names[:15])
        if len(builtin_names) > 15:
            lines[-1] += f", and {len(builtin_names) - 15} more."
        else:
            lines[-1] += "."
    lines.append("")

    lines.append("## Core Types")
    lines.append("")
    lines.append("| Type | Description |")
    lines.append("|------|-------------|")
    for ct in core_types:
        # Collapse multi-line summaries for table cells
        desc = _escape_table_cell(ct.summary)
        lines.append(f"| [`{ct.name}`]({ct.name}.md) | {desc} |")
    lines.append("")

    lines.append("## Modules")
    lines.append("")
    lines.append("| Module | Description |")
    lines.append("|--------|-------------|")

    # Merge generator-owned modules with the hand-authored allowlist, sorted by
    # name so the table stays stable regardless of source.
    table_rows: dict[str, str] = {}
    for mod in modules:
        table_rows[mod.name] = _escape_table_cell(mod.summary)
    for name, info in HAND_AUTHORED_MODULES.items():
        # Generator-owned pages win if a name somehow appears in both.
        table_rows.setdefault(name, _escape_table_cell(info["description"]))

    for name in sorted(table_rows):
        lines.append(f"| [`{name}`]({name}.md) | {table_rows[name]} |")
    lines.append("")

    return "\n".join(lines)


# ---------------------------------------------------------------------------
# mkdocs nav generation
# ---------------------------------------------------------------------------


def _all_module_nav_names(modules: list[DocModule]) -> list[str]:
    """Return the sorted set of module names that belong in the nav/index.

    Combines generator-owned modules with the hand-authored allowlist.
    """
    names = {m.name for m in modules}
    names.update(HAND_AUTHORED_MODULES.keys())
    return sorted(names)


def _replace_child_block(
    lines: list[str], parent_label: str, child_lines: list[str]
) -> tuple[list[str], bool]:
    """Replace the contiguous, more-indented child block under *parent_label*.

    *parent_label* is matched against a stripped line (e.g. ``"- Modules:"``).
    The block of following lines that are indented deeper than the parent is
    replaced wholesale with *child_lines*. The rest of the file is preserved
    byte-for-byte. Returns ``(new_lines, changed)``.

    This is a targeted text-region edit (not a YAML round-trip) so the rest of
    ``mkdocs.yml`` keeps its exact formatting and indentation.
    """
    out: list[str] = []
    i = 0
    n = len(lines)
    changed = False
    while i < n:
        line = lines[i]
        if line.strip() == parent_label:
            parent_indent = len(line) - len(line.lstrip())
            out.append(line)
            i += 1
            # Consume the existing child block (deeper-indented lines; blank
            # lines are tolerated only when a deeper line follows immediately).
            old_block: list[str] = []
            while i < n:
                cur = lines[i]
                if cur.strip() == "":
                    j = i + 1
                    if j < n:
                        nxt = lines[j]
                        nxt_indent = len(nxt) - len(nxt.lstrip())
                        if nxt.strip() != "" and nxt_indent > parent_indent:
                            old_block.append(cur)
                            i += 1
                            continue
                    break
                cur_indent = len(cur) - len(cur.lstrip())
                if cur_indent > parent_indent:
                    old_block.append(cur)
                    i += 1
                    continue
                break
            out.extend(child_lines)
            if old_block != child_lines:
                changed = True
            continue
        out.append(line)
        i += 1
    return out, changed


def build_nav_blocks(
    core_types: list[DocModule], modules: list[DocModule]
) -> dict[str, list[str]]:
    """Compute the desired mkdocs nav child blocks for stdlib.

    Returns a mapping of parent label -> list of child lines (with the 6-space
    indentation used under ``- Modules:`` / ``- Core Types:``).
    """
    core_block = [
        f"      - {ct.name}: stdlib/{ct.name}.md" for ct in core_types
    ]
    module_block = [
        f"      - {name}: stdlib/{name}.md"
        for name in _all_module_nav_names(modules)
    ]
    return {
        "- Core Types:": core_block,
        "- Modules:": module_block,
    }


def compute_mkdocs_nav(
    mkdocs_path: Path, core_types: list[DocModule], modules: list[DocModule]
) -> str:
    """Return what the full contents of *mkdocs_path* would be after a nav update.

    Does not write anything. Used by both ``update_mkdocs_nav`` and ``--check``.
    """
    text = mkdocs_path.read_text(encoding="utf-8")
    trailing_newline = text.endswith("\n")
    lines = text.split("\n")
    if trailing_newline:
        # Drop the final empty element from split so we can rejoin cleanly.
        lines = lines[:-1]

    blocks = build_nav_blocks(core_types, modules)
    for label, child_lines in blocks.items():
        lines, _changed = _replace_child_block(lines, label, child_lines)

    result = "\n".join(lines)
    if trailing_newline:
        result += "\n"
    return result


def update_mkdocs_nav(
    mkdocs_path: Path,
    core_types: list[DocModule],
    modules: list[DocModule],
    verbose: bool = False,
) -> bool:
    """Rewrite the stdlib nav child blocks in *mkdocs_path* in place.

    Only the ``- Modules:`` and ``- Core Types:`` child blocks are touched; the
    rest of the file is preserved exactly. Returns ``True`` if the file changed.
    Idempotent: running twice produces no further change.
    """
    if not mkdocs_path.exists():
        if verbose:
            print(f"  mkdocs file not found, skipping nav update: {mkdocs_path}")
        return False
    current = mkdocs_path.read_text(encoding="utf-8")
    desired = compute_mkdocs_nav(mkdocs_path, core_types, modules)
    if desired == current:
        if verbose:
            print(f"  mkdocs nav already up to date: {mkdocs_path.name}")
        return False
    mkdocs_path.write_text(desired, encoding="utf-8")
    if verbose:
        print(f"  Updated mkdocs nav: {mkdocs_path.name}")
    return True


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------


def generate(
    source_dir: Path,
    output_dir: Path,
    force: bool = False,
    verbose: bool = False,
    stdlib_dir: Optional[Path] = None,
    mkdocs_path: Optional[Path] = None,
    update_nav: bool = True,
) -> list[Path]:
    """Generate stdlib documentation pages.

    Returns list of generated file paths.
    """
    output_dir.mkdir(parents=True, exist_ok=True)
    generated: list[Path] = []

    if verbose:
        print(f"Source: {source_dir}")
        if stdlib_dir:
            print(f"Stdlib: {stdlib_dir}")
        print(f"Output: {output_dir}")
        print()

    # Discover
    if verbose:
        print("Discovering modules...")
    modules = discover_modules(source_dir)
    if stdlib_dir and stdlib_dir.exists():
        stdlib_modules = discover_modules(stdlib_dir)
        modules.extend(stdlib_modules)
        if verbose:
            print(f"  Found {len(stdlib_modules)} stdlib modules")
    modules.sort(key=lambda m: m.name)
    if verbose:
        print(f"  Found {len(modules)} modules total")

    if verbose:
        print("Discovering core types...")
    core_types = discover_core_types(source_dir)
    if verbose:
        print(f"  Found {len(core_types)} core types")

    if verbose:
        print("Discovering builtins...")
    builtins = discover_builtins(source_dir)
    if verbose:
        print(f"  Found {len(builtins.members)} builtin members")
        print()

    # Render builtins
    out_path = output_dir / "builtins.md"
    if force or not out_path.exists():
        out_path.write_text(render_module_page(builtins), encoding="utf-8")
        generated.append(out_path)
        if verbose:
            print(f"  Generated: {out_path.name}")

    # Render core types
    for ct in core_types:
        out_path = output_dir / f"{ct.name}.md"
        if force or not out_path.exists():
            out_path.write_text(render_module_page(ct), encoding="utf-8")
            generated.append(out_path)
            if verbose:
                print(f"  Generated: {out_path.name}")

    # Render modules. Hand-authored pages are never overwritten or generated,
    # even with --force; they are owned outside this tool.
    for mod in modules:
        if mod.name in HAND_AUTHORED_MODULES:
            if verbose:
                print(f"  Skipping hand-authored page: {mod.name}.md")
            continue
        out_path = output_dir / f"{mod.name}.md"
        if force or not out_path.exists():
            out_path.write_text(render_module_page(mod), encoding="utf-8")
            generated.append(out_path)
            if verbose:
                print(f"  Generated: {out_path.name}")

    # Render index (merges hand-authored modules into the Modules table).
    out_path = output_dir / "index.md"
    if force or not out_path.exists():
        out_path.write_text(
            render_index_page(builtins, core_types, modules),
            encoding="utf-8",
        )
        generated.append(out_path)
        if verbose:
            print(f"  Generated: {out_path.name}")

    # Update the mkdocs nav (only when requested; never for temp/check dirs —
    # the caller controls this via update_nav / mkdocs_path).
    if update_nav and mkdocs_path is not None:
        if update_mkdocs_nav(mkdocs_path, core_types, modules, verbose=verbose):
            generated.append(mkdocs_path)

    if verbose:
        print(f"\nDone. Generated {len(generated)} files.")

    return generated


# ---------------------------------------------------------------------------
# Drift detection (--check)
# ---------------------------------------------------------------------------


def check_docs(
    source_dir: Path,
    output_dir: Path,
    stdlib_dir: Optional[Path] = None,
    mkdocs_path: Optional[Path] = None,
) -> tuple[bool, list[str]]:
    """Check whether committed docs are in sync with what would be generated.

    Generates everything into a temporary directory (writing NOTHING to
    *output_dir* or *mkdocs_path*), then diffs:

      * each generator-owned ``*.md`` page (incl. ``index.md``) against the
        committed page in *output_dir*
      * the committed ``mkdocs.yml`` nav against what it would become

    Returns ``(up_to_date, messages)``. ``messages`` concisely describes every
    difference found (empty when in sync).
    """
    messages: list[str] = []

    tmp_root = Path(tempfile.mkdtemp(prefix="sharpy-doc-check-"))
    try:
        tmp_out = tmp_root / "stdlib"
        # Generate into the temp dir with force, no nav writing.
        generate(
            source_dir=source_dir,
            output_dir=tmp_out,
            force=True,
            verbose=False,
            stdlib_dir=stdlib_dir,
            update_nav=False,
        )

        # Compare generator-owned pages.
        generated_pages = sorted(p.name for p in tmp_out.glob("*.md"))
        for name in generated_pages:
            expected = (tmp_out / name).read_text(encoding="utf-8")
            committed_path = output_dir / name
            if not committed_path.exists():
                messages.append(
                    f"missing page: docs/stdlib/{name} (would be generated)"
                )
                continue
            actual = committed_path.read_text(encoding="utf-8")
            if actual != expected:
                messages.append(f"out-of-date page: docs/stdlib/{name}")

        # Note hand-authored pages that are missing entirely.
        for name in HAND_AUTHORED_MODULES:
            page = output_dir / f"{name}.md"
            if not page.exists():
                messages.append(
                    f"missing hand-authored page: docs/stdlib/{name}.md"
                )

        # Compare mkdocs nav.
        if mkdocs_path is not None and mkdocs_path.exists():
            current = mkdocs_path.read_text(encoding="utf-8")
            modules = discover_modules(source_dir)
            if stdlib_dir and stdlib_dir.exists():
                modules.extend(discover_modules(stdlib_dir))
            modules.sort(key=lambda m: m.name)
            core_types = discover_core_types(source_dir)
            desired = compute_mkdocs_nav(mkdocs_path, core_types, modules)
            if current != desired:
                messages.append(f"out-of-date nav: {mkdocs_path.name}")
    finally:
        shutil.rmtree(tmp_root, ignore_errors=True)

    return (len(messages) == 0, messages)
