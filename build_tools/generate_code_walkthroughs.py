#!/usr/bin/env python3
"""
Generate internal code walkthrough documentation using AI CLI tools.

This script analyzes C# source files from Sharpy.Cli and Sharpy.Compiler projects,
using multiple parallel instances of an AI CLI tool (GitHub Copilot or Claude Code)
to generate markdown documentation that helps newcomers understand the codebase.

Supported CLI tools:
- GitHub Copilot CLI (copilot): Uses explicit tool permissions
- Claude Code CLI (claude): Uses explicit tool permissions

SECURITY MODEL:
- Read access: AI can read files in the repository (needed to analyze source)
- Write access: AI can create new files (needed to write documentation)
- NO shell/bash access: AI cannot execute arbitrary commands
- NO edit access: AI cannot modify existing files (only create new ones)
- NO delete access: AI cannot remove files

The AI tools are explicitly restricted to 'Read' and 'Write' operations only.
This prevents accidental or malicious file deletion, code modification, or
arbitrary command execution.

Working directory: Script runs from repository root for path resolution.
"""

import argparse
import asyncio
import json
import re
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path
from typing import List, Set, Optional, Dict, Any, Tuple
from dataclasses import dataclass
from datetime import timedelta


class RateLimitExceeded(Exception):
    """Exception raised when all backends are rate limited."""

    def __init__(
        self, message: str, wait_time: Optional[float] = None, backend: str = ""
    ):
        super().__init__(message)
        self.wait_time = wait_time  # Seconds to wait before retrying
        self.backend = backend


class BackendUnavailable(Exception):
    """Exception raised when a specific backend is unavailable."""

    def __init__(self, message: str, backend: str, wait_time: Optional[float] = None):
        super().__init__(message)
        self.backend = backend
        self.wait_time = wait_time


# =============================================================================
# Backend Management with Failover
# =============================================================================


@dataclass
class BackendState:
    """Tracks state for a backend."""

    name: str
    enabled: bool = True
    available: bool = True
    consecutive_errors: int = 0
    last_error: Optional[str] = None
    disabled_until: Optional[float] = None  # Unix timestamp

    def is_available(self) -> bool:
        """Check if backend is currently available."""
        if not self.enabled:
            return False
        if self.disabled_until and time.time() < self.disabled_until:
            return False
        return self.available

    def disable_temporarily(self, seconds: float, reason: str) -> None:
        """Temporarily disable this backend."""
        self.disabled_until = time.time() + seconds
        self.available = False
        self.last_error = reason

    def get_wait_time(self) -> Optional[float]:
        """Get remaining wait time if disabled."""
        if self.disabled_until:
            remaining = self.disabled_until - time.time()
            return remaining if remaining > 0 else None
        return None


class BackendManager:
    """Manages multiple CLI backends with automatic failover.

    Priority order: Claude Code → GitHub Copilot
    Falls back to the next backend if one is rate limited or unavailable.
    """

    # Backend priority order (first = preferred)
    BACKEND_PRIORITY = ["claude", "copilot"]

    def __init__(self):
        self.backends: Dict[str, BackendState] = {
            "claude": BackendState(name="claude"),
            "copilot": BackendState(name="copilot"),
        }
        self._check_backend_availability()

    def _check_backend_availability(self) -> None:
        """Check which backends are installed and available."""
        # Check Claude Code CLI
        try:
            result = subprocess.run(
                ["claude", "--version"],
                capture_output=True,
                timeout=5,
            )
            self.backends["claude"].available = result.returncode == 0
            if result.returncode == 0:
                print(f"✓ Claude Code CLI available")
        except (FileNotFoundError, subprocess.TimeoutExpired):
            self.backends["claude"].available = False
            self.backends["claude"].last_error = "CLI not found"
            print(f"✗ Claude Code CLI not available")

        # Check GitHub Copilot CLI
        try:
            # Check for standalone copilot CLI
            result = subprocess.run(
                ["/opt/homebrew/bin/copilot", "--version"],
                capture_output=True,
                timeout=5,
            )
            self.backends["copilot"].available = result.returncode == 0
            if result.returncode == 0:
                print(f"✓ GitHub Copilot CLI available")
        except (FileNotFoundError, subprocess.TimeoutExpired):
            self.backends["copilot"].available = False
            self.backends["copilot"].last_error = "CLI not found"
            print(f"✗ GitHub Copilot CLI not available")

    def get_available_backend(self) -> Optional[str]:
        """Get the best available backend based on priority."""
        for backend_name in self.BACKEND_PRIORITY:
            if backend_name in self.backends:
                state = self.backends[backend_name]
                if state.is_available():
                    return backend_name
        return None

    def mark_rate_limited(
        self, backend: str, wait_time: Optional[float] = None
    ) -> None:
        """Mark a backend as rate limited."""
        if backend in self.backends:
            state = self.backends[backend]
            # Default wait time: 5 minutes if not specified
            wait = wait_time if wait_time else 300.0
            state.disable_temporarily(wait, f"Rate limited (wait {wait:.0f}s)")
            print(f"⚠ {backend} rate limited, disabled for {wait:.0f}s")

    def mark_error(self, backend: str, error: str) -> None:
        """Mark a backend as having an error."""
        if backend in self.backends:
            state = self.backends[backend]
            state.consecutive_errors += 1
            state.last_error = error
            # After 3 consecutive errors, disable for 60 seconds
            if state.consecutive_errors >= 3:
                state.disable_temporarily(60.0, f"Too many errors: {error}")

    def mark_success(self, backend: str) -> None:
        """Mark a successful execution for a backend."""
        if backend in self.backends:
            state = self.backends[backend]
            state.consecutive_errors = 0
            state.available = True

    def get_status(self) -> Dict[str, Any]:
        """Get status of all backends."""
        return {
            name: {
                "available": state.is_available(),
                "enabled": state.enabled,
                "consecutive_errors": state.consecutive_errors,
                "last_error": state.last_error,
                "wait_time": state.get_wait_time(),
            }
            for name, state in self.backends.items()
        }

    def all_exhausted(self) -> Tuple[bool, Optional[float]]:
        """Check if all backends are exhausted and return min wait time."""
        available = any(s.is_available() for s in self.backends.values())
        if available:
            return False, None

        # Calculate minimum wait time across all backends
        wait_times = [
            s.get_wait_time()
            for s in self.backends.values()
            if s.get_wait_time() is not None
        ]
        min_wait = min(wait_times) if wait_times else None
        return True, min_wait


def extract_rate_limit_wait_time(output: str) -> Optional[float]:
    """
    Extract wait time from rate limit error messages.

    Looks for patterns like:
    - "resets 2am" / "resets at 2am"
    - "try again in X minutes/hours"
    - "wait X seconds"
    - "retry after X"
    """
    output_lower = output.lower()

    # Pattern: "try again in X minutes/hours/seconds"
    match = re.search(r"try again in (\d+)\s*(second|minute|hour)s?", output_lower)
    if match:
        value = int(match.group(1))
        unit = match.group(2)
        if unit == "hour":
            return value * 3600
        elif unit == "minute":
            return value * 60
        else:
            return float(value)

    # Pattern: "wait X seconds/minutes"
    match = re.search(r"wait (\d+)\s*(second|minute)s?", output_lower)
    if match:
        value = int(match.group(1))
        unit = match.group(2)
        return value * 60 if unit == "minute" else float(value)

    # Pattern: "retry after X seconds"
    match = re.search(r"retry after (\d+)", output_lower)
    if match:
        return float(match.group(1))

    # Pattern: "resets (at)? Xam/pm" - calculate time until reset
    match = re.search(r"resets\s*(?:at\s*)?(\d{1,2})([ap]m)", output_lower)
    if match:
        reset_hour = int(match.group(1))
        is_pm = match.group(2) == "pm"
        if is_pm and reset_hour != 12:
            reset_hour += 12
        elif not is_pm and reset_hour == 12:
            reset_hour = 0

        now = datetime.now()
        reset_time = now.replace(hour=reset_hour, minute=0, second=0, microsecond=0)
        if reset_time <= now:
            # Reset is tomorrow - use timedelta to handle month boundaries correctly
            reset_time = reset_time + timedelta(days=1)

        wait_seconds = (reset_time - now).total_seconds()
        return max(wait_seconds, 60.0)  # At least 60 seconds

    # Pattern: X-minute/hour window
    match = re.search(r"(\d+)[- ](minute|hour)\s*window", output_lower)
    if match:
        value = int(match.group(1))
        unit = match.group(2)
        return value * 3600 if unit == "hour" else value * 60

    return None


def is_rate_limit_error(output: str) -> bool:
    """Check if output indicates a rate limit error."""
    indicators = [
        "rate_limited",
        "rate limit",
        "rate-limit",
        "429",
        "too many requests",
        "exceeded your copilot token usage",
        "hit your limit",
        "quota exceeded",
        "resets 2am",
        "resets at",
        "try again later",
        "request limit",
    ]
    output_lower = output.lower()
    return any(indicator in output_lower for indicator in indicators)


@dataclass
class Config:
    """Configuration for the walkthrough generator."""

    parallel_instances: int = 3
    timeout_between_batches: int = 60  # seconds
    copilot_timeout: int = 120  # seconds per file
    source_dirs: Optional[List[str]] = None
    output_dir: str = "docs/implementation_walkthrough"
    cli_provider: str = "auto"  # "auto", "copilot", or "claude"
    force_regenerate: bool = False  # regenerate all docs regardless of timestamps
    execution_log_path: str = "docs/implementation_walkthrough/execution_log.jsonl"
    spec_dir: str = "docs/language_specification"

    def __post_init__(self):
        if self.source_dirs is None:
            self.source_dirs = ["src/Sharpy.Cli", "src/Sharpy.Compiler"]
        if self.cli_provider not in ("copilot", "claude", "auto"):
            raise ValueError(
                f"Invalid CLI provider: {self.cli_provider}. Must be 'copilot', 'claude', or 'auto'."
            )


def find_cs_files(base_dir: Path, source_dirs: List[str]) -> List[Path]:
    """Find all C# files in the specified source directories."""
    cs_files = []
    for source_dir in source_dirs:
        full_path = base_dir / source_dir
        if not full_path.exists():
            print(f"Warning: Directory {full_path} does not exist", file=sys.stderr)
            continue

        # Find all .cs files recursively
        for cs_file in full_path.rglob("*.cs"):
            # Skip obj and bin directories
            if any(part in ["obj", "bin"] for part in cs_file.parts):
                continue
            cs_files.append(cs_file)

    return sorted(cs_files)


def get_output_path(
    cs_file: Path, base_dir: Path, source_dirs: List[str], output_dir: Path
) -> Path:
    """
    Calculate the output markdown path for a given C# file.

    Preserves the directory structure relative to the source directory root.
    """
    # Find which source directory this file belongs to
    relative_path = cs_file.relative_to(base_dir)

    for source_dir in source_dirs:
        source_path = Path(source_dir)
        try:
            # Try to make path relative to this source directory
            rel_to_source = relative_path.relative_to(source_path)
            # Change extension to .md
            md_filename = rel_to_source.with_suffix(".md")
            return output_dir / source_path / md_filename
        except ValueError:
            # Not relative to this source directory, try next
            continue

    # Fallback: shouldn't happen if find_cs_files is correct
    return output_dir / relative_path.with_suffix(".md")


def find_existing_docs(output_dir: Path) -> Set[Path]:
    """Find all existing markdown documentation files."""
    if not output_dir.exists():
        return set()

    return set(output_dir.rglob("*.md"))


def is_doc_stale(source_file: Path, doc_file: Path) -> bool:
    """
    Check if documentation is stale (source file is newer than doc file).

    Args:
        source_file: Path to the source .cs file
        doc_file: Path to the documentation .md file

    Returns:
        True if the doc file doesn't exist or is older than the source file
    """
    if not doc_file.exists():
        return True

    source_mtime = source_file.stat().st_mtime
    doc_mtime = doc_file.stat().st_mtime

    return source_mtime > doc_mtime


# =============================================================================
# Component-Specific Context
# =============================================================================

# Map directory paths to compiler pipeline components with descriptions
COMPONENT_CONTEXT: Dict[str, Dict[str, str]] = {
    "Lexer": {
        "role": "Lexical Analysis (Tokenization)",
        "description": "Converts raw source text into tokens. First phase of the compiler pipeline.",
        "upstream": "Raw .spy source files",
        "downstream": "Parser",
        "key_concepts": "Token types, lexer states, whitespace handling, indentation tracking",
    },
    "Parser": {
        "role": "Syntactic Analysis (AST Construction)",
        "description": "Converts token stream into Abstract Syntax Tree (AST). Implements recursive descent parsing.",
        "upstream": "Lexer (token stream)",
        "downstream": "Semantic Analysis",
        "key_concepts": "AST nodes, expression parsing, statement parsing, operator precedence",
    },
    "Parser/Ast": {
        "role": "AST Node Definitions",
        "description": "Defines the Abstract Syntax Tree node types used throughout the compiler.",
        "upstream": "Parser creates these nodes",
        "downstream": "Semantic analysis and CodeGen consume these nodes",
        "key_concepts": "Record types, visitor pattern, expression vs statement nodes",
    },
    "Semantic": {
        "role": "Semantic Analysis",
        "description": "Performs name resolution, type checking, and type inference. Multi-pass analysis.",
        "upstream": "Parser (AST)",
        "downstream": "CodeGen (RoslynEmitter)",
        "key_concepts": "SymbolTable, scopes, type inference, type narrowing, SemanticInfo",
    },
    "CodeGen": {
        "role": "Code Generation",
        "description": "Transforms typed AST into C# using Roslyn syntax trees. Final compiler phase.",
        "upstream": "Semantic Analysis (typed AST + SemanticInfo)",
        "downstream": "C# source code → .NET compilation",
        "key_concepts": "RoslynEmitter, SyntaxFactory, TypeMapper, name mangling (snake_case → PascalCase)",
    },
}

# Map keywords in filenames to related specification documents
SPEC_KEYWORD_MAP: Dict[str, List[str]] = {
    # Type system
    "type": [
        "type_annotations.md",
        "type_hierarchy.md",
        "type_narrowing.md",
        "type_casting.md",
    ],
    "generic": ["generics.md", "generic_variance.md"],
    "nullable": ["nullable_types.md", "none_literal.md"],
    "none": ["none_literal.md", "nullable_types.md", "type_narrowing.md"],
    # Control flow
    "if": ["if_statement.md", "type_narrowing.md"],
    "for": ["for_statement.md", "loop_else.md", "break_continue.md"],
    "while": ["while_statement.md", "loop_else.md", "break_continue.md"],
    "match": ["match_statement.md"],
    "try": ["exception_handling.md", "try_expressions.md"],
    # Functions and classes
    "function": [
        "function_definition.md",
        "function_parameters.md",
        "function_types.md",
        "lambdas.md",
    ],
    "lambda": ["lambdas.md", "function_types.md"],
    "class": ["classes.md", "constructors.md", "inheritance.md", "class_methods.md"],
    "method": ["class_methods.md", "static_methods.md", "dunder_methods.md"],
    "property": [
        "properties.md",
        "properties_function_style.md",
        "properties_inheritance.md",
    ],
    "decorator": ["decorators.md"],
    # Expressions and operators
    "expression": ["expressions.md", "operator_precedence.md"],
    "operator": [
        "operator_precedence.md",
        "operator_overloading.md",
        "arithmetic_operators.md",
    ],
    "binary": [
        "arithmetic_operators.md",
        "comparison_operators.md",
        "logical_operators.md",
    ],
    "unary": ["arithmetic_operators.md", "logical_operators.md"],
    # Lexer/Parser specific
    "lexer": [
        "lexer_implementation.md",
        "indentation.md",
        "keywords.md",
        "identifiers.md",
    ],
    "token": ["lexer_implementation.md", "keywords.md", "string_literals.md"],
    "literal": [
        "string_literals.md",
        "integer_literals.md",
        "float_literals.md",
        "boolean_literals.md",
    ],
    "string": [
        "string_literals.md",
        "string_type.md",
        "string_operators.md",
        "fstrings.md",
    ],
    # Collections
    "list": ["collection_types.md", "comprehensions.md"],
    "dict": ["collection_types.md", "comprehensions.md"],
    "set": ["collection_types.md", "comprehensions.md"],
    "tuple": ["named_tuples.md", "collection_types.md"],
    # .NET interop
    "roslyn": ["dotnet_interop.md"],
    "emit": ["dotnet_interop.md"],
    "interop": ["dotnet_interop.md"],
    # Module system
    "import": ["import_statements.md", "module_system.md", "module_resolution.md"],
    "module": ["module_system.md", "module_resolution.md"],
    "symbol": ["variable_scoping.md", "identifiers.md"],
    "scope": ["variable_scoping.md"],
    "name": ["name_mangling.md", "naming_conventions.md", "identifiers.md"],
}


def get_component_context(cs_file: Path, base_dir: Path) -> Optional[Dict[str, str]]:
    """
    Get component-specific context for a C# file based on its directory.

    Returns context dict with role, description, upstream/downstream info.
    """
    try:
        relative_path = cs_file.relative_to(base_dir)
        parts = relative_path.parts

        # Look for matching component in path (check longer paths first)
        for component_path in sorted(COMPONENT_CONTEXT.keys(), key=len, reverse=True):
            if component_path in "/".join(parts):
                return COMPONENT_CONTEXT[component_path]
    except ValueError:
        pass

    return None


def find_related_specs(cs_file: Path, base_dir: Path, spec_dir: str) -> List[str]:
    """
    Find related specification documents based on the C# filename.

    Returns list of relative paths to relevant spec documents.
    """
    filename_lower = cs_file.stem.lower()
    related_specs: Set[str] = set()

    # Check each keyword against the filename
    for keyword, specs in SPEC_KEYWORD_MAP.items():
        if keyword in filename_lower:
            for spec in specs:
                spec_path = base_dir / spec_dir / spec
                if spec_path.exists():
                    related_specs.add(f"{spec_dir}/{spec}")

    return sorted(related_specs)[:5]  # Limit to top 5 most relevant


def extract_dependencies(cs_file: Path) -> Dict[str, List[str]]:
    """
    Extract using statements and namespace from a C# file.

    Returns dict with 'usings' (import statements) and 'namespace'.
    """
    usings: List[str] = []
    namespace: Optional[str] = None

    try:
        content = cs_file.read_text(encoding="utf-8")

        # Extract using statements
        using_pattern = re.compile(r"^using\s+([\w.]+)\s*;", re.MULTILINE)
        usings = using_pattern.findall(content)

        # Extract namespace
        ns_pattern = re.compile(r"^namespace\s+([\w.]+)", re.MULTILINE)
        ns_match = ns_pattern.search(content)
        if ns_match:
            namespace = ns_match.group(1)

    except Exception:
        pass  # Silently ignore read errors

    return {
        "usings": [u for u in usings if u.startswith("Sharpy")],  # Only Sharpy deps
        "namespace": namespace,
    }


def log_execution(
    log_path: Path,
    event_type: str,
    cs_file: Path,
    output_path: Path,
    success: bool,
    duration: float,
    error: Optional[str] = None,
    is_stale: bool = False,
    extra: Optional[Dict[str, Any]] = None,
) -> None:
    """
    Append an execution event to the JSONL log file.

    Args:
        log_path: Path to the JSONL log file
        event_type: Type of event (e.g., 'generate', 'skip', 'error')
        cs_file: Path to the source file
        output_path: Path to the output documentation file
        success: Whether the operation succeeded
        duration: Duration in seconds
        error: Optional error message
        is_stale: Whether this was a stale doc regeneration
        extra: Optional additional metadata
    """
    log_entry = {
        "timestamp": datetime.now().isoformat(),
        "event_type": event_type,
        "source_file": str(cs_file),
        "output_file": str(output_path),
        "success": success,
        "duration_seconds": round(duration, 2),
        "is_stale_regeneration": is_stale,
    }

    if error:
        log_entry["error"] = error
    if extra:
        log_entry.update(extra)

    # Ensure log directory exists
    log_path.parent.mkdir(parents=True, exist_ok=True)

    # Append to JSONL file
    with open(log_path, "a", encoding="utf-8") as f:
        f.write(json.dumps(log_entry) + "\n")


def _build_cli_command(cli_provider: str, prompt: str) -> List[str]:
    """
    Build the command line arguments for the specified CLI provider.

    SECURITY: Only 'Read' and 'Write' tools are allowed.
    - Read: Allows reading source files for analysis
    - Write: Allows creating new markdown documentation files

    NOT allowed (for safety):
    - Bash/shell: No arbitrary command execution
    - Edit: No modification of existing files
    - Delete: No file removal
    - Other tools that could have side effects

    Args:
        cli_provider: Either "copilot" or "claude"
        prompt: The prompt to send to the AI

    Returns:
        List of command arguments
    """
    if cli_provider == "copilot":
        # GitHub Copilot CLI: explicitly allow only read and write tools
        # This prevents shell access, file deletion, or editing existing files
        return [
            "/opt/homebrew/bin/copilot",
            "--prompt",
            prompt,
            "--allow-tool",
            "read",
            "--allow-tool",
            "write",
        ]
    elif cli_provider == "claude":
        # Claude Code CLI: explicitly allow only Read and Write tools
        # Do NOT use --dangerously-skip-permissions as it bypasses all safety checks
        # Only Read and Write are allowed - no Bash, Edit, or other tools
        return [
            "claude",
            "--print",
            "--allowedTools",
            "Read,Write",
            "--prompt",
            prompt,
        ]
    else:
        raise ValueError(f"Unknown CLI provider: {cli_provider}")


async def analyze_file_with_cli(
    cs_file: Path,
    output_path: Path,
    base_dir: Path,
    timeout: int,
    cli_provider: str,
    config: Config,
    is_stale: bool = False,
    backend_manager: Optional[BackendManager] = None,
) -> Tuple[bool, Optional[str]]:
    """
    Analyze a single C# file using the specified AI CLI tool.

    Args:
        cs_file: Path to the C# source file
        output_path: Path where the markdown documentation should be written
        base_dir: Base directory of the repository
        timeout: Timeout in seconds for the CLI command
        cli_provider: Either "copilot", "claude", or "auto"
        config: Configuration object with spec_dir and log path
        is_stale: Whether this is a stale doc regeneration
        backend_manager: Optional backend manager for failover support

    Returns:
        Tuple of (success: bool, used_backend: Optional[str])
        Raises BackendUnavailable if a specific backend fails (for failover)
        Raises RateLimitExceeded if all backends are exhausted
    """
    print(f"Analyzing: {cs_file}")
    start_time = time.time()
    log_path = base_dir / config.execution_log_path

    try:
        # Create output directory if needed
        output_path.parent.mkdir(parents=True, exist_ok=True)

        # Use relative paths for better readability
        relative_cs_file = cs_file.relative_to(base_dir)
        relative_output = output_path.relative_to(base_dir)

        # Gather rich context
        component_ctx = get_component_context(cs_file, base_dir)
        related_specs = find_related_specs(cs_file, base_dir, config.spec_dir)
        deps = extract_dependencies(cs_file)

        # Build the context sections
        context_sections = []

        # Component context
        if component_ctx:
            context_sections.append(
                f"""## Compiler Pipeline Context
**Component**: {component_ctx['role']}
**Description**: {component_ctx['description']}
**Upstream**: {component_ctx['upstream']}
**Downstream**: {component_ctx['downstream']}
**Key Concepts**: {component_ctx['key_concepts']}"""
            )

        # Dependencies
        if deps["usings"]:
            deps_list = "\n".join(f"- `{u}`" for u in deps["usings"][:10])
            context_sections.append(
                f"""## Internal Dependencies
This file imports from these Sharpy namespaces:
{deps_list}"""
            )

        # Related specifications
        if related_specs:
            specs_list = "\n".join(f"- `{s}`" for s in related_specs)
            context_sections.append(
                f"""## Related Specification Documents
The following specs may be relevant to understanding this file:
{specs_list}"""
            )

        # Combine context
        rich_context = "\n\n".join(context_sections) if context_sections else ""

        # Build the prompt
        prompt = f"""Read the C# source file '{relative_cs_file}' and create a comprehensive walkthrough document for a newcomer engineer joining the Sharpy compiler project.

{rich_context}

## Architecture Overview
The Sharpy compiler pipeline flows: Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → RoslynEmitter → C#

## Task
Create a walkthrough document covering:

1. **Overview**: Brief summary of what this file does and its role in the compiler pipeline
2. **Class/Type Structure**: Explain the main classes, interfaces, structs, or enums defined
3. **Key Functions/Methods**: Walk through important methods:
   - What each method does
   - Key parameters and return values
   - Important implementation details or algorithms
   - How it connects to upstream/downstream components
4. **Dependencies**: Note important dependencies on other parts of the codebase
5. **Patterns and Design Decisions**: Highlight design patterns, architectural decisions, or coding conventions
6. **Debugging Tips**: Insights that would help someone debug issues in this code
7. **Contribution Guidelines**: What kinds of changes might be made to this file

Write the walkthrough as a well-structured markdown document to '{relative_output}' with:
- A header showing "# Walkthrough: {cs_file.name}" and "**Source File**: `{relative_cs_file}`" followed by a separator (---)
- Clear headings and subheadings
- Code snippets where helpful (use ```csharp blocks)
- Bullet points for lists
- Emphasis on readability for someone new to the codebase

Focus on providing intuition and understanding, not just restating what the code does line-by-line."""

        # Build the command for the specified CLI provider
        cmd = _build_cli_command(cli_provider, prompt)

        # Call the AI CLI in programmatic mode with write permissions
        # Change to the base directory so relative paths work
        # Using create_subprocess_exec (not shell) so the prompt argument is safely passed
        # without any shell interpretation - no escaping needed
        process = await asyncio.create_subprocess_exec(
            *cmd,
            cwd=str(base_dir),
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,
        )

        try:
            stdout, stderr = await asyncio.wait_for(
                process.communicate(), timeout=timeout
            )
            duration = time.time() - start_time

            if process.returncode != 0:
                stderr_text = stderr.decode("utf-8")
                stdout_text = stdout.decode("utf-8")
                combined_output = stderr_text + stdout_text

                # Check for rate limiting errors
                if is_rate_limit_error(combined_output):
                    wait_time = extract_rate_limit_wait_time(combined_output)
                    log_execution(
                        log_path,
                        "rate_limit",
                        cs_file,
                        output_path,
                        success=False,
                        duration=duration,
                        error="Rate limit exceeded",
                        is_stale=is_stale,
                        extra={
                            "backend": cli_provider,
                            "wait_time": wait_time,
                        },
                    )

                    # Format wait time message
                    if wait_time:
                        if wait_time >= 3600:
                            wait_str = f"{wait_time / 3600:.1f} hours"
                        elif wait_time >= 60:
                            wait_str = f"{wait_time / 60:.0f} minutes"
                        else:
                            wait_str = f"{wait_time:.0f} seconds"
                        print(
                            f"⚠ {cli_provider} rate limited (wait ~{wait_str})",
                            file=sys.stderr,
                        )
                    else:
                        print(
                            f"⚠ {cli_provider} rate limited",
                            file=sys.stderr,
                        )

                    # Raise BackendUnavailable so the caller can try another backend
                    raise BackendUnavailable(
                        f"Rate limit exceeded for {cli_provider}",
                        backend=cli_provider,
                        wait_time=wait_time,
                    )

                log_execution(
                    log_path,
                    "error",
                    cs_file,
                    output_path,
                    success=False,
                    duration=duration,
                    error=stderr_text[:500],
                    is_stale=is_stale,
                    extra={"backend": cli_provider},
                )
                print(
                    f"Error analyzing {cs_file}: {stderr_text}",
                    file=sys.stderr,
                )
                return False, cli_provider

            # Check if the output file was created
            if output_path.exists():
                log_execution(
                    log_path,
                    "generate",
                    cs_file,
                    output_path,
                    success=True,
                    duration=duration,
                    is_stale=is_stale,
                    extra={"backend": cli_provider},
                )
                print(f"✓ Generated: {output_path} (via {cli_provider})")
                return True, cli_provider
            else:
                log_execution(
                    log_path,
                    "missing_output",
                    cs_file,
                    output_path,
                    success=False,
                    duration=duration,
                    error="CLI completed but output file not created",
                    is_stale=is_stale,
                    extra={"backend": cli_provider},
                )
                print(
                    f"Warning: CLI completed but output file not found: {output_path}",
                    file=sys.stderr,
                )
                # Print CLI's response for debugging
                if stdout:
                    print(
                        f"CLI output: {stdout.decode('utf-8')[:500]}",
                        file=sys.stderr,
                    )
                return False, cli_provider

        except asyncio.TimeoutError:
            duration = time.time() - start_time
            log_execution(
                log_path,
                "timeout",
                cs_file,
                output_path,
                success=False,
                duration=duration,
                error=f"Timeout after {timeout}s",
                is_stale=is_stale,
                extra={"backend": cli_provider},
            )
            print(f"Timeout analyzing {cs_file}", file=sys.stderr)
            process.kill()
            await process.wait()
            return False, cli_provider

    except BackendUnavailable:
        # Re-raise for failover handling
        raise
    except Exception as e:
        duration = time.time() - start_time
        log_execution(
            log_path,
            "exception",
            cs_file,
            output_path,
            success=False,
            duration=duration,
            error=str(e),
            is_stale=is_stale,
            extra={"backend": cli_provider},
        )
        print(f"Exception analyzing {cs_file}: {e}", file=sys.stderr)
        return False, cli_provider


async def analyze_file_with_failover(
    cs_file: Path,
    output_path: Path,
    base_dir: Path,
    timeout: int,
    config: Config,
    is_stale: bool,
    backend_manager: BackendManager,
) -> Tuple[bool, Optional[str]]:
    """
    Analyze a file with automatic backend failover.

    Tries backends in priority order (Claude → Copilot) until one succeeds
    or all are exhausted.

    Returns:
        Tuple of (success: bool, used_backend: Optional[str])
        Raises RateLimitExceeded if all backends are exhausted
    """
    tried_backends: List[str] = []

    for backend_name in BackendManager.BACKEND_PRIORITY:
        state = backend_manager.backends.get(backend_name)
        if not state or not state.is_available():
            continue

        tried_backends.append(backend_name)

        try:
            success, used = await analyze_file_with_cli(
                cs_file=cs_file,
                output_path=output_path,
                base_dir=base_dir,
                timeout=timeout,
                cli_provider=backend_name,
                config=config,
                is_stale=is_stale,
                backend_manager=backend_manager,
            )

            if success:
                backend_manager.mark_success(backend_name)
                return True, used
            else:
                # Non-rate-limit failure, continue to next backend
                backend_manager.mark_error(backend_name, "Execution failed")

        except BackendUnavailable as e:
            # Rate limited or unavailable, mark and try next
            backend_manager.mark_rate_limited(e.backend, e.wait_time)
            continue

    # All backends exhausted
    exhausted, min_wait = backend_manager.all_exhausted()
    if exhausted:
        if min_wait:
            if min_wait >= 3600:
                wait_str = f"{min_wait / 3600:.1f} hours"
            elif min_wait >= 60:
                wait_str = f"{min_wait / 60:.0f} minutes"
            else:
                wait_str = f"{min_wait:.0f} seconds"
            raise RateLimitExceeded(
                f"All backends rate limited. Try again in ~{wait_str}",
                wait_time=min_wait,
                backend="all",
            )
        else:
            raise RateLimitExceeded(
                "All backends unavailable",
                wait_time=None,
                backend="all",
            )

    # Some backends had non-rate-limit failures
    return False, tried_backends[-1] if tried_backends else None


async def process_batch(
    files_batch: List[tuple],
    config: Config,
    base_dir: Path,
    backend_manager: BackendManager,
) -> int:
    """
    Process a batch of files in parallel with backend failover.

    Returns the number of successfully processed files.
    Raises RateLimitExceeded if all backends are exhausted.
    """
    if config.cli_provider == "auto":
        # Use failover mode
        tasks = [
            analyze_file_with_failover(
                cs_file=cs_file,
                output_path=output_path,
                base_dir=base_dir,
                timeout=config.copilot_timeout,
                config=config,
                is_stale=is_stale,
                backend_manager=backend_manager,
            )
            for cs_file, output_path, is_stale in files_batch
        ]
    else:
        # Use specific backend
        tasks = [
            analyze_file_with_cli(
                cs_file=cs_file,
                output_path=output_path,
                base_dir=base_dir,
                timeout=config.copilot_timeout,
                cli_provider=config.cli_provider,
                config=config,
                is_stale=is_stale,
                backend_manager=backend_manager,
            )
            for cs_file, output_path, is_stale in files_batch
        ]

    # Use return_exceptions=True to handle individual failures
    results = await asyncio.gather(*tasks, return_exceptions=True)

    # Check if any task raised RateLimitExceeded (all backends exhausted)
    for result in results:
        if isinstance(result, RateLimitExceeded):
            raise result

    # Count successful results
    # Results are tuples (success, backend) or exceptions
    success_count = 0
    for r in results:
        if isinstance(r, tuple) and r[0] is True:
            success_count += 1
        elif isinstance(r, BackendUnavailable):
            # Single backend failed but we're in specific mode
            pass

    return success_count


async def main_async(config: Config):
    """Main async processing function."""
    # Find the repository root (where sharpy.sln is located)
    script_dir = Path(__file__).parent.parent
    base_dir = script_dir

    # Ensure source_dirs is set
    if config.source_dirs is None:
        config.source_dirs = ["src/Sharpy.Cli", "src/Sharpy.Compiler"]

    # Initialize backend manager
    print("Checking backend availability...")
    backend_manager = BackendManager()
    print()

    # Determine effective CLI provider
    if config.cli_provider == "auto":
        available_backend = backend_manager.get_available_backend()
        if available_backend:
            print(f"Auto mode: Will use {available_backend} (with failover)")
        else:
            print("Error: No backends available", file=sys.stderr)
            sys.exit(1)
    else:
        # Check if the specific backend is available
        state = backend_manager.backends.get(config.cli_provider)
        if not state or not state.is_available():
            print(
                f"Error: {config.cli_provider} backend is not available",
                file=sys.stderr,
            )
            sys.exit(1)

    print(f"Repository root: {base_dir}")
    print(f"CLI provider: {config.cli_provider}")
    print(f"Source directories: {config.source_dirs}")
    print(f"Output directory: {config.output_dir}")
    print(f"Parallel instances: {config.parallel_instances}")
    print(f"Timeout between batches: {config.timeout_between_batches}s")
    print(f"Force regenerate: {config.force_regenerate}")
    print(f"Execution log: {config.execution_log_path}")
    print()

    # Find all C# files
    cs_files = find_cs_files(base_dir, config.source_dirs)
    print(f"Found {len(cs_files)} C# files")

    if not cs_files:
        print("No C# files found to process")
        return

    # Calculate output paths and check for existing docs
    output_dir = base_dir / config.output_dir
    existing_docs = find_existing_docs(output_dir)

    # Filter files based on existence and modification times
    # Each entry is (cs_file, output_path, is_stale)
    files_to_process: List[tuple] = []
    skipped_up_to_date = 0
    new_files = 0
    stale_files = 0

    for cs_file in cs_files:
        output_path = get_output_path(cs_file, base_dir, config.source_dirs, output_dir)

        if config.force_regenerate:
            # Force mode: process all files
            is_stale = output_path in existing_docs
            files_to_process.append((cs_file, output_path, is_stale))
            if is_stale:
                stale_files += 1
            else:
                new_files += 1
        elif output_path not in existing_docs:
            # New file without documentation
            print(f"New file: {cs_file}")
            files_to_process.append((cs_file, output_path, False))
            new_files += 1
        elif is_doc_stale(cs_file, output_path):
            # Source file has been modified since doc was generated
            print(f"Stale doc (source modified): {cs_file}")
            files_to_process.append((cs_file, output_path, True))
            stale_files += 1
        else:
            # Documentation is up-to-date
            print(f"Skipping (up-to-date): {cs_file}")
            skipped_up_to_date += 1

    print(f"\nSummary:")
    print(f"  - New files to document: {new_files}")
    print(f"  - Stale docs to regenerate: {stale_files}")
    print(f"  - Up-to-date (skipped): {skipped_up_to_date}")
    print(f"  - Total to process: {len(files_to_process)}")
    print()

    if not files_to_process:
        print("All files already have documentation!")
        return

    # Process in batches
    total_processed = 0
    total_batches = (
        len(files_to_process) + config.parallel_instances - 1
    ) // config.parallel_instances

    try:
        for batch_num in range(0, len(files_to_process), config.parallel_instances):
            batch = files_to_process[batch_num : batch_num + config.parallel_instances]
            current_batch_num = batch_num // config.parallel_instances + 1

            print(f"\n{'='*60}")
            print(f"Batch {current_batch_num}/{total_batches} ({len(batch)} files)")
            print(f"{'='*60}\n")

            success_count = await process_batch(
                batch, config, base_dir, backend_manager
            )
            total_processed += success_count

            print(
                f"\nBatch {current_batch_num} complete: {success_count}/{len(batch)} successful"
            )

            # Wait between batches to avoid rate limiting (except for the last batch)
            if batch_num + config.parallel_instances < len(files_to_process):
                print(
                    f"Waiting {config.timeout_between_batches}s to avoid rate limiting..."
                )
                await asyncio.sleep(config.timeout_between_batches)
    except RateLimitExceeded as e:
        # All backends exhausted, stop processing
        print(
            f"\n{'='*60}",
            file=sys.stderr,
        )
        print(
            "ALL BACKENDS RATE LIMITED",
            file=sys.stderr,
        )
        print(
            f"{'='*60}",
            file=sys.stderr,
        )
        print(
            f"\nStopped after processing {total_processed} files.",
            file=sys.stderr,
        )

        if e.wait_time:
            if e.wait_time >= 3600:
                wait_str = f"{e.wait_time / 3600:.1f} hours"
            elif e.wait_time >= 60:
                wait_str = f"{e.wait_time / 60:.0f} minutes"
            else:
                wait_str = f"{e.wait_time:.0f} seconds"
            print(
                f"\n⏰ Estimated wait time: {wait_str}",
                file=sys.stderr,
            )
            print(
                f"   Try again after: {datetime.now().replace(microsecond=0) + timedelta(seconds=e.wait_time)}",
                file=sys.stderr,
            )

        print(
            f"\nRun the script again later to continue. Already processed files will be skipped.",
            file=sys.stderr,
        )
        print(
            f"{'='*60}\n",
            file=sys.stderr,
        )
        sys.exit(1)

    print(f"\n{'='*60}")
    print(
        f"Complete! Processed {total_processed}/{len(files_to_process)} files successfully"
    )
    print(f"Documentation output: {output_dir}")

    # Print backend status
    status = backend_manager.get_status()
    print(f"\nBackend status:")
    for name, info in status.items():
        status_str = "✓ available" if info["available"] else "✗ unavailable"
        print(f"  - {name}: {status_str}")
    print(f"{'='*60}")


def main():
    """Main entry point."""
    parser = argparse.ArgumentParser(
        description="Generate code walkthrough documentation using AI CLI tools.\n\n"
        "Supports GitHub Copilot CLI and Claude Code CLI with automatic failover.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Use auto mode (Claude first, then Copilot fallback) - RECOMMENDED
  %(prog)s --cli auto

  # Use Claude Code CLI only
  %(prog)s --cli claude

  # Use GitHub Copilot CLI only
  %(prog)s --cli copilot

  # Use 5 parallel instances with 90s timeout between batches
  %(prog)s --parallel 5 --timeout 90

  # Process only Sharpy.Compiler with custom output directory
  %(prog)s --source-dirs src/Sharpy.Compiler --output-dir docs/compiler_walkthrough

  # Increase timeout for CLI to 3 minutes per file
  %(prog)s --copilot-timeout 180

CLI Providers:
  auto:    Automatic failover (Claude → Copilot). RECOMMENDED.
  claude:  Use Claude Code CLI only
  copilot: Use GitHub Copilot CLI only

Backend Failover (--cli auto):
  When using 'auto' mode, the script will:
  1. Try Claude Code first (preferred)
  2. If Claude is rate-limited, automatically switch to Copilot
  3. If both are rate-limited, exit with estimated wait time

Security Model:
  Both CLI tools are restricted to ONLY 'Read' and 'Write' operations:
  - Read: Can read source files for analysis
  - Write: Can create new markdown documentation files

  NOT allowed (for safety):
  - Bash/shell: No arbitrary command execution
  - Edit: Cannot modify existing files
  - Delete: Cannot remove files
        """,
    )

    parser.add_argument(
        "--parallel",
        "-p",
        type=int,
        default=3,
        help="Number of parallel instances (default: 3)",
    )

    parser.add_argument(
        "--timeout",
        "-t",
        type=int,
        default=60,
        help="Timeout in seconds between batches to avoid rate limiting (default: 60)",
    )

    parser.add_argument(
        "--copilot-timeout",
        type=int,
        default=300,
        help="Timeout in seconds for each CLI analysis (default: 300)",
    )

    parser.add_argument(
        "--source-dirs",
        nargs="+",
        help="Source directories to process (default: src/Sharpy.Cli src/Sharpy.Compiler)",
    )

    parser.add_argument(
        "--output-dir",
        default="docs/implementation_walkthrough",
        help="Output directory for markdown documentation (default: docs/implementation_walkthrough)",
    )

    parser.add_argument(
        "--cli",
        choices=["auto", "copilot", "claude"],
        default="auto",
        help="CLI provider: 'auto' for failover (Claude→Copilot), or specific provider (default: auto)",
    )

    parser.add_argument(
        "--force",
        "-f",
        action="store_true",
        help="Force regeneration of all documentation, even if up-to-date",
    )

    args = parser.parse_args()

    # Validate arguments
    if args.parallel < 1:
        print("Error: --parallel must be >= 1", file=sys.stderr)
        sys.exit(1)

    if args.timeout < 0:
        print("Error: --timeout must be >= 0", file=sys.stderr)
        sys.exit(1)

    # Note: Backend availability is checked in main_async via BackendManager
    # This allows for better error messages and failover handling

    # Create config
    config = Config(
        parallel_instances=args.parallel,
        timeout_between_batches=args.timeout,
        copilot_timeout=args.copilot_timeout,
        source_dirs=args.source_dirs,
        output_dir=args.output_dir,
        cli_provider=args.cli,
        force_regenerate=args.force,
    )

    # Run async main
    try:
        asyncio.run(main_async(config))
    except KeyboardInterrupt:
        print("\n\nInterrupted by user", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
