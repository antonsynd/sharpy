#!/usr/bin/env python3
"""
Generate Sharpy standard library documentation from Sharpy.Core C# source.

Parses XML doc comments, method signatures, and attributes from C# files
and produces Markdown pages suitable for MkDocs Material.
"""

import re
import os
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional
from xml.etree import ElementTree


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
    text = re.sub(r"<[^>]+>", "", text)
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
        summary_match = re.search(
            r"<summary>(.*?)</summary>", xml_text, re.DOTALL
        )
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
    "GetEnumerator", "GetReverseEnumerator", "CompareTo",
    "GetHashCode", "Equals", "ToString", "Dispose",
    "TryGetValue", "ContainsKey",
}


def _is_operator(name: str) -> bool:
    return name.startswith("operator") and (
        len(name) <= 8 or not name[8].isalnum()
    )


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
            params.append(DocParam(
                name=pascal_to_snake(pname),
                type=map_type(ptype.strip()),
                default=default,
            ))
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


def parse_cs_file(
    filepath: Path,
    is_extension: bool = False,
    is_builtins: bool = False,
) -> list[DocMember]:
    """Parse a C# file and extract documented public members."""
    text = filepath.read_text(encoding="utf-8")
    lines = text.split("\n")
    members: list[DocMember] = []
    i = 0

    while i < len(lines):
        stripped = lines[i].strip()

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
                members.append(DocMember(
                    kind="constant",
                    name=pascal_to_snake(cname),
                    cs_name=cname,
                    signature="",
                    summary=doc.get("summary", ""),
                    return_type=map_type(ctype.strip()),
                    is_static=True,
                ))
            i = end_i + 1
            continue

        # Skip class/struct/interface/enum declarations
        if re.match(r"public\s+(?:(?:static|sealed|abstract|partial|readonly)\s+)*(?:class|struct|interface|enum)\s", joined):
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

            members.append(DocMember(
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
            ))
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
                    members.append(DocMember(
                        kind="property",
                        name=pascal_to_snake(pname),
                        cs_name=pname,
                        signature="",
                        summary=doc.get("summary", ""),
                        return_type=map_type(ptype.strip()),
                        is_static="static" in modifiers,
                    ))

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

            # Check if this is a SharpyModuleType
            file_text = cs_file.read_text(encoding="utf-8")
            type_match = re.search(r'\[SharpyModuleType\("([^"]+)"\)\]', file_text)
            if type_match:
                type_name = type_match.group(1)
                type_summary = _get_class_summary(cs_file)
                type_members = parse_cs_file(cs_file)
                all_types.append(DocType(
                    name=type_name,
                    cs_name=cs_file.stem,
                    summary=type_summary,
                    members=type_members,
                ))
            else:
                members = parse_cs_file(cs_file)
                all_members.extend(members)

        modules.append(DocModule(
            name=mod_name,
            kind="module",
            summary=summary,
            members=all_members,
            types=all_types,
        ))

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

        types.append(DocModule(
            name=type_name,
            kind="type",
            summary=summary,
            members=all_members,
        ))

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

    # Builtins/ subdirectory
    builtins_dir = core_dir / "Builtins"
    if builtins_dir.exists():
        for cs_file in sorted(builtins_dir.glob("*.cs")):
            if cs_file.name == "__Init__.cs":
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

    lines = ["## Constants", "", "| Name | Type | Description |",
             "|------|------|-------------|"]
    for c in constants:
        lines.append(f"| `{c.name}` | `{c.return_type}` | {c.summary} |")
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

    # Properties
    properties = [m for m in module.members if m.kind == "property"]
    if properties:
        lines.append("## Properties")
        lines.append("")
        lines.append("| Name | Type | Description |")
        lines.append("|------|------|-------------|")
        for p in properties:
            lines.append(f"| `{p.name}` | `{p.return_type}` | {p.summary} |")
        lines.append("")

    # Methods/Functions
    methods = [m for m in module.members if m.kind == "method"]
    if methods:
        section_title = "Functions" if module.kind in ("module", "builtins") else "Methods"
        lines.append(f"## {section_title}")
        lines.append("")

        prefix = ""
        if module.kind == "module":
            prefix = f"{module.name}."

        for m in methods:
            lines.append(_render_member(m, prefix=prefix))

    # Module types (e.g., ArgumentParser in argparse)
    for doc_type in module.types:
        lines.append(f"## {doc_type.name}")
        lines.append("")
        if doc_type.summary:
            lines.append(doc_type.summary)
            lines.append("")

        type_constants = [m for m in doc_type.members if m.kind == "constant"]
        if type_constants:
            lines.append(_render_constants_table(type_constants))

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
        f"[Built-in functions](builtins.md) available without any import: "
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
        desc = _one_line(ct.summary)
        lines.append(f"| [`{ct.name}`]({ct.name}.md) | {desc} |")
    lines.append("")

    lines.append("## Modules")
    lines.append("")
    lines.append("| Module | Description |")
    lines.append("|--------|-------------|")
    for mod in modules:
        desc = _one_line(mod.summary)
        lines.append(f"| [`{mod.name}`]({mod.name}.md) | {desc} |")
    lines.append("")

    return "\n".join(lines)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def generate(
    source_dir: Path,
    output_dir: Path,
    force: bool = False,
    verbose: bool = False,
) -> list[Path]:
    """Generate stdlib documentation pages.

    Returns list of generated file paths.
    """
    output_dir.mkdir(parents=True, exist_ok=True)
    generated: list[Path] = []

    if verbose:
        print(f"Source: {source_dir}")
        print(f"Output: {output_dir}")
        print()

    # Discover
    if verbose:
        print("Discovering modules...")
    modules = discover_modules(source_dir)
    if verbose:
        print(f"  Found {len(modules)} modules")

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

    # Render modules
    for mod in modules:
        out_path = output_dir / f"{mod.name}.md"
        if force or not out_path.exists():
            out_path.write_text(render_module_page(mod), encoding="utf-8")
            generated.append(out_path)
            if verbose:
                print(f"  Generated: {out_path.name}")

    # Render index
    out_path = output_dir / "index.md"
    if force or not out_path.exists():
        out_path.write_text(
            render_index_page(builtins, core_types, modules),
            encoding="utf-8",
        )
        generated.append(out_path)
        if verbose:
            print(f"  Generated: {out_path.name}")

    if verbose:
        print(f"\nDone. Generated {len(generated)} files.")

    return generated
